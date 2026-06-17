using Robust.Shared.Serialization;

namespace Content.Shared.MassMedia.Systems;

public abstract class SharedNewsSystem : EntitySystem
{
    public const int MaxTitleLength = 25;
    public const int MaxContentLength = 2048;
}

[Serializable, NetSerializable, DataDefinition]
public partial struct NewsArticle
{
    [DataField]
    public string Title;

    [DataField]
    public string Content;

    [DataField]
    public string? Author;

    // Runtime-only cross-references: a value-tuple element type has no data definition, so this stays a
    // plain field (NOT a [DataField]) — otherwise YAML-serializing the article would crash the save.
    [ViewVariables]
    public ICollection<(NetEntity, uint)>? AuthorStationRecordKeyIds;

    [DataField]
    public TimeSpan ShareTime;
}

[ByRefEvent]
public record struct NewsArticlePublishedEvent(NewsArticle Article);

[ByRefEvent]
public record struct NewsArticleDeletedEvent;
