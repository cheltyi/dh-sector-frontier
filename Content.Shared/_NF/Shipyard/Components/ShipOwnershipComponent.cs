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
    [DataField, AutoNetworkedField]
    public NetUserId OwnerUserId;

    /// <summary>
    /// When the owner last connected or disconnected
    /// </summary>
    // Dark Haven - persistence: absolute CurTime, so use TimeOffsetSerializer + pause handling. Otherwise the
    // offline-deletion timer is corrupted after a restart (CurTime resets) and ships are never/instantly deleted.
    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoNetworkedField, AutoPausedField]
    public TimeSpan LastStatusChangeTime;

    [DataField, AutoNetworkedField]
    public bool IsDeletionTimerPaused;

    [DataField, AutoNetworkedField]
    public TimeSpan DeletionTimerStartTime;

    [DataField, AutoNetworkedField]
    public TimeSpan AccumulatedUnpoweredTime;

    [DataField]
    public float DeletionTimeoutSeconds = 7200;
}
