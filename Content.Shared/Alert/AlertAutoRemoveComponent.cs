using Robust.Shared.GameStates;

namespace Content.Shared.Alert;

/// <summary>
///     Copy of the entity's alerts that are flagged for autoRemove, so that not all of the alerts need to be checked constantly
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class AlertAutoRemoveComponent : Component
{
    /// <summary>
    ///     List of alerts that have to be checked on every tick for automatic removal at a specific time
    /// </summary>
    // AlertKeys is a pure runtime cache rebuilt from active alerts (see component summary above); it is not
    // config. AlertKey is a network-only struct (no [DataDefinition]), so a [DataField] here would crash
    // map/grid saves ("No data definition found for type AlertKey when writing"). Networked-only, not saved.
    [AutoNetworkedField]
    public List<AlertKey> AlertKeys = new();

    public override bool SendOnlyToOwner => true;
}
