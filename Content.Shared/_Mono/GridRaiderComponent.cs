namespace Content.Shared._Mono;

/// <summary>
/// Component that applies NoHack and NoDeconstruct to entities with Door and/or VendingMachine components on a grid.
/// Protection is applied once during initialization and remains until the component is removed.
/// </summary>
[RegisterComponent]
public sealed partial class GridRaiderComponent : Component
{
    /// <summary>
    /// The list of entities that have been given NoHack and NoDeconstruct by this component.
    /// Runtime-only tracking set populated by GridRaiderSystem.OnGridRaiderMapInit when the grid is first
    /// map-initialized. It must NOT be serialized: it accumulates grid-resident EntityUids and is never pruned when one
    /// of those entities is deleted, so persisting it writes stale/deleted EntityUids that fail world serialization.
    /// The per-entity protection lives on each entity's own (serialized) NoHack/NoDeconstruct components, so dropping
    /// [DataField] does not lose protection state on restore.
    /// </summary>
    public HashSet<EntityUid> ProtectedEntities = new();

    /// <summary>
    /// Whether to protect entities with Door components.
    /// </summary>
    [DataField]
    public bool ProtectDoors = true;

    /// <summary>
    /// Whether to protect entities with VendingMachine components.
    /// </summary>
    [DataField]
    public bool ProtectVendingMachines = true;
}
