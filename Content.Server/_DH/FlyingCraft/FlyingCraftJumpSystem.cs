// Dark Haven - This file is licensed under AGPLv3
// Copyright (c) 2026 Dark Haven Contributors
// See AGPLv3.txt for details.

using Content.Shared._DH.FlyingCraft;
using Content.Shared._DH.FlyingCraft.Components;
using Content.Shared.DoAfter;
using Content.Shared.Popups;
using Robust.Shared.Map;
using Robust.Shared.Timing;

namespace Content.Server._DH.FlyingCraft;

/// <summary>
/// The single-entity FTL "jump"/BSS (characteristic #7) for FTL-capable craft (tier 3+). The pilot picks a
/// destination on the HUD radar MAP (relayed via <see cref="FlyingCraftBeginFtlEvent"/>); the server charges a
/// per-tier DoAfter (5/4/3 s for tier 3/4/5) and then teleports the whole craft toward the target (clamped to
/// range), consuming fuel and going on cooldown. Deliberately NOT the grid FTL pipeline (which can't move a
/// mech-style entity).
/// </summary>
public sealed class FlyingCraftJumpSystem : EntitySystem
{
    [Dependency] private readonly SharedTransformSystem _xform = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly FlyingCraftSystem _flying = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeNetworkEvent<FlyingCraftBeginFtlEvent>(OnBeginFtl);
        SubscribeLocalEvent<FlyingCraftJumpComponent, FlyingCraftFtlChargeEvent>(OnFtlCharged);
    }

    /// <summary>Client relay: the seated pilot clicked an FTL destination on the radar MAP. Start the charge.</summary>
    private void OnBeginFtl(FlyingCraftBeginFtlEvent msg, EntitySessionEventArgs args)
    {
        if (args.SenderSession.AttachedEntity is not { } pilot
            || !TryComp<FlyingCraftPilotComponent>(pilot, out var pilotComp))
            return;

        var craft = GetEntity(msg.Craft);
        // Component presence IS the FTL-capability gate (only on tier 3-5 protos).
        if (pilotComp.Craft != craft
            || !TryComp<FlyingCraftJumpComponent>(craft, out var jump)
            || !TryComp<FlyingCraftComponent>(craft, out var craftComp))
            return;

        var now = _timing.CurTime;
        if (now < jump.NextJump)
        {
            _popup.PopupEntity(Loc.GetString("flying-craft-jump-cooldown"), craft, pilot);
            return;
        }

        if (TryComp<FlyingCraftFuelComponent>(craft, out var fuel) && fuel.Fuel < fuel.JumpCost)
        {
            _popup.PopupEntity(Loc.GetString("flying-craft-jump-no-fuel"), craft, pilot);
            return;
        }

        // Charge time by tier: III = 5 s, IV = 4 s, V = 3 s (and 5 s for any lower FTL tier, if ever authored).
        var seconds = craftComp.Tier switch
        {
            >= 5 => 3f,
            4 => 4f,
            _ => 5f,
        };

        var ev = new FlyingCraftFtlChargeEvent(msg.Destination);
        var doAfter = new DoAfterArgs(EntityManager, pilot, TimeSpan.FromSeconds(seconds), ev, craft)
        {
            BreakOnMove = false,        // you fly while charging
            RequireCanInteract = false, // contained pilot, no interaction target
        };
        _doAfter.TryStartDoAfter(doAfter);
        _popup.PopupEntity(Loc.GetString("flying-craft-jump-charging"), craft, pilot);
    }

    private void OnFtlCharged(Entity<FlyingCraftJumpComponent> ent, ref FlyingCraftFtlChargeEvent args)
    {
        if (args.Cancelled || args.Handled)
            return;

        // The pilot must still be seated in THIS craft (container removal doesn't auto-cancel the DoAfter).
        if (!TryComp<FlyingCraftPilotComponent>(args.Args.User, out var pilotComp) || pilotComp.Craft != ent.Owner)
            return;

        var now = _timing.CurTime;
        if (now < ent.Comp.NextJump) // re-gate cooldown
            return;

        TryComp<FlyingCraftFuelComponent>(ent, out var fuel);
        if (fuel != null && fuel.Fuel < fuel.JumpCost) // re-gate fuel (it can drop during the charge)
            return;

        var craft = _xform.GetMapCoordinates(ent);
        var target = _xform.ToMapCoordinates(GetCoordinates(args.Destination));
        if (target.MapId != craft.MapId)
            return;

        var delta = target.Position - craft.Position;
        var dist = delta.Length();
        if (dist < 0.01f)
            return;

        var dest = dist > ent.Comp.MaxRange
            ? craft.Position + delta / dist * ent.Comp.MaxRange
            : target.Position;

        _xform.SetWorldPosition(ent.Owner, dest);

        if (fuel != null)
            _flying.SetFuel((ent.Owner, fuel), fuel.Fuel - fuel.JumpCost);

        ent.Comp.NextJump = now + ent.Comp.Cooldown;
        Dirty(ent);
        args.Handled = true;
    }
}
