// Dark Haven - This file is licensed under AGPLv3
// Copyright (c) 2026 Dark Haven Contributors
// See AGPLv3.txt for details.

using Robust.Shared.GameStates;

namespace Content.Shared._DH.FlyingCraft.Components;

/// <summary>
/// Marker for a craft that has the upgrade/weaponise board slot. Exists so FlyingCraftTierSystem can subscribe its
/// board-insertion handler here instead of on <see cref="FlyingCraftComponent"/> (the engine forbids two directed
/// subscriptions for the same (component, event) pair, and FlyingCraftPilotSystem already owns the FlyingCraft +
/// EntInsertedIntoContainerMessage pair).
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class FlyingCraftBoardSlotComponent : Component
{
}
