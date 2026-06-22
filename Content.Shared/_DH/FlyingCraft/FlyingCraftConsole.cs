// Dark Haven - This file is licensed under AGPLv3
// Copyright (c) 2026 Dark Haven Contributors
// See AGPLv3.txt for details.

using Robust.Shared.Serialization;

namespace Content.Shared._DH.FlyingCraft;

[Serializable, NetSerializable]
public enum FlyingCraftConsoleUiKey : byte
{
    Key,
}

/// <summary>Which map spawner markers a craft console spawns onto (None = the board console: spawns at the console).</summary>
public enum FlyingCraftSpawnerKind : byte
{
    None,
    Civilian,
    Security,
}

/// <summary>One buyable catalog row sent to the client (proto id, display name, price).</summary>
[Serializable, NetSerializable]
public sealed class FlyingCraftCatalogEntry
{
    public string Proto;
    public string Name;
    public int Price;

    public FlyingCraftCatalogEntry(string proto, string name, int price)
    {
        Proto = proto;
        Name = name;
        Price = price;
    }
}

/// <summary>One nearby spawn point sent to the client (the spawner entity + a display name).</summary>
[Serializable, NetSerializable]
public sealed class FlyingCraftSpawnPoint
{
    public NetEntity Spawner;
    public string Name;

    public FlyingCraftSpawnPoint(NetEntity spawner, string name)
    {
        Spawner = spawner;
        Name = name;
    }
}

[Serializable, NetSerializable]
public sealed class FlyingCraftConsoleState : BoundUserInterfaceState
{
    public int Balance;
    public bool NeedsSpawner;
    public List<FlyingCraftCatalogEntry> Catalog;
    public List<FlyingCraftSpawnPoint> Spawners;

    public FlyingCraftConsoleState(int balance, bool needsSpawner, List<FlyingCraftCatalogEntry> catalog,
        List<FlyingCraftSpawnPoint> spawners)
    {
        Balance = balance;
        NeedsSpawner = needsSpawner;
        Catalog = catalog;
        Spawners = spawners;
    }
}

/// <summary>Client -> server: buy <see cref="Proto"/>, spawning at <see cref="Spawner"/> (null = at the console).</summary>
[Serializable, NetSerializable]
public sealed class FlyingCraftBuyMessage : BoundUserInterfaceMessage
{
    public string Proto;
    public NetEntity? Spawner;

    public FlyingCraftBuyMessage(string proto, NetEntity? spawner)
    {
        Proto = proto;
        Spawner = spawner;
    }
}
