using Content.Shared.Actions;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Silicons.Laws.Components;

/// <summary>
/// This is used for entities which are bound to silicon laws and can view them.
/// </summary>
[RegisterComponent, NetworkedComponent, Access(typeof(SharedSiliconLawSystem))]
public sealed partial class SiliconLawBoundComponent : Component
{
    /// <summary>
    /// The last entity that provided laws to this entity.
    /// </summary>
    // Dark Haven: deliberately NOT a [DataField]. This is a runtime cache (the station/grid/self that last
    // supplied laws), re-resolved on the next GetSiliconLaws and defensively nulled if its target is gone.
    // Persisting it made a saved silicon (e.g. a Station AI) dangle a "missing entity" reference on map save,
    // because the provider is usually a nullspace station or a grid outside the saved map. Laws themselves are
    // unaffected — this just re-populates after load.
    [ViewVariables]
    public EntityUid? LastLawProvider;
}

/// <summary>
/// Event raised to get the laws that a law-bound entity has.
///
/// Is first raised on the entity itself, then on the
/// entity's station, then on the entity's grid,
/// before being broadcast.
/// </summary>
/// <param name="Entity"></param>
[ByRefEvent]
public record struct GetSiliconLawsEvent(EntityUid Entity)
{
    public EntityUid Entity = Entity;

    public SiliconLawset Laws = new();

    public bool Handled = false;
}

public sealed partial class ToggleLawsScreenEvent : InstantActionEvent
{

}

[NetSerializable, Serializable]
public enum SiliconLawsUiKey : byte
{
    Key
}

[Serializable, NetSerializable]
public sealed class SiliconLawBuiState : BoundUserInterfaceState
{
    public List<SiliconLaw> Laws;
    public HashSet<string>? RadioChannels;

    public SiliconLawBuiState(List<SiliconLaw> laws, HashSet<string>? radioChannels)
    {
        Laws = laws;
        RadioChannels = radioChannels;
    }
}
