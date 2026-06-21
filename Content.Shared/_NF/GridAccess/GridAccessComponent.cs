using Robust.Shared.GameStates;
using Robust.Shared.Audio;

namespace Content.Shared._NF.GridAccess;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class GridAccessComponent : Component
{
    /// <summary>
    /// Frontier - Grid access
    /// The uid to which this device is limited to be used on.
    /// </summary>
    // Dark Haven - persistence: not a [DataField]. Caches a shuttle grid root that is cross-map relative to the
    // device; serializing it dangles on save. Authorization simply resets on restart (re-swipe once). Keep
    // [AutoNetworkedField] for client sync.
    [ViewVariables, AutoNetworkedField]
    public EntityUid? LinkedShuttleUid = null;

    [DataField]
    public SoundSpecifier ErrorSound =
        new SoundPathSpecifier("/Audio/Effects/Cargo/buzz_sigh.ogg");

    [DataField]
    public SoundSpecifier SwipeSound =
        new SoundPathSpecifier("/Audio/Machines/id_swipe.ogg");

    [DataField]
    public SoundSpecifier InsertSound =
        new SoundPathSpecifier("/Audio/Machines/id_insert.ogg");
}
