// Dark Haven - This file is licensed under AGPLv3
// Copyright (c) 2026 Dark Haven Contributors
// See AGPLv3.txt for details.

using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._DH.FlyingCraft.Components;

/// <summary>
/// A flying-craft purchase console: sells a catalog of craft/board prototypes for bank money. Craft consoles spawn
/// the purchase on a chosen nearby map spawner (<see cref="SpawnerKind"/> within <see cref="SpawnRange"/> tiles);
/// the board console (SpawnerKind None) spawns the board at the console.
/// </summary>
[RegisterComponent]
public sealed partial class FlyingCraftPurchaseConsoleComponent : Component
{
    [DataField]
    public List<FlyingCraftPurchaseEntry> Catalog = new();

    [DataField]
    public FlyingCraftSpawnerKind SpawnerKind = FlyingCraftSpawnerKind.None;

    [DataField]
    public float SpawnRange = 50f;
}

[DataDefinition]
public sealed partial class FlyingCraftPurchaseEntry
{
    [DataField(required: true)]
    public EntProtoId Proto;

    [DataField]
    public int Price;
}

/// <summary>Map marker: a spot a civilian craft console may spawn a purchased craft onto.</summary>
[RegisterComponent]
public sealed partial class FlyingCraftCivilianSpawnerComponent : Component
{
}

/// <summary>Map marker: a spot a security craft console may spawn a purchased craft onto.</summary>
[RegisterComponent]
public sealed partial class FlyingCraftSecuritySpawnerComponent : Component
{
}
