// Dark Haven - This file is licensed under AGPLv3
// Copyright (c) 2026 Dark Haven Contributors
// See AGPLv3.txt for details.

using System.Numerics;
using Content.Shared._DH.FlyingCraft;
using Content.Shared._DH.FlyingCraft.Components;
using Content.Shared.CombatMode;
using Content.Shared.Movement.Components;
using Content.Shared.Movement.Systems;
using Robust.Shared.Containers;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;

namespace Content.Server._DH.FlyingCraft;

/// <summary>
/// Hybrid flight controller for Dark Haven flying craft. The craft is a single Dynamic-body ENTITY (not a grid):
/// it can rest on a grid (hangar deck) and does NOT show as a grid blip, but it flies with grid-like physics —
/// real linear inertia (thrust applies FORCE toward the nose, capped at a per-class top speed), real angular
/// inertia (turning applies TORQUE, capped), and coast/decay from the body's damping (set in the prototype).
/// The pilot is whoever is buckled into the craft; their movement keys drive thrust (W/S) and turn (A/D).
/// Burns fuel while thrusting in open space, refuels while parked on a grid, and crawls slowly on decks.
/// </summary>
public sealed class FlyingCraftSystem : EntitySystem
{
    [Dependency] private readonly SharedPhysicsSystem _physics = default!;
    [Dependency] private readonly SharedTransformSystem _xform = default!;
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;
    [Dependency] private readonly SharedContainerSystem _container = default!;

    private const float MoveThreshold = 0.1f;

    /// <summary>Below this absolute heading error (rad) the combat auto-turn stops nudging (anti-dither).</summary>
    private const float AimDeadband = 0.015f;

    /// <summary>Proportional gain for the combat auto-turn (1/s): higher = snappier, lower = smoother/lazier.</summary>
    private const float TurnGain = 7f;

    /// <summary>Seconds to ramp the turn rate up to / down from MaxAngularSpeed (angular accel = MaxAngularSpeed / this).</summary>
    private const float TurnRampTime = 0.4f;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeNetworkEvent<FlyingCraftSetAimEvent>(OnSetAim);
        SubscribeNetworkEvent<FlyingCraftSetBrakingEvent>(OnSetBraking);
        SubscribeNetworkEvent<FlyingCraftSetTurnEvent>(OnSetTurn);
    }

    /// <summary>Client relay: the seated pilot holds/releases the brake (works in any mode).</summary>
    private void OnSetBraking(FlyingCraftSetBrakingEvent msg, EntitySessionEventArgs args)
    {
        if (args.SenderSession.AttachedEntity is not { } pilot
            || !TryComp<FlyingCraftPilotComponent>(pilot, out var pilotComp))
            return;

        var craft = GetEntity(msg.Craft);
        if (pilotComp.Craft != craft || !TryComp<FlyingCraftComponent>(craft, out var craftComp))
            return;

        craftComp.Braking = msg.Braking;
    }

    /// <summary>Client relay: the seated pilot's manual rotate input (Q/E). Applied outside combat mode.</summary>
    private void OnSetTurn(FlyingCraftSetTurnEvent msg, EntitySessionEventArgs args)
    {
        if (args.SenderSession.AttachedEntity is not { } pilot
            || !TryComp<FlyingCraftPilotComponent>(pilot, out var pilotComp))
            return;

        var craft = GetEntity(msg.Craft);
        if (pilotComp.Craft != craft || !TryComp<FlyingCraftComponent>(craft, out var craftComp))
            return;

        craftComp.ManualTurn = Math.Clamp(msg.Turn, -1, 1);
    }

    /// <summary>Client relay: the seated pilot's cursor-aim goal (desired body rotation) while in combat mode.</summary>
    private void OnSetAim(FlyingCraftSetAimEvent msg, EntitySessionEventArgs args)
    {
        // Combat mode is the pilot's OWN vanilla combat mode.
        if (args.SenderSession.AttachedEntity is not { } pilot
            || !TryComp<FlyingCraftPilotComponent>(pilot, out var pilotComp)
            || !TryComp<CombatModeComponent>(pilot, out var cm) || !cm.IsInCombatMode)
            return;

        var craft = GetEntity(msg.Craft);
        if (pilotComp.Craft != craft || !TryComp<FlyingCraftComponent>(craft, out var craftComp))
            return;

        craftComp.CursorGoalRotation = msg.Rotation;
    }

    /// <summary>Sets fuel, clamps, and flags empty (the controller then refuses thrust).</summary>
    public void SetFuel(Entity<FlyingCraftFuelComponent> ent, float value)
    {
        ent.Comp.Fuel = Math.Clamp(value, 0f, ent.Comp.MaxFuel);
        ent.Comp.OutOfFuel = ent.Comp.Fuel <= 0f;
        Dirty(ent);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<FlyingCraftComponent, PhysicsComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var craft, out var body, out var xform))
        {
            // Flight is disabled while the technical (repair) panel is open — no thrust/turn (it coasts).
            if (TryComp<FlyingCraftRepairComponent>(uid, out var repair) && repair.PanelOpen)
            {
                _appearance.SetData(uid, FlyingCraftVisuals.Flying, body.LinearVelocity.Length() > MoveThreshold);
                continue;
            }

            // Resolve the pilot: whoever is seated inside the craft's pilot container.
            EntityUid? pilot = null;
            if (_container.TryGetContainer(uid, FlyingCraftPilotSystem.PilotContainerId, out var pilotContainer)
                && pilotContainer is ContainerSlot { ContainedEntity: { } seated })
            {
                pilot = seated;
            }

            var forward = 0f;
            var strafe = 0f;
            if (pilot != null && TryComp<InputMoverComponent>(pilot.Value, out var mover))
            {
                var b = mover.HeldMoveButtons;
                // W/S = forward/reverse along the nose (+Y). A/D = strafe sideways. (Nose = sprite +Y; the
                // placeholder RSI is vertically flipped so the nose points the same way the craft thrusts.)
                if ((b & MoveButtons.Up) != 0) forward += 1f;
                if ((b & MoveButtons.Down) != 0) forward -= 1f;
                if ((b & MoveButtons.Right) != 0) strafe += 1f; // D = strafe right (+X local)
                if ((b & MoveButtons.Left) != 0) strafe -= 1f;
            }

            // Q/E manual rotate (relayed from the client). Q = +1 (left/CCW), E = -1 (right/CW). Used outside combat.
            var turn = (float) craft.ManualTurn;

            // Combat mode = the pilot's own (vanilla) combat mode: the craft auto-turns its nose toward the cursor.
            var inCombat = pilot != null
                           && TryComp<CombatModeComponent>(pilot.Value, out var combat)
                           && combat.IsInCombatMode;

            // Full thrust everywhere. (No on-grid slowdown: in 2D a craft flying OVER a grid is parented to it, so
            // an on-grid penalty would also cripple flight near every station — it must crawl only when truly
            // resting, which can't be told apart in 2D.) onGrid is still used for hangar refuelling below.
            var onGrid = xform.GridUid != null;
            const float thrustMult = 1f;
            var maxSpeed = craft.MaxLinearSpeed * thrustMult;

            // Fuel: thrusting in open space burns it; parked on a grid refuels.
            var hasFuel = true;
            if (TryComp<FlyingCraftFuelComponent>(uid, out var fuel))
            {
                if (onGrid)
                {
                    if (fuel.Fuel < fuel.MaxFuel)
                        SetFuel((uid, fuel), fuel.Fuel + fuel.RefuelPerSecond * frameTime);
                }
                else if ((forward != 0f || strafe != 0f) && fuel.Fuel > 0f)
                {
                    SetFuel((uid, fuel), fuel.Fuel - fuel.DrainPerSecond * frameTime);
                }

                hasFuel = !fuel.OutOfFuel;
            }

            // Linear motion is driven DIRECTLY via velocity (mass-independent, closed-loop — like the turn). A fixed
            // ApplyForce is open-loop and is crushed by the craft's mass; velocity control gives an exact ramp.
            // accel = MaxLinearSpeed / AccelTime (t/s^2) -> reaches top speed in AccelTime seconds.
            var accel = craft.AccelTime > 0f ? maxSpeed / craft.AccelTime : maxSpeed;

            if (craft.Braking)
            {
                // Brake (Space): bleed velocity straight to zero, faster than thrust ramps. Fuel-free reaction control.
                var vel = body.LinearVelocity;
                var len = vel.Length();
                if (len > 0f)
                {
                    var newLen = MathF.Max(0f, len - accel * craft.BrakeStrength * frameTime);
                    _physics.SetLinearVelocity(uid, newLen <= 0f ? Vector2.Zero : vel * (newLen / len), body: body);
                }
            }
            else if ((forward != 0f || strafe != 0f) && hasFuel)
            {
                // Decompose velocity onto the nose (forward) + right (strafe) axes, ramp each toward its target speed,
                // recompose. Forward caps at maxSpeed; strafe caps at maxSpeed * StrafeMultiplier (half).
                var worldRot = _xform.GetWorldRotation(uid);
                var nose = worldRot.RotateVec(Vector2.UnitY);
                var right = worldRot.RotateVec(Vector2.UnitX);
                var vel = body.LinearVelocity;
                var noseComp = Vector2.Dot(vel, nose);
                var rightComp = Vector2.Dot(vel, right);

                if (forward != 0f)
                    noseComp = MoveToward(noseComp, forward * maxSpeed, accel * frameTime);
                if (strafe != 0f)
                {
                    var strafeAccel = accel * craft.StrafeMultiplier;
                    rightComp = MoveToward(rightComp, strafe * maxSpeed * craft.StrafeMultiplier, strafeAccel * frameTime);
                }

                _physics.SetLinearVelocity(uid, nose * noseComp + right * rightComp, body: body);
            }

            // Turn is also driven DIRECTLY via angular velocity (TileFrictionModifier zeroes physics angular damping,
            // so settle is handled here). targetOmega: combat = proportional toward the cursor; manual = Q/E turn
            // rate; otherwise 0 (bleed residual spin to a stop). Ramped at angAccel so it eases in/out.
            var angAccel = craft.MaxAngularSpeed / TurnRampTime;
            var targetOmega = 0f;

            if (inCombat && craft.CursorGoalRotation is { } goal)
            {
                var err = (float) Angle.ShortestDistance(_xform.GetWorldRotation(uid), goal).Theta; // +CCW
                if (MathF.Abs(err) > AimDeadband)
                    targetOmega = Math.Clamp(err * TurnGain, -craft.MaxAngularSpeed, craft.MaxAngularSpeed);
            }
            else
            {
                targetOmega = turn * craft.MaxAngularSpeed; // manual Q/E (turn = ManualTurn -1/0/+1), 0 = settle
            }

            var newOmega = MoveToward(body.AngularVelocity, targetOmega, angAccel * frameTime);
            _physics.SetAngularVelocity(uid, newOmega, body: body);

            // Flight animation flag (plays while actually moving, incl. coasting).
            _appearance.SetData(uid, FlyingCraftVisuals.Flying, body.LinearVelocity.Length() > MoveThreshold);
        }
    }

    /// <summary>Moves <paramref name="current"/> toward <paramref name="target"/> by at most <paramref name="maxDelta"/>.</summary>
    private static float MoveToward(float current, float target, float maxDelta)
    {
        var diff = target - current;
        if (MathF.Abs(diff) <= maxDelta)
            return target;

        return current + MathF.Sign(diff) * maxDelta;
    }
}
