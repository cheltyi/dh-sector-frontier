// Dark Haven - This file is licensed under AGPLv3
// Copyright (c) 2026 Dark Haven Contributors
// See AGPLv3.txt for details.

using Robust.Shared.GameStates;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Shared._DH.FlyingCraft.Components;

/// <summary>
/// Limited propulsion fuel for a flying craft (characteristic #1). Drains while the craft is under thrust;
/// at empty the craft can't move (its movement speed is zeroed) until refueled. Separate from weapon energy.
/// Higher tiers carry more fuel. Refuel by sitting in a hangar refuel pad / inserting a fuel cell.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState, AutoGenerateComponentPause]
public sealed partial class FlyingCraftFuelComponent : Component
{
    /// <summary>Current fuel units.</summary>
    [DataField, AutoNetworkedField]
    public float Fuel;

    /// <summary>Maximum fuel units (scales with tier).</summary>
    [DataField, AutoNetworkedField]
    public float MaxFuel = 600f;

    /// <summary>Fuel burned per second while thrusting (moving under pilot input).</summary>
    [DataField]
    public float DrainPerSecond = 1.0f;

    /// <summary>Fuel burned per FTL jump (only relevant for FTL-capable craft).</summary>
    [DataField]
    public float JumpCost = 120f;

    /// <summary>Fuel restored per second while parked on a hangar refuel pad / in atmosphere.</summary>
    [DataField]
    public float RefuelPerSecond = 25f;

    /// <summary>Runtime: true while the tank is empty (movement is gated off). Drives a speed refresh on change.</summary>
    [ViewVariables]
    public bool OutOfFuel;

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoPausedField]
    public TimeSpan NextUpdate;

    [DataField]
    public TimeSpan UpdateRate = TimeSpan.FromSeconds(1);
}
