namespace Content.Shared._Mono;

/// <summary>
/// Component that applies GodMode to all non-organic entities on a grid.
/// </summary>
[RegisterComponent]
public sealed partial class GridGodModeComponent : Component
{
    /// <summary>
    /// The list of entities that have been given GodMode by this component.
    /// Runtime-only tracking set populated by GridGodModeSystem.OnGridGodModeMapInit when the grid is first
    /// map-initialized. It must NOT be serialized: it accumulates grid-resident EntityUids and is never pruned when one
    /// of those entities is deleted/deconstructed, so persisting it writes stale/deleted EntityUids that fail world
    /// serialization ([ERRO] Encountered a reference to a deleted entity ...). The per-entity protection lives on each
    /// entity's own (serialized) GodmodeComponent, so dropping [DataField] does not lose protection state on restore.
    /// </summary>
    public HashSet<EntityUid> ProtectedEntities = new();
}
