using Content.Shared._NF.Medical.Prototypes;

namespace Content.Server._NF.Medical.Components;

[RegisterComponent]
[AutoGenerateComponentState]
public sealed partial class MedicalBountyComponent : Component
{
    /// <summary>
    /// The bounty to use/used for damage generation.
    /// If null, a medical bounty type will be selected at random.
    /// </summary>
    // Runtime-assigned (picked at random on startup when null). It holds a prototype OBJECT, which has no
    // data definition and cannot be read from or written to YAML — a [DataField] here would crash map/grid
    // saves. If this ever needs to persist, store it as ProtoId<MedicalBountyPrototype> and resolve on use.
    [ViewVariables]
    public MedicalBountyPrototype? Bounty = null;

    /// <summary>
    /// Maximum bounty value for this entity in spesos.
    /// Cached from bounty params on generation.
    /// </summary>
    [ViewVariables(VVAccess.ReadWrite), AutoNetworkedField]
    public int MaxBountyValue;

    /// <summary>
    /// Ensures damage is only applied once, set to true on startup.
    /// </summary>
    public bool BountyInitialized;
}
