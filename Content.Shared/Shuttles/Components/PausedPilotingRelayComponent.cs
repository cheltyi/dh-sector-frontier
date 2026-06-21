using Robust.Shared.GameStates;

namespace Content.Shared.Shuttles.Components;

/// <summary>
/// Temporary component used to store the target of a RelayInputMoverComponent
/// when it's removed because the entity started piloting.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class PausedPilotingRelayComponent : Component
{
    // Dark Haven - persistence: not a [DataField]. Transient pilot-relay target stored only while piloting;
    // serializing it dangles on save (mirrors RelayInputMoverComponent.RelayEntity).
    [ViewVariables]
    public EntityUid RelayTarget;
} 