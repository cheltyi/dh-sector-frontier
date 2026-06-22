// Dark Haven - This file is licensed under AGPLv3
// Copyright (c) 2026 Dark Haven Contributors
// See AGPLv3.txt for details.

using System.Numerics;
using Content.Server.Atmos.EntitySystems;
using Content.Server.Body.Systems;
using Content.Server.Destructible;
using Content.Server.Destructible.Thresholds.Triggers;
using Content.Server.Movement.Systems;
using Content.Server.Resist;
using Content.Shared._DH.FlyingCraft;
using Content.Shared._DH.FlyingCraft.Components;
using Content.Shared.ActionBlocker;
using Content.Shared.Actions;
using Content.Shared.DoAfter;
using Content.Shared.DragDrop;
using Content.Shared.Interaction;
using Content.Shared.Movement.Components;
using Content.Shared.Popups;
using Robust.Shared.Containers;
using Robust.Shared.Physics.Components;

namespace Content.Server._DH.FlyingCraft;

/// <summary>
/// Enter/exit for flying-craft pilots. The pilot is placed INSIDE a container on the craft (so they are hidden in
/// the cockpit, not visibly perched on top). You climb in by DRAGGING yourself onto the craft (a ~3s DoAfter), or
/// by activating it (E); you leave via a granted action. While seated the pilot gets a mech-style redirect comp,
/// a combat-mode toggle, and has its escape-from-container suppressed (the cargo Storage would otherwise auto-eject
/// the pilot on every move). The flight controller (<see cref="FlyingCraftSystem"/>) reads the contained pilot's
/// input.
/// </summary>
public sealed class FlyingCraftPilotSystem : EntitySystem
{
    [Dependency] private readonly SharedContainerSystem _container = default!;
    [Dependency] private readonly SharedActionsSystem _actions = default!;
    [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;
    [Dependency] private readonly ActionBlockerSystem _actionBlocker = default!;
    [Dependency] private readonly ContentEyeSystem _eye = default!;
    [Dependency] private readonly SharedEyeSystem _eyeBase = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;

    public const string PilotContainerId = "flyingcraft-pilot";

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<FlyingCraftComponent, ActivateInWorldEvent>(OnActivate);
        SubscribeLocalEvent<FlyingCraftComponent, CanDropTargetEvent>(OnCanDrop);
        SubscribeLocalEvent<FlyingCraftComponent, DragDropTargetEvent>(OnDragDrop);
        SubscribeLocalEvent<FlyingCraftComponent, FlyingCraftEntryEvent>(OnEntryDoAfter);
        SubscribeLocalEvent<FlyingCraftComponent, FlyingCraftExitActionEvent>(OnExitAction);
        SubscribeLocalEvent<FlyingCraftComponent, FlyingCraftExitDoAfterEvent>(OnExitDoAfter);
        SubscribeLocalEvent<FlyingCraftComponent, EntInsertedIntoContainerMessage>(OnInserted);
        SubscribeLocalEvent<FlyingCraftComponent, EntRemovedFromContainerMessage>(OnRemoved);

        // Cockpit HUD on-screen control buttons (relayed from the client).
        SubscribeNetworkEvent<FlyingCraftHudButtonEvent>(OnHudButton);

        // Cockpit air: redirect the seated pilot's breathing to the craft's sealed internal mixture.
        SubscribeLocalEvent<FlyingCraftPilotComponent, InhaleLocationEvent>(OnPilotInhale);
        SubscribeLocalEvent<FlyingCraftPilotComponent, ExhaleLocationEvent>(OnPilotExhale);
        SubscribeLocalEvent<FlyingCraftPilotComponent, AtmosExposedGetAirEvent>(OnPilotExpose);
    }

    private void OnPilotInhale(EntityUid uid, FlyingCraftPilotComponent comp, InhaleLocationEvent args)
    {
        if (TryComp<FlyingCraftAirComponent>(comp.Craft, out var air))
            args.Gas = air.Air;
    }

    private void OnPilotExhale(EntityUid uid, FlyingCraftPilotComponent comp, ExhaleLocationEvent args)
    {
        // Exhale back into the same mixture so the closed cockpit loop doesn't dump gas into nullspace.
        if (TryComp<FlyingCraftAirComponent>(comp.Craft, out var air))
            args.Gas = air.Air;
    }

    private void OnPilotExpose(EntityUid uid, FlyingCraftPilotComponent comp, ref AtmosExposedGetAirEvent args)
    {
        if (args.Handled)
            return;

        if (TryComp<FlyingCraftAirComponent>(comp.Craft, out var air))
        {
            args.Gas = air.Air;
            args.Handled = true;
        }
    }

    private void OnActivate(Entity<FlyingCraftComponent> ent, ref ActivateInWorldEvent args)
    {
        if (args.Handled)
            return;

        if (TryStartEntry(ent, args.User))
            args.Handled = true;
    }

    /// <summary>Starts the "climb in" DoAfter (interrupted if the climber moves). Used by both E and drag-drop.</summary>
    private bool TryStartEntry(Entity<FlyingCraftComponent> ent, EntityUid user)
    {
        if (!CanPilot(ent, user))
            return false;

        var doAfter = new DoAfterArgs(EntityManager, user, ent.Comp.EntryDelay,
            new FlyingCraftEntryEvent(), ent.Owner, target: ent.Owner)
        {
            BreakOnMove = true,
        };
        return _doAfter.TryStartDoAfter(doAfter);
    }

    private void OnCanDrop(Entity<FlyingCraftComponent> ent, ref CanDropTargetEvent args)
    {
        // Always claim the event so the drop highlights (mirrors the mech), but only allow an empty cockpit + a
        // mob that can actually pilot.
        args.Handled = true;
        args.CanDrop |= CanPilot(ent, args.Dragged);
    }

    private void OnDragDrop(Entity<FlyingCraftComponent> ent, ref DragDropTargetEvent args)
    {
        if (args.Handled)
            return;

        // The dragged mob climbs in over EntryDelay; raised back on the craft so OnEntryDoAfter fires here.
        if (TryStartEntry(ent, args.Dragged))
            args.Handled = true;
    }

    private void OnEntryDoAfter(Entity<FlyingCraftComponent> ent, ref FlyingCraftEntryEvent args)
    {
        if (args.Cancelled || args.Handled)
            return;

        var user = args.Args.User;
        if (!CanPilot(ent, user))
            return;

        var container = _container.EnsureContainer<ContainerSlot>(ent, PilotContainerId);
        if (_container.Insert(user, container))
            args.Handled = true;
    }

    private void OnExitAction(Entity<FlyingCraftComponent> ent, ref FlyingCraftExitActionEvent args)
    {
        args.Handled = true;

        if (!_container.TryGetContainer(ent, PilotContainerId, out var container)
            || container is not ContainerSlot { ContainedEntity: { } pilot })
            return;

        // Can't bail out while the craft is moving fast.
        if (TryComp<PhysicsComponent>(ent, out var body) && body.LinearVelocity.Length() > ent.Comp.ExitMaxSpeed)
        {
            _popup.PopupEntity(Loc.GetString("flying-craft-exit-too-fast"), ent, pilot);
            return;
        }

        // Climb out over ExitDelay; BreakOnMove on the contained pilot cancels it if the craft starts moving.
        var doAfter = new DoAfterArgs(EntityManager, pilot, ent.Comp.ExitDelay,
            new FlyingCraftExitDoAfterEvent(), ent.Owner, target: ent.Owner)
        {
            BreakOnMove = true,
        };
        _doAfter.TryStartDoAfter(doAfter);
    }

    private void OnExitDoAfter(Entity<FlyingCraftComponent> ent, ref FlyingCraftExitDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled)
            return;

        Eject(ent);
        args.Handled = true;
    }

    /// <summary>Client relay: the seated pilot pressed an on-screen HUD control button.</summary>
    private void OnHudButton(FlyingCraftHudButtonEvent msg, EntitySessionEventArgs args)
    {
        if (args.SenderSession.AttachedEntity is not { } pilot
            || !TryComp<FlyingCraftPilotComponent>(pilot, out var pilotComp))
            return;

        var craft = GetEntity(msg.Craft);
        if (pilotComp.Craft != craft || !TryComp<FlyingCraftComponent>(craft, out var craftComp))
            return;

        switch (msg.Button)
        {
            case FlyingCraftHudButton.Exit:
                Eject((craft, craftComp));
                break;
            case FlyingCraftHudButton.SwapWeapon:
                RaiseLocalEvent(craft, new FlyingCraftSwapWeaponActionEvent { Performer = pilot });
                break;
            case FlyingCraftHudButton.Headlight:
                RaiseLocalEvent(craft, new FlyingCraftHeadlightActionEvent { Performer = pilot });
                break;
        }
    }

    /// <summary>Removes the current pilot (drops them next to the craft).</summary>
    public void Eject(Entity<FlyingCraftComponent> ent)
    {
        if (_container.TryGetContainer(ent, PilotContainerId, out var container)
            && container is ContainerSlot { ContainedEntity: { } pilot })
        {
            _container.RemoveEntity(ent.Owner, pilot);
        }
    }

    /// <summary>An empty cockpit + a mob that can move/provide input may pilot.</summary>
    private bool CanPilot(Entity<FlyingCraftComponent> ent, EntityUid user)
    {
        if (!HasComp<InputMoverComponent>(user) || !_actionBlocker.CanMove(user))
            return false;

        return !_container.TryGetContainer(ent, PilotContainerId, out var container)
               || container is not ContainerSlot { ContainedEntity: not null };
    }

    private void OnInserted(Entity<FlyingCraftComponent> ent, ref EntInsertedIntoContainerMessage args)
    {
        if (args.Container.ID != PilotContainerId)
            return;

        var pilot = args.Entity;

        // Mech-style redirect so the combat client/server can hop pilot -> craft.
        var pilotComp = EnsureComp<FlyingCraftPilotComponent>(pilot);
        pilotComp.Craft = ent.Owner;
        Dirty(pilot, pilotComp);

        // The cargo Storage makes this container escapable; strip escape so moving doesn't auto-eject the pilot.
        ent.Comp.PilotHadEscape = RemComp<CanEscapeInventoryComponent>(pilot);

        _actions.AddAction(pilot, ref ent.Comp.ExitActionEntity, ent.Comp.ExitAction, ent.Owner);

        // Networked HUD snapshot for the cockpit HUD (ammo is filled by the weapon system; fill max health here
        // from the destruction threshold, which is server-only).
        var hud = EnsureComp<FlyingCraftHudStateComponent>(ent.Owner);
        hud.MaxHealth = GetMaxHealth(ent.Owner);
        Dirty(ent.Owner, hud);

        // Scout craft give the pilot a wider recon view (zoom out + larger PVS range); restored on exit.
        if (ent.Comp.Class == FlyingCraftClass.Scout && TryComp<ContentEyeComponent>(pilot, out var eye))
        {
            ent.Comp.PilotPrevMaxZoom = eye.MaxZoom;
            ent.Comp.PilotPrevTargetZoom = eye.TargetZoom;
            _eye.SetMaxZoom(pilot, ent.Comp.ScoutZoom, eye); // raise the cap first...
            _eye.SetZoom(pilot, ent.Comp.ScoutZoom, eye: eye); // ...then zoom out to it.

            if (TryComp<EyeComponent>(pilot, out var engineEye))
            {
                ent.Comp.PilotPrevPvsScale = engineEye.PvsScale;
                _eyeBase.SetPvsScale((pilot, engineEye), ent.Comp.ScoutPvsScale);
            }
        }
    }

    private void OnRemoved(Entity<FlyingCraftComponent> ent, ref EntRemovedFromContainerMessage args)
    {
        if (args.Container.ID != PilotContainerId)
            return;

        var pilot = args.Entity;

        // Guard pilot-side mutations: when the craft is destroyed the pilot is being recursively deleted
        // (terminating), and adding/removing components on a terminating entity throws.
        if (!TerminatingOrDeleted(pilot))
        {
            _actions.RemoveAction(pilot, ent.Comp.ExitActionEntity);

            if (ent.Comp.PilotHadEscape)
                EnsureComp<CanEscapeInventoryComponent>(pilot);

            RemComp<FlyingCraftPilotComponent>(pilot);

            // Restore the pilot's view if a Scout widened it.
            if (ent.Comp.PilotPrevMaxZoom is { } prevMax && TryComp<ContentEyeComponent>(pilot, out var eye))
            {
                _eye.SetMaxZoom(pilot, prevMax, eye);
                _eye.SetZoom(pilot, ent.Comp.PilotPrevTargetZoom ?? Vector2.One, eye: eye);
            }

            if (ent.Comp.PilotPrevPvsScale is { } prevPvs && TryComp<EyeComponent>(pilot, out var engineEye))
                _eyeBase.SetPvsScale((pilot, engineEye), prevPvs);
        }

        ent.Comp.ExitActionEntity = null;
        ent.Comp.PilotHadEscape = false;
        ent.Comp.PilotPrevMaxZoom = null;
        ent.Comp.PilotPrevTargetZoom = null;
        ent.Comp.PilotPrevPvsScale = null;

        // Drop aim/fire state so a parked/empty craft doesn't keep aiming or firing (skip if the craft is going away).
        if (!TerminatingOrDeleted(ent))
        {
            ClearCombatState(ent);
            RemComp<FlyingCraftHudStateComponent>(ent);
        }
    }

    /// <summary>Clears aim/brake/fire state (on exit / when no longer piloted).</summary>
    private void ClearCombatState(Entity<FlyingCraftComponent> ent)
    {
        ent.Comp.CursorGoalRotation = null;
        ent.Comp.Braking = false;
        ent.Comp.ManualTurn = 0;
        if (TryComp<FlyingCraftWeaponsComponent>(ent, out var weapons))
        {
            foreach (var weapon in weapons.Weapons)
                weapon.Firing = false;
        }
    }

    /// <summary>Reads the craft's destruction threshold (max hull health) from its Destructible DamageTrigger.</summary>
    private float GetMaxHealth(EntityUid craft)
    {
        if (!TryComp<DestructibleComponent>(craft, out var destructible))
            return 0f;

        foreach (var threshold in destructible.Thresholds)
        {
            if (threshold.Trigger is DamageTrigger trigger)
                return trigger.Damage;
        }

        return 0f;
    }
}
