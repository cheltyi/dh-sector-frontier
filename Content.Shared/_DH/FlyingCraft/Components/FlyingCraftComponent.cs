// Dark Haven - This file is licensed under AGPLv3
// Copyright (c) 2026 Dark Haven Contributors
// See AGPLv3.txt for details.

using System.Numerics;
using Content.Shared.Actions;
using Robust.Shared.GameStates;
using Robust.Shared.Maths;
using Robust.Shared.Prototypes;

namespace Content.Shared._DH.FlyingCraft.Components;

/// <summary>
/// Marks an entity as a Dark Haven flying craft (a single-seat piloted "mech-style" combat flyer, built on the
/// mech entity stack — see flying-craft-feature-design memory). Holds the class/tier metadata that the rest of
/// the _DH.FlyingCraft systems and the purchase/upgrade flow read. Stats themselves live on the reused
/// components (MechComponent integrity, MovementSpeedModifier speed, FlyingCraftFuelComponent fuel, etc.) and
/// are authored per (class, tier) prototype.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class FlyingCraftComponent : Component
{
    /// <summary>The combat role of this craft.</summary>
    [DataField, AutoNetworkedField]
    public FlyingCraftClass Class = FlyingCraftClass.Fighter;

    /// <summary>Tier 1..5 (I Standard .. V Experimental). Set at spawn and raised by upgrade boards.</summary>
    [DataField, AutoNetworkedField]
    public int Tier = 1;

    /// <summary>Max tier this craft can be upgraded to via boards.</summary>
    [DataField]
    public int MaxTier = 5;

    /// <summary>Whether this craft can perform the single-entity FTL jump. Auto-set by tier (>= FtlTier) on apply.</summary>
    [DataField, AutoNetworkedField]
    public bool FtlCapable;

    /// <summary>Tier at and above which the craft gains FTL/BSS (the jump component is added on tier apply).</summary>
    [DataField]
    public int FtlTier = 3;

    /// <summary>Civilian variant: unarmed; can be weaponised into a combat class with a weapon board.</summary>
    [DataField, AutoNetworkedField]
    public bool Civilian;

    /// <summary>For civilians: true = heavy/hauler (big storage) variant, false = fast/runner variant. Gates weapon boards.</summary>
    [DataField, AutoNetworkedField]
    public bool CivilianHeavy;

    // --- Tier scaling (applied by FlyingCraftTierSystem.ApplyTier from these per-class tables) ---

    /// <summary>Top linear speed per tier (index 0 = tier 1 .. index 4 = tier 5). MaxLinearSpeed is set from this.</summary>
    [DataField]
    public List<float> TierSpeeds = new() { 6f, 8f, 12f, 15f, 18f };

    /// <summary>Tier-1 hull durability (destruction threshold). Tier N = BaseDurability * DurabilityPerTier^(N-1).</summary>
    [DataField]
    public float BaseDurability = 500f;

    /// <summary>Durability multiplier applied per tier step (×1.3 per the design).</summary>
    [DataField]
    public float DurabilityPerTier = 1.3f;

    // --- Hybrid flight model (grid-like physics on a single Dynamic entity, NOT a grid) ---
    // The craft is an entity that can rest ON a grid (hangar) but flies with real thrust/inertia in space.
    // Acceleration is force/mass (keep thrust:mass ~constant across classes for "equal accel"); top speed scales
    // per class/tier via MaxLinearSpeed. Rotation uses real angular momentum (ApplyTorque) capped by
    // MaxAngularSpeed. Passive coast/decay comes from the body's linear/angular damping (set in the prototype).

    /// <summary>
    /// Legacy force value — UNUSED by the flight controller (kept so existing prototypes still parse). The controller
    /// now drives velocity directly (mass-independent, closed-loop) using MaxLinearSpeed + AccelTime.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float LinearThrust = 360f;

    /// <summary>Top linear speed (t/s). Per tier: I 6, II 8, III 12, IV 15, V 18 (top tier 18 t/s).</summary>
    [DataField, AutoNetworkedField]
    public float MaxLinearSpeed = 12f;

    /// <summary>Seconds of full thrust to ramp from 0 to MaxLinearSpeed (acceleration = MaxLinearSpeed / AccelTime).</summary>
    [DataField, AutoNetworkedField]
    public float AccelTime = 10f;

    /// <summary>Brake deceleration is this multiple of the thrust acceleration (Space stops faster than W ramps).</summary>
    [DataField, AutoNetworkedField]
    public float BrakeStrength = 3f;

    /// <summary>Sideways strafe is this fraction of the forward thrust/top-speed (A/D = half-speed strafe).</summary>
    [DataField, AutoNetworkedField]
    public float StrafeMultiplier = 0.5f;

    /// <summary>Legacy torque value — UNUSED (the controller now drives angular velocity directly via MaxAngularSpeed).</summary>
    [DataField, AutoNetworkedField]
    public float AngularThrust = 10500f;

    /// <summary>Runtime top turn rate (rad/s); set by ApplyTier = MaxAngularSpeedBase × TurnTierMults[tier-1].</summary>
    [DataField, AutoNetworkedField]
    public float MaxAngularSpeed = 12.25f;

    /// <summary>Tier-5 turn rate (rad/s). Lower tiers turn slower via TurnTierMults.</summary>
    [DataField]
    public float MaxAngularSpeedBase = 12.25f;

    /// <summary>Per-tier turn-rate multipliers (index 0 = tier 1). Lower tiers turn slower.</summary>
    [DataField]
    public List<float> TurnTierMults = new() { 0.5f, 0.65f, 0.78f, 0.9f, 1.0f };

    /// <summary>The "leave craft" action granted to the pilot while inside.</summary>
    [DataField]
    public EntProtoId ExitAction = "ActionFlyingCraftExit";

    /// <summary>Tracked granted exit action entity (removed when the pilot leaves).</summary>
    [DataField]
    public EntityUid? ExitActionEntity;

    // --- Combat mode (cursor-aim + held-LMB fire) ---
    // Combat mode is the pilot's OWN standard CombatModeComponent.IsInCombatMode (the vanilla red toggle): while it
    // is on, the craft auto-rotates its nose toward the cursor and the pilot fires the active weapon by holding LMB.

    /// <summary>
    /// Desired body rotation (nose toward the cursor) relayed from the pilot's client while in combat mode. Server
    /// runtime only — the flight controller turns toward it with capped torque. Null = no aim relayed yet.
    /// </summary>
    [ViewVariables]
    public Angle? CursorGoalRotation;

    /// <summary>Seconds to climb into the cockpit (entering; interrupted if the pilot moves).</summary>
    [DataField, AutoNetworkedField]
    public float EntryDelay = 3f;

    /// <summary>Seconds to climb out of the cockpit (exiting; interrupted if the craft moves).</summary>
    [DataField, AutoNetworkedField]
    public float ExitDelay = 3f;

    /// <summary>The craft must be moving no faster than this (t/s) to begin climbing out.</summary>
    [DataField, AutoNetworkedField]
    public float ExitMaxSpeed = 0.5f;

    /// <summary>
    /// Whether the pilot had <c>CanEscapeInventoryComponent</c> stripped on entry. The craft's cargo Storage makes
    /// the pilot container "escapable", which would spam "you try to break free" and auto-eject on every move;
    /// we strip it on entry and restore it here on exit.
    /// </summary>
    [ViewVariables]
    public bool PilotHadEscape;

    /// <summary>Runtime: the pilot is holding the brake; the controller bleeds linear velocity to a stop.</summary>
    [ViewVariables]
    public bool Braking;

    /// <summary>Runtime: the pilot's manual rotate input (Q/E), -1/0/+1. The manual turn outside combat mode.</summary>
    [ViewVariables]
    public int ManualTurn;

    // --- Scout: wider view (camera zoom-out + PVS range), applied to the pilot on entry, restored on exit. ---

    /// <summary>How far a Scout pilot zooms out (cap on the view; Vector2.One = normal).</summary>
    [DataField]
    public Vector2 ScoutZoom = new(1.75f, 1.75f);

    /// <summary>Scout PVS range multiplier so distant entities aren't culled while zoomed out.</summary>
    [DataField]
    public float ScoutPvsScale = 1.75f;

    /// <summary>Pilot's eye zoom/PVS before a Scout widened them; restored on exit (server runtime only).</summary>
    [ViewVariables] public Vector2? PilotPrevMaxZoom;

    [ViewVariables] public Vector2? PilotPrevTargetZoom;

    [ViewVariables] public float? PilotPrevPvsScale;
}

/// <summary>Raised on the craft (relayed from the pilot's action) to eject the pilot.</summary>
public sealed partial class FlyingCraftExitActionEvent : InstantActionEvent
{
}
