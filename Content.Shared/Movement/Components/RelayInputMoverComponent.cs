using Content.Shared.Movement.Systems;
using Robust.Shared.GameStates;

namespace Content.Shared.Movement.Components;

/// <summary>
/// Raises the engine movement inputs for a particular entity onto the designated entity
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(true)]
[Access(typeof(SharedMoverController))]
public sealed partial class RelayInputMoverComponent : Component
{
    // Not a [DataField]: RelayEntity is always wired up at runtime via SharedMoverController.SetRelay (EnsureComp)
    // and reset on relink, so it never holds meaningful persistent state. For the Station AI brain it points at the
    // transient StationAiHolo eye; serializing it produces a dangling EntityUid reference on map save.
    // Keep [AutoNetworkedField] for client sync.
    [AutoNetworkedField]
    public EntityUid RelayEntity;
}
