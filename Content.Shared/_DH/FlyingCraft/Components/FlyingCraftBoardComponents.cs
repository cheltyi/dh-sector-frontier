// Dark Haven - This file is licensed under AGPLv3
// Copyright (c) 2026 Dark Haven Contributors
// See AGPLv3.txt for details.

using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._DH.FlyingCraft.Components;

/// <summary>
/// A tier-upgrade board. Inserted into a combat craft's board slot to raise it to <see cref="Tier"/> (must be exactly
/// one above the craft's current tier). Consumed on success. Tier 1 has no board (it's the base craft).
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class FlyingCraftUpgradeBoardComponent : Component
{
    /// <summary>The tier this board upgrades a craft TO (2..5).</summary>
    [DataField]
    public int Tier = 2;
}

/// <summary>
/// A weaponisation board. Inserted into a CIVILIAN craft to convert it into a combat class (copying that class's
/// stats + weapons from <see cref="TargetCraft"/>) at tier 1. <see cref="ForHeavy"/> gates which civilian it fits:
/// heavy/hauler boards (Bomber/Attacker/Fighter) vs fast/runner boards (Interceptor/Scout/Fighter). Consumed on use.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class FlyingCraftWeaponBoardComponent : Component
{
    /// <summary>The combat base-craft prototype whose class + weapons this board installs (e.g. "FlyingCraftFighter").</summary>
    [DataField(required: true)]
    public EntProtoId TargetCraft;

    /// <summary>True = fits only the heavy/hauler civilian; false = fits only the fast/runner civilian.</summary>
    [DataField]
    public bool ForHeavy;
}
