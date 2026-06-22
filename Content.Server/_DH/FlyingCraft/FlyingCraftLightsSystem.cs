// Dark Haven - This file is licensed under AGPLv3
// Copyright (c) 2026 Dark Haven Contributors
// See AGPLv3.txt for details.

using System.Numerics;
using Content.Shared._DH.FlyingCraft;
using Content.Shared._DH.FlyingCraft.Components;
using Content.Shared.Actions;
using Robust.Shared.Containers;
using Robust.Shared.Map;

namespace Content.Server._DH.FlyingCraft;

/// <summary>
/// Spawns the craft's light children on MapInit (green port / red starboard nav lights, a faint headlight glow, and
/// the directional headlight beam — a cone child given a 180° local rotation so autoRot aims it FORWARD along the
/// nose), grants the seated pilot a "toggle headlight" action that flips the beam + glow on/off (no battery, mirrors
/// the mech light), and cleans the children up when the craft is removed.
/// </summary>
public sealed class FlyingCraftLightsSystem : EntitySystem
{
    [Dependency] private readonly SharedPointLightSystem _pointLights = default!;
    [Dependency] private readonly SharedActionsSystem _actions = default!;
    [Dependency] private readonly SharedTransformSystem _xform = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<FlyingCraftLightsComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<FlyingCraftLightsComponent, EntInsertedIntoContainerMessage>(OnPilotInserted);
        SubscribeLocalEvent<FlyingCraftLightsComponent, EntRemovedFromContainerMessage>(OnPilotRemoved);
        SubscribeLocalEvent<FlyingCraftLightsComponent, FlyingCraftHeadlightActionEvent>(OnHeadlight);
        SubscribeLocalEvent<FlyingCraftLightsComponent, ComponentShutdown>(OnShutdown);
    }

    private void OnMapInit(Entity<FlyingCraftLightsComponent> ent, ref MapInitEvent args)
    {
        // SpawnAttachedTo parents the child to the craft at the given local offset; it rotates/moves with the hull.
        if (ent.Comp.PortLight == null || Deleted(ent.Comp.PortLight))
            ent.Comp.PortLight = SpawnAttachedTo(ent.Comp.NavLightPort,
                new EntityCoordinates(ent.Owner, ent.Comp.PortOffset));

        if (ent.Comp.StarboardLight == null || Deleted(ent.Comp.StarboardLight))
            ent.Comp.StarboardLight = SpawnAttachedTo(ent.Comp.NavLightStarboard,
                new EntityCoordinates(ent.Owner, ent.Comp.StarboardOffset));

        // The headlight's soft glow lives at the hull centre and is toggled with the directional cone.
        if (ent.Comp.HeadlightGlowLight == null || Deleted(ent.Comp.HeadlightGlowLight))
            ent.Comp.HeadlightGlowLight = SpawnAttachedTo(ent.Comp.HeadlightGlow,
                new EntityCoordinates(ent.Owner, Vector2.Zero));

        // The directional beam is a child at the hull centre rotated 180° locally, so with autoRot its cone aims
        // FORWARD along the nose (child worldRot = craft heading + 180°). PointLight.Rotation can't be set via
        // YAML/server (Animatable-only, write-locked), so we aim it through the child's transform instead.
        if (ent.Comp.HeadlightBeamLight == null || Deleted(ent.Comp.HeadlightBeamLight))
        {
            var beam = SpawnAttachedTo(ent.Comp.HeadlightBeam, new EntityCoordinates(ent.Owner, Vector2.Zero));
            _xform.SetLocalRotation(beam, Math.PI);
            ent.Comp.HeadlightBeamLight = beam;
        }
    }

    private void OnPilotInserted(Entity<FlyingCraftLightsComponent> ent, ref EntInsertedIntoContainerMessage args)
    {
        if (args.Container.ID != FlyingCraftPilotSystem.PilotContainerId)
            return;

        _actions.AddAction(args.Entity, ref ent.Comp.HeadlightActionEntity, ent.Comp.HeadlightAction, ent.Owner);
    }

    private void OnPilotRemoved(Entity<FlyingCraftLightsComponent> ent, ref EntRemovedFromContainerMessage args)
    {
        if (args.Container.ID != FlyingCraftPilotSystem.PilotContainerId)
            return;

        if (!TerminatingOrDeleted(args.Entity))
            _actions.RemoveAction(args.Entity, ent.Comp.HeadlightActionEntity);

        ent.Comp.HeadlightActionEntity = null;
    }

    private void OnHeadlight(Entity<FlyingCraftLightsComponent> ent, ref FlyingCraftHeadlightActionEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;

        // The beam (forward cone) is the master; the glow follows it. Both are child entities.
        if (ent.Comp.HeadlightBeamLight is not { } beam || TerminatingOrDeleted(beam)
            || !_pointLights.TryGetLight(beam, out var beamLight))
            return;

        var newState = !beamLight.Enabled;
        _pointLights.SetEnabled(beam, newState, beamLight);

        if (ent.Comp.HeadlightGlowLight is { } glow && !TerminatingOrDeleted(glow)
            && _pointLights.TryGetLight(glow, out var glowLight))
            _pointLights.SetEnabled(glow, newState, glowLight);
    }

    private void OnShutdown(Entity<FlyingCraftLightsComponent> ent, ref ComponentShutdown args)
    {
        // The nav/glow lights are parented (not contained) child entities — delete them with the craft.
        if (ent.Comp.PortLight is { } port && !TerminatingOrDeleted(port))
            QueueDel(port);
        if (ent.Comp.StarboardLight is { } starboard && !TerminatingOrDeleted(starboard))
            QueueDel(starboard);
        if (ent.Comp.HeadlightGlowLight is { } glow && !TerminatingOrDeleted(glow))
            QueueDel(glow);
        if (ent.Comp.HeadlightBeamLight is { } beam && !TerminatingOrDeleted(beam))
            QueueDel(beam);

        ent.Comp.PortLight = null;
        ent.Comp.StarboardLight = null;
        ent.Comp.HeadlightGlowLight = null;
        ent.Comp.HeadlightBeamLight = null;
    }
}
