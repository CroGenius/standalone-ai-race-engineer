using RaceEngineer.Core.Knowledge;

namespace RaceEngineer.Desktop.Wpf;

public sealed class KnowledgeSourceItem(KnowledgeSource source)
{
    public KnowledgeSource Source { get; } = source;

    public Guid Id => Source.Id;

    public string Display => $"{Source.Title} [{Source.SourceType}] {Source.Car ?? "-"} / {Source.Track ?? "-"} / {Source.Category ?? "-"}";
}
