using Robust.Shared.Serialization;

namespace Content.Shared.StationRecords;

[Serializable, NetSerializable, DataDefinition]
public sealed partial class StationRecordsFilter
{
    [DataField]
    public StationRecordFilterType Type = StationRecordFilterType.Name;
    [DataField]
    public string Value  = "";

    // Parameterless ctor required so the data-definition instantiator can construct the filter on load.
    public StationRecordsFilter() { }

    public StationRecordsFilter(StationRecordFilterType filterType, string newValue = "")
    {
        Type = filterType;
        Value = newValue;
    }
}

/// <summary>
/// Message for updating the filter on any kind of records console.
/// </summary>
[Serializable, NetSerializable]
public sealed class SetStationRecordFilter : BoundUserInterfaceMessage
{
    public readonly string Value;
    public readonly StationRecordFilterType Type;

    public SetStationRecordFilter(StationRecordFilterType filterType,
        string filterValue)
    {
        Type = filterType;
        Value = filterValue;
    }
}

/// <summary>
/// Different strings that results can be filtered by.
/// </summary>
[Serializable, NetSerializable]
public enum StationRecordFilterType : byte
{
    Name,
    Job,
    Species,
    Prints,
    DNA,
}
