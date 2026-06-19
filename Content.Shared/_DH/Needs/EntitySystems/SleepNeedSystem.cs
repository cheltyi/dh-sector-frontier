// Dark Haven - This file is licensed under AGPLv3
// Copyright (c) 2026 Dark Haven Contributors
// See AGPLv3.txt for details.

using Content.Shared._DH.Needs.Components;
using Content.Shared.Alert;
using Content.Shared.Bed.Sleep;
using Content.Shared.Movement.Systems;
using Content.Shared.Rejuvenate;
using Robust.Shared.Timing;

namespace Content.Shared._DH.Needs.EntitySystems;

/// <summary>
/// Drives the sleep need: tiredness rises while awake, slows the mob as it climbs, makes it pass out at the
/// maximum, and drains while it sleeps. Modeled on HungerSystem/ThirstSystem (a shared per-tick meter).
/// </summary>
public sealed class SleepNeedSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly AlertsSystem _alerts = default!;
    [Dependency] private readonly MovementSpeedModifierSystem _movement = default!;
    [Dependency] private readonly SleepingSystem _sleeping = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<SleepNeedComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<SleepNeedComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<SleepNeedComponent, RefreshMovementSpeedModifiersEvent>(OnRefreshMovespeed);
        SubscribeLocalEvent<SleepNeedComponent, RejuvenateEvent>(OnRejuvenate);
    }

    private void OnMapInit(Entity<SleepNeedComponent> ent, ref MapInitEvent args)
    {
        ent.Comp.NextUpdateTime = _timing.CurTime;
        ent.Comp.LastBand = GetBand(ent.Comp);
        Dirty(ent);
    }

    private void OnShutdown(Entity<SleepNeedComponent> ent, ref ComponentShutdown args)
    {
        _alerts.ClearAlertCategory(ent, ent.Comp.AlertCategory);
    }

    private void OnRejuvenate(Entity<SleepNeedComponent> ent, ref RejuvenateEvent args)
    {
        SetValue(ent, 0f);
    }

    private void OnRefreshMovespeed(Entity<SleepNeedComponent> ent, ref RefreshMovementSpeedModifiersEvent args)
    {
        var band = GetBand(ent.Comp);
        if (band == 2)
            args.ModifySpeed(ent.Comp.ExhaustedSlowdown, ent.Comp.ExhaustedSlowdown);
        else if (band == 1)
            args.ModifySpeed(ent.Comp.TiredSlowdown, ent.Comp.TiredSlowdown);
    }

    public void SetValue(Entity<SleepNeedComponent> ent, float value)
    {
        ent.Comp.Value = Math.Clamp(value, 0f, ent.Comp.Max);
        UpdateEffects(ent);
        Dirty(ent);
    }

    /// <summary>0 = rested, 1 = tired (mild slow), 2 = exhausted (strong slow).</summary>
    private int GetBand(SleepNeedComponent comp)
    {
        if (comp.Value >= comp.ExhaustedThreshold)
            return 2;
        if (comp.Value >= comp.TiredThreshold)
            return 1;
        return 0;
    }

    private void UpdateEffects(Entity<SleepNeedComponent> ent)
    {
        var band = GetBand(ent.Comp);
        if (band == ent.Comp.LastBand)
            return;

        ent.Comp.LastBand = band;
        _movement.RefreshMovementSpeedModifiers(ent);

        switch (band)
        {
            case 2:
                _alerts.ShowAlert(ent, ent.Comp.ExhaustedAlert);
                break;
            case 1:
                _alerts.ShowAlert(ent, ent.Comp.TiredAlert);
                break;
            default:
                _alerts.ClearAlertCategory(ent, ent.Comp.AlertCategory);
                break;
        }
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<SleepNeedComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            if (_timing.CurTime < comp.NextUpdateTime)
                continue;
            comp.NextUpdateTime += comp.UpdateRate;

            var dt = (float) comp.UpdateRate.TotalSeconds;
            var asleep = HasComp<SleepingComponent>(uid);

            comp.Value = Math.Clamp(
                comp.Value + (asleep ? -comp.SleepDrainRate : comp.RiseRate) * dt,
                0f,
                comp.Max);

            UpdateEffects((uid, comp));

            // Pass out at the maximum (soft sleep — wakeable). Auto-wake once rested if we forced it.
            if (!asleep && comp.Value >= comp.Max)
            {
                if (_sleeping.TrySleeping(uid))
                    comp.PassedOut = true;
            }
            else if (asleep && comp.PassedOut && comp.Value <= 0f)
            {
                _sleeping.TryWaking(uid);
                comp.PassedOut = false;
            }

            Dirty(uid, comp);
        }
    }
}
