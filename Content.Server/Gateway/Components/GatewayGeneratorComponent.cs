using Content.Shared.Parallax.Biomes.Markers;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Server.Gateway.Components;

/// <summary>
/// Generates gateway destinations at a regular interval.
/// </summary>
[RegisterComponent, AutoGenerateComponentPause]
public sealed partial class GatewayGeneratorComponent : Component
{
    /// <summary>
    /// Prototype to spawn on the generated map if applicable.
    /// </summary>
    [DataField]
    public EntProtoId? Proto = "Gateway";

    /// <summary>
    /// Next time another seed unlocks.
    /// </summary>
    [DataField(customTypeSerializer:typeof(TimeOffsetSerializer))]
    [AutoPausedField]
    public TimeSpan NextUnlock;

    /// <summary>
    /// How long it takes to unlock another destination once one is taken.
    /// </summary>
    [DataField]
    public TimeSpan UnlockCooldown = TimeSpan.FromMinutes(75);

    /// <summary>
    /// Maps we've generated.
    /// </summary>
    // Dark Haven: deliberately NOT a [DataField]. These are transient, procedurally-generated expedition MAP
    // entities that are never persisted. Serializing this list dragged entire expedition maps into the main
    // sector save (the engine auto-includes referenced maps) and then logged "missing entity" errors for the
    // gateway structures on them. On load the generator simply starts empty and generates fresh destinations,
    // so nothing of value is lost; the live list still lives in memory for normal runtime use.
    [ViewVariables]
    public List<EntityUid> Generated = new();

    [DataField]
    public int MobLayerCount = 1;

    /// <summary>
    /// Mob layers to pick from.
    /// </summary>
    [DataField]
    public List<ProtoId<BiomeMarkerLayerPrototype>> MobLayers = new()
    {
        "NFCarps",
        "NFXenos",
        "NFFlesh",
        "NFArgocytes",
        "NFPunks",
        "NFDinosaurs",
        "NFMercenaries",
        "NFSyndicate",
        "NFExplorers",
        "NFSilicons",
        "NFCultists",
        "Slimes",
    };

    [DataField]
    public int LootLayerCount = 0;

    /// <summary>
    /// Loot layers to pick from.
    /// </summary>
    public List<ProtoId<BiomeMarkerLayerPrototype>> LootLayers = new()
    {
        "OreIron",
        "OreQuartz",
        "OreCoal",
        "OreSalt",
        "OreGold",
        "OreSilver",
        "OrePlasma",
        "OreUranium",
        "OreDiamond",
        "OreArtifactFragment",
        "OreMagmite",
    };
}

