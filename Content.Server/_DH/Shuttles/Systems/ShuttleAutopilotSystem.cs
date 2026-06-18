// Dark Haven - This file is licensed under AGPLv3
// Copyright (c) 2026 Dark Haven Contributors
// See AGPLv3.txt for details.

using System.Numerics;
using Content.Server.Shuttles;
using Content.Server.Shuttles.Components;
using Content.Server.Shuttles.Events;
using Content.Server.Shuttles.Systems;
using Content.Shared._DH.Shuttles.Components;
using Content.Shared._DH.Shuttles.Events;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.Movement.Components;
using Content.Shared.Movement.Systems;
using Content.Shared.Popups;
using Content.Shared.Shuttles.Components;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Prototypes;

namespace Content.Server._DH.Shuttles.Systems;

/// <summary>
/// Drives a shuttle autonomously toward a player-chosen destination (a point or a docking port picked
/// on the radar). Capability is gated by an autopilot board inserted into the console:
/// tier 1 = straight line, tier 2 = obstacle-avoiding A* (size-aware), tier 3 = + auto-docking.
///
/// Movement reuses the engine's existing "virtual pilot" mechanism: a hidden pilot mob is attached to
/// a console on the grid and we write its <see cref="PilotComponent.HeldButtons"/> each control tick;
/// the base <c>MoverController</c> turns that into real thruster forces. See the AI shuttle brain
/// (<c>Content.Server/_Lua/AiShuttle</c>) for the same approach.
/// </summary>
public sealed partial class ShuttleAutopilotSystem : EntitySystem
{
    [Dependency] private readonly ShuttleConsoleSystem _console = default!;
    [Dependency] private readonly ShuttleSystem _shuttle = default!;
    [Dependency] private readonly DockingSystem _dock = default!;
    [Dependency] private readonly SharedTransformSystem _xform = default!;
    [Dependency] private readonly SharedPhysicsSystem _physics = default!;
    [Dependency] private readonly SharedMapSystem _map = default!;
    [Dependency] private readonly IMapManager _mapManager = default!;
    [Dependency] private readonly ItemSlotsSystem _itemSlots = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;

    /// <summary>Item slot id on the shuttle console that accepts an autopilot board.</summary>
    public const string AutopilotSlotId = "autopilot_slot";

    /// <summary>
    /// Cached docking configs per shuttle grid. Computed while the ship is still far away; reused during the
    /// close approach because recomputing once the hull overlaps the docked area makes the fit check treat
    /// the ship's own grid as an obstacle and return null (which used to abort docking mid-approach).
    /// </summary>
    private readonly Dictionary<EntityUid, DockingConfig> _dockConfigs = new();

    // Control loop runs at a fixed rate (like the AI shuttle brain), independent of frame rate.
    private const float ControlInterval = 1f / 9f;

    // Yaw controller.
    private const float YawDeadzoneDeg = 2.0f;
    private const float YawKAvPerRad = 2.2f;
    private const float YawAvEpsilon = 0.10f;
    private const float YawCoastAv = 0.30f;        // within the deadzone, let residual spin under this carry the nose home
    private const float MaxTurnRate = ShuttleComponent.MaxAngularVelocity; // full hull turn rate

    // Flight model: turn the nose toward the target, then fly forward (rear thrusters). The engine caps the
    // speed and cancels sideways drift for us, so this stays a thin layer over the working base.
    private const float ThrustAlignDot = 0.8f;     // start thrusting once within ~37° of the target (turn-then-burn)
    private const float BrakeLead = 2.0f;          // brake this many seconds of travel before the final goal

    // Arrival.
    private const float ArriveSpeed = 1.0f;

    // Docking maneuver (tier 3).
    private const float DockStandoffDistance = 14f;  // A* aims here, offset out along the dock approach axis
    private const float DockManeuverRange = 18f;      // within this of the docked pose, hand off to DriveDock
    private const float DockApproachSpeed = 2.5f;     // slow, deliberate slide-in speed (per axis)
    private const float DockPosDeadband = 0.2f;       // positional tolerance per axis during the slide-in
    private const float DockVelDeadband = 0.3f;       // residual-velocity tolerance during the slide-in
    private const float DockAlignGateDeg = 12f;       // only translate once rotation is within this of docked
    private const float DockTimeout = 12f;            // give up the soft approach and snap-dock after this

    // Re-planning (tier >= 2). Periodic is a safety refresh; the stuck timer drives real re-routing.
    private const float StuckReplanSeconds = 3.0f;
    private const float PeriodicReplanSeconds = 12.0f;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ShuttleConsoleComponent, AutopilotSetTargetMessage>(OnSetTarget);
        SubscribeLocalEvent<ShuttleConsoleComponent, AutopilotToggleMessage>(OnToggle);
        SubscribeLocalEvent<ShuttleConsoleComponent, AutopilotBoardChangedEvent>(OnBoardChanged);
        SubscribeLocalEvent<ShuttleAutopilotComponent, FTLCompletedEvent>(OnFtlCompleted);
    }

    // After an FTL jump the ship is on a different map, so the sublight route is stale — stop cleanly.
    private void OnFtlCompleted(EntityUid grid, ShuttleAutopilotComponent ap, ref FTLCompletedEvent args)
    {
        if (ap.Enabled)
            Disengage(grid, ap, "autopilot-popup-ftl-interrupted");
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<ShuttleAutopilotComponent, TransformComponent, PhysicsComponent>();
        while (query.MoveNext(out var uid, out var ap, out var xform, out var body))
        {
            if (!ap.Enabled)
                continue;

            // Don't drive/replan/dock while in FTL: the engine ignores pilot input then and an FTLDock
            // mid-jump would corrupt state. Reset accumulators so we resume cleanly on arrival.
            if (HasComp<FTLComponent>(uid))
            {
                ap.ControlAccum = 0f;
                ap.StuckTimer = 0f;
                continue;
            }

            // Belt-and-suspenders: never run above the currently installed capability.
            if (ap.AvailableTier > 0 && ap.Tier > ap.AvailableTier)
                ap.Tier = ap.AvailableTier;

            ap.ControlAccum += frameTime;
            if (ap.ControlAccum < ControlInterval)
                continue;

            var dt = ap.ControlAccum;
            ap.ControlAccum = 0f;
            Drive(uid, ap, xform, body, dt);
        }
    }

    private void OnSetTarget(EntityUid consoleUid, ShuttleConsoleComponent comp, AutopilotSetTargetMessage args)
    {
        if (!TryResolveGrid(consoleUid, out var grid))
            return;

        var tier = ComputeAvailableTier(grid);
        if (tier <= 0)
        {
            Notify(args.Actor, "autopilot-popup-no-board");
            return;
        }

        var ap = EnsureComp<ShuttleAutopilotComponent>(grid);
        ap.AvailableTier = tier;
        ap.Tier = tier;
        // Anchor the destination to the map, not the moving ship grid, or the route would drift on re-plan.
        ap.Target = MapAnchor(args.Coordinates);
        ap.TargetDock = tier >= 3 ? args.Dock : null;
        ap.WaypointIndex = 0;
        _dockConfigs.Remove(grid); // a new target invalidates any cached docking config

        if (!TryBuildRoute(grid, ap, out var reason))
        {
            ap.Route.Clear();
            Notify(args.Actor, reason);
        }

        Dirty(grid, ap);
        _console.RefreshShuttleConsoles(grid);
    }

    private void OnToggle(EntityUid consoleUid, ShuttleConsoleComponent comp, AutopilotToggleMessage args)
    {
        if (!TryResolveGrid(consoleUid, out var grid))
            return;

        if (!args.Enabled)
        {
            if (TryComp<ShuttleAutopilotComponent>(grid, out var existing))
                Disengage(grid, existing);
            return;
        }

        var tier = ComputeAvailableTier(grid);
        if (tier <= 0)
        {
            Notify(args.Actor, "autopilot-popup-no-board");
            return;
        }

        var ap = EnsureComp<ShuttleAutopilotComponent>(grid);
        ap.AvailableTier = tier;
        ap.Tier = tier;

        if (ap.Target == null && ap.TargetDock == null)
        {
            Notify(args.Actor, "autopilot-popup-no-route");
            return;
        }

        if (ap.Route.Count == 0 && !TryBuildRoute(grid, ap, out var reason))
        {
            Notify(args.Actor, reason);
            return;
        }

        if (ActiveManualPilot(grid, ap))
        {
            Notify(args.Actor, "autopilot-popup-manual-control");
            return;
        }

        if (!EnsurePilot(grid, ap))
        {
            Notify(args.Actor, "autopilot-popup-no-console");
            return;
        }

        ap.Enabled = true;
        ap.WaypointIndex = 0;
        ap.StuckTimer = 0f;
        ap.ReplanTimer = 0f;
        ap.LastWaypointDistance = float.MaxValue;
        Dirty(grid, ap);
        _console.RefreshShuttleConsoles(grid);
    }

    private void OnBoardChanged(EntityUid consoleUid, ShuttleConsoleComponent comp, ref AutopilotBoardChangedEvent args)
    {
        // Resolve the controlled shuttle grid (handles boards placed in remote/drone consoles too).
        if (!TryResolveGrid(consoleUid, out var grid))
            return;

        var tier = ComputeAvailableTier(grid);
        if (tier <= 0)
        {
            // No boards left anywhere: clear the gating value (and stop if it was running).
            if (TryComp<ShuttleAutopilotComponent>(grid, out var ap0))
            {
                if (ap0.Enabled)
                    Disengage(grid, ap0);
                ap0.AvailableTier = 0;
                Dirty(grid, ap0);
            }
            return;
        }

        var ap = EnsureComp<ShuttleAutopilotComponent>(grid);
        ap.AvailableTier = tier;

        // A board was pulled mid-flight: clamp the active command to the remaining capability.
        if (ap.Enabled && tier < ap.Tier)
        {
            if (ap.TargetDock != null && tier < 3)
                ap.TargetDock = null;
            ap.Tier = tier;

            if ((ap.Target == null && ap.TargetDock == null) || !TryBuildRoute(grid, ap, out _))
            {
                Disengage(grid, ap);
                return;
            }
        }

        Dirty(grid, ap);
        _console.RefreshShuttleConsoles(grid);
    }

    /// <summary>
    /// One control step: advance along the route, steer toward the active waypoint, brake on arrival,
    /// dock when applicable, and re-plan if stuck (tier >= 2).
    /// </summary>
    private void Drive(EntityUid grid, ShuttleAutopilotComponent ap, TransformComponent xform, PhysicsComponent body, float dt)
    {
        if (!EnsurePilot(grid, ap) || !TryComp<PilotComponent>(ap.PilotEntity, out var pilot))
        {
            Disengage(grid, ap);
            return;
        }

        if (ap.Route.Count == 0)
        {
            Disengage(grid, ap);
            return;
        }

        // Yield to a human who grabs the helm and actively flies.
        if (ActiveManualPilot(grid, ap))
        {
            Disengage(grid, ap, "autopilot-popup-manual-control");
            return;
        }

        var shipPos = GetShipCenter(grid, xform);
        var speed = body.LinearVelocity.Length();

        if (ap.WaypointIndex >= ap.Route.Count)
            ap.WaypointIndex = ap.Route.Count - 1;

        var isLast = ap.WaypointIndex == ap.Route.Count - 1;
        var waypoint = ap.Route[ap.WaypointIndex];
        var distWp = (waypoint - shipPos).Length();

        // Track the closest we've come to the current waypoint this leg.
        var prevBest = ap.LastWaypointDistance;
        if (distWp < ap.LastWaypointDistance)
            ap.LastWaypointDistance = distWp;

        // Advance to the next waypoint when we REACH it, or when we clearly got near it and are now moving
        // away again (a genuine overshoot). The old perpendicular-plane test fired instantly for a waypoint
        // that started close to the ship, making it skip the checkpoint and ram the obstacle.
        if (!isLast)
        {
            var reached = distWp <= ap.WaypointTolerance;
            // Allow a miss-distance scaled by how far inertia can carry us through a turn (turn radius
            // ≈ speed / turn-rate), so a fast ship that swings wide still counts the waypoint instead of looping.
            var missAllow = MathF.Max(ap.WaypointTolerance * 2f, speed / MaxTurnRate + ap.WaypointTolerance);
            var overshot = ap.LastWaypointDistance <= missAllow && distWp > ap.LastWaypointDistance + 3f;
            if (reached || overshot)
            {
                ap.WaypointIndex++;
                ap.StuckTimer = 0f;
                ap.LastWaypointDistance = float.MaxValue;
                return;
            }
        }

        var finalGoal = ap.Route[^1];

        if (ap.TargetDock != null)
        {
            if (!TryGetDockWorld(ap, out _, out _, out var dockWorld))
            {
                Disengage(grid, ap, "autopilot-popup-fail-nodock");
                return;
            }

            // Near the dock, hand off to the precision maneuver (rotate to the docked orientation, slide in —
            // laterally for side ports — and latch). Measure from the ship CENTRE (same reference the route
            // and steering use) so the handoff fires reliably once we reach the standoff.
            var distToDock = (dockWorld - shipPos).Length();
            if (distToDock <= DockManeuverRange)
            {
                if (!TryResolveDock(grid, ap, recompute: false, out var config, out _, out var dockedOrigin, out var dockedAngle))
                {
                    // Close but no valid docking config — the ship genuinely doesn't fit at this port.
                    Disengage(grid, ap, "autopilot-popup-fail-nofit");
                    return;
                }

                DriveDock(grid, ap, pilot, body, xform, config, dockedOrigin, dockedAngle, dt);
                return;
            }

            ap.DockTimer = 0f; // still cruising toward the standoff
        }
        else
        {
            // Coordinate arrival.
            var distFinal = (finalGoal - shipPos).Length();
            if (isLast && distFinal <= ap.ArriveTolerance && speed < ArriveSpeed)
            {
                Disengage(grid, ap, "autopilot-popup-arrived");
                return;
            }
        }

        DriveTowards(grid, ap, pilot, body, shipPos, waypoint, isLast);

        if (ap.Tier < 2)
            return;

        // Stuck / periodic re-plan for the smart tiers. (Best distance is already updated above.)
        ap.ReplanTimer += dt;
        if (distWp < prevBest - 0.5f)
            ap.StuckTimer = 0f;
        else
            ap.StuckTimer += dt;

        if (ap.StuckTimer < StuckReplanSeconds && ap.ReplanTimer < PeriodicReplanSeconds)
            return;

        ap.ReplanTimer = 0f;
        ap.StuckTimer = 0f;
        ap.LastWaypointDistance = float.MaxValue;
        if (TryBuildRoute(grid, ap, out _))
        {
            Dirty(grid, ap);
            _console.RefreshShuttleConsoles(grid);
        }
    }

    /// <summary>
    /// Steering: turn the nose toward the target, then fly forward (rear thrusters); brake on arrival. The
    /// engine itself caps the speed and steers velocity toward the nose (cancelling drift), so this is a thin
    /// layer over the working base — no extra speed/strafe controllers on top of it.
    /// </summary>
    private void DriveTowards(EntityUid grid, ShuttleAutopilotComponent ap, PilotComponent pilot, PhysicsComponent body,
        Vector2 shipPos, Vector2 target, bool isFinal)
    {
        var gridRot = _xform.GetWorldRotation(grid);
        var consoleRot = Angle.Zero;
        if (pilot.Console is { } con && TryComp<TransformComponent>(con, out var conXform))
            consoleRot = conXform.LocalRotation;
        var forward = (gridRot + consoleRot + Angle.FromDegrees(ap.ForwardAngleOffset)).RotateVec(Vector2.UnitY);

        var delta = target - shipPos;
        var dist = delta.Length();
        var dir = dist > 1e-4f ? delta / dist : forward;
        var speed = body.LinearVelocity.Length();

        // Brake to a stop on the final approach.
        if (isFinal && dist <= MathF.Max(ap.ArriveTolerance, speed * BrakeLead))
        {
            SetButtons(pilot, ShuttleButtons.Brake);
            return;
        }

        var buttons = ShuttleButtons.None;
        AddYaw(ref buttons, forward, dir, body.AngularVelocity);

        // Turn-then-burn: only thrust once roughly facing the target, so we don't chase a swinging bearing.
        if (Vector2.Dot(forward, dir) > ThrustAlignDot)
            buttons |= ShuttleButtons.StrafeUp;

        SetButtons(pilot, buttons);
    }

    /// <summary>Gentle yaw toward <paramref name="faceDir"/> with a capped turn rate and a coast band.</summary>
    private void AddYaw(ref ShuttleButtons buttons, Vector2 forward, Vector2 faceDir, float av)
    {
        var cross = forward.X * faceDir.Y - forward.Y * faceDir.X;
        var dot = Vector2.Dot(forward, faceDir);
        var yawError = MathF.Atan2(cross, dot);
        var deadzone = YawDeadzoneDeg * (MathF.PI / 180f);

        if (MathF.Abs(yawError) > deadzone)
        {
            var desiredAv = Math.Clamp(yawError * YawKAvPerRad, -MaxTurnRate, MaxTurnRate);
            var avErr = desiredAv - av;
            if (avErr > YawAvEpsilon)
                buttons |= ShuttleButtons.RotateLeft;
            else if (avErr < -YawAvEpsilon)
                buttons |= ShuttleButtons.RotateRight;
        }
        else
        {
            // Coast: let small residual spin carry the nose home; only damp a clearly-too-fast spin.
            if (av > YawCoastAv)
                buttons |= ShuttleButtons.RotateRight;
            else if (av < -YawCoastAv)
                buttons |= ShuttleButtons.RotateLeft;
        }
    }

    private void SetButtons(PilotComponent pilot, ShuttleButtons buttons)
    {
        if (pilot.HeldButtons != buttons)
            pilot.HeldButtons = buttons;
    }

    /// <summary>Highest autopilot tier installed across all consoles on the grid (0 = none).</summary>
    private int ComputeAvailableTier(EntityUid grid)
    {
        var max = 0;
        var query = EntityQueryEnumerator<ShuttleConsoleComponent, TransformComponent>();
        while (query.MoveNext(out var consoleUid, out _, out var cxform))
        {
            // Count boards on consoles physically on this grid AND on remote/drone consoles that control it.
            if (cxform.GridUid != grid && !(TryResolveGrid(consoleUid, out var resolved) && resolved == grid))
                continue;

            var item = _itemSlots.GetItemOrNull(consoleUid, AutopilotSlotId);
            if (item != null && TryComp<AutopilotUpgradeComponent>(item.Value, out var board))
                max = Math.Max(max, board.Tier);
        }

        return max;
    }

    private bool EnsurePilot(EntityUid grid, ShuttleAutopilotComponent ap)
    {
        // Reuse the existing virtual pilot only if its console is still on THIS grid (handles grid splits/relocation).
        if (ap.PilotEntity is { } existing && !Deleted(existing)
            && TryComp<PilotComponent>(existing, out var pc) && pc.Console is { } existingConsole && !Deleted(existingConsole)
            && TryComp<TransformComponent>(existingConsole, out var existingConsoleXform) && existingConsoleXform.GridUid == grid)
            return true;

        EntityUid? consoleUid = null;
        var query = EntityQueryEnumerator<ShuttleConsoleComponent, TransformComponent>();
        while (query.MoveNext(out var cUid, out _, out var cxform))
        {
            if (cxform.GridUid == grid)
            {
                consoleUid = cUid;
                break;
            }
        }

        if (consoleUid == null)
            return false;

        if (ap.PilotEntity == null || Deleted(ap.PilotEntity.Value))
        {
            var proto = _proto.HasIndex<EntityPrototype>("MobShuttleAIPilotNF") ? "MobShuttleAIPilotNF" : "MobShuttleAIPilot";
            ap.PilotEntity = Spawn(proto, Transform(consoleUid.Value).Coordinates);
        }

        var pilot = ap.PilotEntity.Value;
        EnsureComp<InputMoverComponent>(pilot);
        EnsureComp<PilotComponent>(pilot);
        var pilotComp = Comp<PilotComponent>(pilot);
        if (pilotComp.Console != consoleUid)
            _console.AddPilot(consoleUid.Value, pilot, Comp<ShuttleConsoleComponent>(consoleUid.Value));

        return true;
    }

    private void TeardownPilot(ShuttleAutopilotComponent ap)
    {
        if (ap.PilotEntity is { } pilot && !Deleted(pilot))
        {
            _console.RemovePilot(pilot);
            QueueDel(pilot);
        }

        ap.PilotEntity = null;
    }

    private void Disengage(EntityUid grid, ShuttleAutopilotComponent ap, string? popup = null)
    {
        ap.Enabled = false;
        ap.Route.Clear();        // clear the drawn route/marker on all clients
        ap.WaypointIndex = 0;
        ap.Braking = false;
        ap.DockTimer = 0f;
        _dockConfigs.Remove(grid);
        TeardownPilot(ap);
        Dirty(grid, ap);
        _console.RefreshShuttleConsoles(grid);

        if (popup != null)
            PopupOnGrid(grid, popup);
    }

    /// <summary>Shows a popup at a console on the grid, so the helmsman at the console actually sees it.</summary>
    private void PopupOnGrid(EntityUid grid, string loc)
    {
        var query = EntityQueryEnumerator<ShuttleConsoleComponent, TransformComponent>();
        while (query.MoveNext(out var consoleUid, out _, out var cxform))
        {
            if (cxform.GridUid != grid)
                continue;
            _popup.PopupEntity(Loc.GetString(loc), consoleUid);
            return;
        }

        _popup.PopupCoordinates(Loc.GetString(loc), Transform(grid).Coordinates);
    }

    /// <summary>
    /// True if a human (any non-autopilot pilot) is actively flying this grid right now (has movement input
    /// held). The autopilot yields to manual control rather than averaging inputs with the helmsman.
    /// </summary>
    private bool ActiveManualPilot(EntityUid grid, ShuttleAutopilotComponent ap)
    {
        var query = EntityQueryEnumerator<ShuttleConsoleComponent, TransformComponent>();
        while (query.MoveNext(out var consoleUid, out var console, out var cxform))
        {
            if (cxform.GridUid != grid && !(TryResolveGrid(consoleUid, out var resolved) && resolved == grid))
                continue;

            foreach (var pilotUid in console.SubscribedPilots)
            {
                if (pilotUid == ap.PilotEntity)
                    continue;
                if (TryComp<PilotComponent>(pilotUid, out var pc) && pc.HeldButtons != ShuttleButtons.None)
                    return true;
            }
        }

        return false;
    }

    /// <summary>Re-anchors a (possibly grid-relative) coordinate to the map so the target doesn't drift as the ship moves.</summary>
    private NetCoordinates MapAnchor(NetCoordinates netCoords)
    {
        var mapCoords = _xform.ToMapCoordinates(GetCoordinates(netCoords));
        if (mapCoords.MapId == MapId.Nullspace)
            return netCoords;
        return GetNetCoordinates(new EntityCoordinates(_mapManager.GetMapEntityId(mapCoords.MapId), mapCoords.Position));
    }

    /// <summary>Resolves the shuttle grid commanded by a (possibly remote/drone) console.</summary>
    private bool TryResolveGrid(EntityUid consoleUid, out EntityUid grid)
    {
        grid = default;
        var actual = _console.GetDroneConsole(consoleUid) ?? consoleUid;
        var gridUid = Transform(actual).GridUid;
        if (gridUid == null)
            return false;
        grid = gridUid.Value;
        return true;
    }

    private Vector2 GetShipCenter(EntityUid grid, TransformComponent xform)
    {
        var matrix = _xform.GetWorldMatrix(xform);
        var center = TryComp<MapGridComponent>(grid, out var gridComp) ? gridComp.LocalAABB.Center : Vector2.Zero;
        return Vector2.Transform(center, matrix);
    }

    private void Notify(EntityUid actor, string loc)
    {
        _popup.PopupEntity(Loc.GetString(loc), actor, actor);
    }
}
