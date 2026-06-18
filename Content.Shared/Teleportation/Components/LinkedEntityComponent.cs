using Content.Shared.Teleportation.Systems;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared.Teleportation.Components;

/// <summary>
///     Represents an entity which is linked to other entities (perhaps portals), and which can be walked through /
///     thrown into to teleport an entity.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
[Access(typeof(LinkedEntitySystem))]
public sealed partial class LinkedEntityComponent : Component
{
    /// <summary>
    ///     The entities that this entity is linked to.
    ///     Not a [DataField]: portal links are transient runtime state created by LinkedEntitySystem.TryLink/OneWayLink
    ///     (e.g. GatewaySystem.OpenPortal) and torn down by TryUnlink/ClosePortal. They are never re-established on map
    ///     load, and the linked target frequently lives on another, unsaved map, so persisting them only writes dangling
    ///     EntityUid references on save. Keep [AutoNetworkedField] for client-side portal prediction.
    /// </summary>
    [AutoNetworkedField]
    public HashSet<EntityUid> LinkedEntities = new();

    /// <summary>
    ///     Should this entity be deleted if all of its links are removed?
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public bool DeleteOnEmptyLinks;
}

[Serializable, NetSerializable]
public enum LinkedEntityVisuals : byte
{
    HasAnyLinks
}
