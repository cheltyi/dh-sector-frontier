// Dark Haven - This file is licensed under AGPLv3
// Copyright (c) 2026 Dark Haven Contributors
// See AGPLv3.txt for details.

using Robust.Shared.GameStates;

namespace Content.Shared._DH.Shuttles.Components;

/// <summary>
/// Marks an item as a shuttle autopilot upgrade board. Inserted into a shuttle console's
/// "autopilot_slot" to unlock autopilot capability. Higher <see cref="Tier"/> = more features:
/// 1 = straight-line A→B, 2 = + obstacle avoidance (size-aware pathfinding), 3 = + auto-docking.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class AutopilotUpgradeComponent : Component
{
    [DataField]
    public int Tier = 1;
}
