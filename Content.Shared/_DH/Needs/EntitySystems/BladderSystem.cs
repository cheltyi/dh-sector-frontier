// Dark Haven - This file is licensed under AGPLv3
// Copyright (c) 2026 Dark Haven Contributors
// See AGPLv3.txt for details.

using Content.Shared._DH.Needs.Components;
using Content.Shared.Alert;
using Content.Shared.Buckle.Components;
using Content.Shared.Movement.Systems;
using Content.Shared.Rejuvenate;
using Content.Shared.Toilet.Components;
using Robust.Shared.Timing;

namespace Content.Shared._DH.Needs.EntitySystems;

/// <summary>
/// Drives the combined bladder need: fills slowly over time (faster the more the mob eats/drinks), slows the
/// mob when nearly bursting, and empties while the mob sits on a toilet.
/// </summary>
public sealed class BladderSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly AlertsSystem _alerts = default!;
    [Dependency] private readonly MovementSpeedModifierSystem _movement = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<BladderComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<BladderComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<BladderComponent, RefreshMovementSpeedModifiersEvent>(OnRefreshMovespeed);
        SubscribeLocalEvent<BladderComponent, RejuvenateEvent>(OnRejuvenate);
        SubscribeLocalEvent<BladderComponent, IngestedEvent>(OnIngested);
    }

    private void OnMapInit(Entity<BladderComponent> ent, ref MapInitEvent args)
    {
        ent.Comp.NextUpdateTime = _timing.CurTime;
        ent.Comp.LastBand = GetBand(ent.Comp);
        Dirty(ent);
    }

    private void OnShutdown(Entity<BladderComponent> ent, ref ComponentShutdown args)
    {
        _alerts.ClearAlertCategory(ent, ent.Comp.AlertCategory);
    }

    private void OnRejuvenate(Entity<BladderComponent> ent, ref RejuvenateEvent args)
    {
        SetValue(ent, 0f);
    }

    private void OnIngested(Entity<BladderComponent> ent, ref IngestedEvent args)
    {
        SetValue(ent, ent.Comp.Value + (args.IsDrink ? ent.Comp.DrinkFill : ent.Comp.FoodFill));
    }

    private void OnRefreshMovespeed(Entity<BladderComponent> ent, ref RefreshMovementSpeedModifiersEvent args)
    {
        // Only the bursting band slows the mob (like hunger/thirst at their worst).
        if (GetBand(ent.Comp) == 2)
            args.ModifySpeed(ent.Comp.BurstingSlowdown, ent.Comp.BurstingSlowdown);
    }

    public void SetValue(Entity<BladderComponent> ent, float value)
    {
        ent.Comp.Value = Math.Clamp(value, 0f, ent.Comp.Max);
        UpdateEffects(ent);
        Dirty(ent);
    }

    /// <summary>0 = fine, 1 = full (alert), 2 = bursting (alert + slow).</summary>
    private int GetBand(BladderComponent comp)
    {
        if (comp.Value >= comp.BurstingThreshold)
            return 2;
        if (comp.Value >= comp.FullThreshold)
            return 1;
        return 0;
    }

    private void UpdateEffects(Entity<BladderComponent> ent)
    {
        var band = GetBand(ent.Comp);
        if (band == ent.Comp.LastBand)
            return;

        ent.Comp.LastBand = band;
        _movement.RefreshMovementSpeedModifiers(ent);

        switch (band)
        {
            case 2:
                _alerts.ShowAlert(ent, ent.Comp.BurstingAlert);
                break;
            case 1:
                _alerts.ShowAlert(ent, ent.Comp.FullAlert);
                break;
            default:
                _alerts.ClearAlertCategory(ent, ent.Comp.AlertCategory);
                break;
        }
    }

    private bool OnToilet(EntityUid uid)
    {
        return TryComp<BuckleComponent>(uid, out var buckle)
               && buckle.BuckledTo is { } strap
               && HasComp<ToiletComponent>(strap);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<BladderComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            if (_timing.CurTime < comp.NextUpdateTime)
                continue;
            comp.NextUpdateTime += comp.UpdateRate;

            var dt = (float) comp.UpdateRate.TotalSeconds;

            comp.Value = Math.Clamp(
                comp.Value + (OnToilet(uid) ? -comp.RelieveRate : comp.RiseRate) * dt,
                0f,
                comp.Max);

            UpdateEffects((uid, comp));
            Dirty(uid, comp);
        }
    }
}
