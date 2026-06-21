using Robust.Shared.GameStates;
using Robust.Shared.Network;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom; // Dark Haven - persistence: TimeOffsetSerializer

namespace Content.Shared._NF.Shipyard.Components;

/// <summary>
/// Tracks ownership of a ship grid and manages deletion when the owner has been offline too long
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState, AutoGenerateComponentPause]
public sealed partial class ShipOwnershipComponent : Component
{
    /// <summary>
    /// The owner's player session ID
    /// </summary>
    [DataField, AutoNetworkedField]
    public NetUserId OwnerUserId;

    /// <summary>
    /// When the owner last connected or disconnected
    /// </summary>
    // Dark Haven - persistence: absolute CurTime, so use TimeOffsetSerializer + pause handling. Otherwise the
    // offline-deletion timer is corrupted after a restart (CurTime resets) and ships are never/instantly deleted.
    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoNetworkedField, AutoPausedField]
    public TimeSpan LastStatusChangeTime;

    /// <summary>
    /// Whether the owner is currently online
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool IsOwnerOnline;

    /// <summary>
    /// How long to wait after the owner disconnects before deleting their ship (in seconds)
    /// </summary>
    [DataField]
    public float DeletionTimeoutSeconds = 7200; // 2 hours
}
