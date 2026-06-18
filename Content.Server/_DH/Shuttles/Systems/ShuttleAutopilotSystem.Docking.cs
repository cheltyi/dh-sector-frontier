// Dark Haven - This file is licensed under AGPLv3
// Copyright (c) 2026 Dark Haven Contributors
// See AGPLv3.txt for details.

using System.Numerics;
using Content.Server.Shuttles;
using Content.Server.Shuttles.Components;
using Content.Shared._DH.Shuttles.Components;
using Content.Shared.Movement.Systems;
using Content.Shared.Shuttles.Components;
using Robust.Shared.Map.Components;
using Robust.Shared.Physics.Components;

namespace Content.Server._DH.Shuttles.Systems;

// Tier-3 auto-docking. Instead of teleporting onto the dock, the ship performs a believable maneuver: it
// cruises to a standoff point (set by the A* route), rotates into the docked orientation, then eases in —
// using lateral thrust as well, so side-mounted ports slide in sideways — and latches the real ports with
// DockingSystem.Dock once they are aligned. A timeout snap-docks as a last resort so it never hangs.
public sealed partial class ShuttleAutopilotSystem
{
    /// <summary>
    /// Resolves the docking config and live docked pose (origin world position + world rotation) for the
    /// chosen port. The config is cached per grid (see <see cref="_dockConfigs"/>): with
    /// <paramref name="recompute"/> it is computed fresh and cached (do this while still far away), otherwise
    /// the cached one is reused. The pose is always re-derived from the (target-grid-relative) config against
    /// the target grid's CURRENT transform, so a moving/rotating station is tracked.
    /// </summary>
    private bool TryResolveDock(EntityUid grid, ShuttleAutopilotComponent ap, bool recompute,
        out DockingConfig config, out EntityUid targetGrid, out Vector2 dockedOrigin, out Angle dockedAngle)
    {
        config = default!;
        targetGrid = default;
        dockedOrigin = default;
        dockedAngle = default;

        if (ap.TargetDock is not { } dockNet)
            return false;

        var dockUid = GetEntity(dockNet);
        if (Deleted(dockUid))
            return false;

        var tg = Transform(dockUid).GridUid;
        if (tg == null)
            return false;
        targetGrid = tg.Value;

        DockingConfig? c;
        if (recompute || !_dockConfigs.TryGetValue(grid, out c) || c == null)
        {
            c = _dock.GetDockingConfigForGridDock(grid, targetGrid, dockUid, dockType: DockType.Airlock | DockType.Gas);
            if (c != null)
                _dockConfigs[grid] = c;
            else if (!_dockConfigs.TryGetValue(grid, out c) || c == null)
                return false; // no fresh fit and nothing cached to fall back on
        }

        config = c;
        dockedOrigin = _xform.ToMapCoordinates(c.Coordinates).Position;
        dockedAngle = c.Angle + _xform.GetWorldRotation(targetGrid);
        return true;
    }

    /// <summary>The precision docking maneuver: orient, slide in, then latch (or snap on timeout).</summary>
    private void DriveDock(EntityUid grid, ShuttleAutopilotComponent ap, PilotComponent pilot, PhysicsComponent body,
        TransformComponent xform, DockingConfig config, Vector2 dockedOrigin, Angle dockedAngle, float dt)
    {
        // Ports already aligned enough to mate? Latch them for real — no teleport.
        if (TryLatchDock(config))
        {
            Disengage(grid, ap, "autopilot-popup-docked");
            return;
        }

        // The soft approach has a time budget; snap-dock as a last resort so docking never gets stuck.
        ap.DockTimer += dt;
        if (ap.DockTimer >= DockTimeout)
        {
            _shuttle.FTLDock((grid, xform), config);
            Disengage(grid, ap, "autopilot-popup-docked");
            return;
        }

        var gridRot = _xform.GetWorldRotation(grid);
        var consoleRot = Angle.Zero;
        if (pilot.Console is { } con && TryComp<TransformComponent>(con, out var conXform))
            consoleRot = conXform.LocalRotation;

        var fwd = (gridRot + consoleRot).RotateVec(Vector2.UnitY);
        var right = (gridRot + consoleRot).RotateVec(Vector2.UnitX);
        var forward = (gridRot + consoleRot + Angle.FromDegrees(ap.ForwardAngleOffset)).RotateVec(Vector2.UnitY);
        // Facing that puts the hull at the docked orientation (aligning forward to this makes gridRot -> dockedAngle).
        var faceDir = (dockedAngle + consoleRot + Angle.FromDegrees(ap.ForwardAngleOffset)).RotateVec(Vector2.UnitY);

        var buttons = ShuttleButtons.None;

        // 1. Rotate the hull to the docked orientation (shared gentle yaw controller).
        AddYaw(ref buttons, forward, faceDir, body.AngularVelocity);

        // 2. Translate (omnidirectional, so side ports slide in sideways). While still turning, hold position
        //    (offset = 0 -> ApproachAxis just damps drift) so inertia doesn't carry us off the dock; once
        //    roughly oriented, ease the origin onto the docked spot.
        var yawErr = MathF.Atan2(forward.X * faceDir.Y - forward.Y * faceDir.X, Vector2.Dot(forward, faceDir));
        var offset = MathF.Abs(yawErr) < DockAlignGateDeg * (MathF.PI / 180f)
            ? dockedOrigin - _xform.GetWorldPosition(xform)
            : Vector2.Zero;
        var vel = body.LinearVelocity;
        ApproachAxis(ref buttons, Vector2.Dot(offset, fwd), Vector2.Dot(vel, fwd), DockApproachSpeed, DockPosDeadband, DockVelDeadband, ShuttleButtons.StrafeUp, ShuttleButtons.StrafeDown);
        ApproachAxis(ref buttons, Vector2.Dot(offset, right), Vector2.Dot(vel, right), DockApproachSpeed, DockPosDeadband, DockVelDeadband, ShuttleButtons.StrafeRight, ShuttleButtons.StrafeLeft);

        SetButtons(pilot, buttons);
    }

    /// <summary>Bang-bang approach for one axis: nudge toward the target at a slow capped speed, then settle.</summary>
    private static void ApproachAxis(ref ShuttleButtons buttons, float offset, float vel,
        float approachSpeed, float posDeadband, float velDeadband, ShuttleButtons positive, ShuttleButtons negative)
    {
        if (offset > posDeadband)
        {
            if (vel < approachSpeed)
                buttons |= positive;
            else if (vel > approachSpeed + 1f)
                buttons |= negative; // overspeed -> ease off
        }
        else if (offset < -posDeadband)
        {
            if (vel > -approachSpeed)
                buttons |= negative;
            else if (vel < -approachSpeed - 1f)
                buttons |= positive;
        }
        else
        {
            // On target on this axis: cancel residual drift so we settle instead of sliding past.
            if (vel > velDeadband)
                buttons |= negative;
            else if (vel < -velDeadband)
                buttons |= positive;
        }
    }

    /// <summary>Latches every dock pair for real (no teleport) once they are all physically alignable.</summary>
    private bool TryLatchDock(DockingConfig config)
    {
        if (config.Docks.Count == 0)
            return false;

        foreach (var (dockAUid, dockBUid, dockA, dockB) in config.Docks)
        {
            if (!_dock.CanDock((dockAUid, dockA), (dockBUid, dockB)))
                return false;
        }

        foreach (var (dockAUid, dockBUid, dockA, dockB) in config.Docks)
        {
            _dock.Dock((dockAUid, dockA), (dockBUid, dockB));
        }

        return true;
    }

    /// <summary>Resolves just the chosen dock entity, its grid and world position (no docking config needed).</summary>
    private bool TryGetDockWorld(ShuttleAutopilotComponent ap, out EntityUid dockUid, out EntityUid targetGrid, out Vector2 dockWorld)
    {
        dockUid = default;
        targetGrid = default;
        dockWorld = default;

        if (ap.TargetDock is not { } dockNet)
            return false;
        dockUid = GetEntity(dockNet);
        if (Deleted(dockUid))
            return false;
        var tg = Transform(dockUid).GridUid;
        if (tg == null)
            return false;
        targetGrid = tg.Value;
        dockWorld = _xform.GetWorldPosition(dockUid);
        return true;
    }

    /// <summary>
    /// Outward approach direction for a dock: the dock port's own facing (its local -Y, per DockingSystem
    /// geometry), flipped to point away from the target grid's centre if needed. Works for hull and
    /// side-mounted ports (cargo shuttles).
    /// </summary>
    private Vector2 DockApproachAxis(EntityUid targetGrid, EntityUid dockUid, Vector2 dockWorld)
    {
        var face = _xform.GetWorldRotation(dockUid).RotateVec(new Vector2(0f, -1f));
        var center = TryComp<MapGridComponent>(targetGrid, out var gridComp)
            ? Vector2.Transform(gridComp.LocalAABB.Center, _xform.GetWorldMatrix(targetGrid))
            : _xform.GetWorldPosition(targetGrid);

        if (Vector2.Dot(face, dockWorld - center) < 0f)
            face = -face; // ensure it points outward from the station

        return face.LengthSquared() > 0.0001f ? Vector2.Normalize(face) : new Vector2(0f, 1f);
    }
}
