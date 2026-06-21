using Content.Shared.Dataset;
using Content.Shared.FixedPoint;
using Robust.Shared.Prototypes;

namespace Content.Server._NF.Smuggling.Components;

/// <summary>
///     Stores dead drop information for the entire sector.
///     Frequency of dead drops, and other dead drop mechanics should be driven by this state.
/// </summary>
[RegisterComponent]
public sealed partial class SectorDeadDropComponent : Component
{
    /// <summary>
    ///     Accumulator for FUC values.  Pays out at a given amount.
    /// </summary>
    // Dark Haven - persistence: [DataField] so the sub-payout accumulator survives a restart. The sector service
    // entity hosting this component is now persisted (see StationSectorServiceHostComponent), and ComponentInit
    // does not reset this field, so it round-trips correctly.
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public FixedPoint2 FUCAccumulator = FixedPoint2.Zero;

    // Utility field for windowing reported events.  Having more in an hour results in more precise information.
    [ViewVariables(VVAccess.ReadWrite)]
    public WindowedCounter? ReportedEventsThisHour = null;

    // In the case of providing a fake location for alternative notifications, which names can we draw from?
    [ViewVariables(VVAccess.ReadWrite)]
    public Dictionary<EntityUid, string> DeadDropStationNames = new();

    [DataField]
    public ProtoId<LocalizedDatasetPrototype> FakeDeadDropHints = default!;
}
