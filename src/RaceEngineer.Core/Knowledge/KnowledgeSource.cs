namespace RaceEngineer.Core.Knowledge;

public static class KnowledgeSourceTypes
{
    public const string ManualNote = "manual_note";
    public const string ImportedFile = "imported_file";
    public const string Web = "web";
}

public sealed record KnowledgeSource(
    Guid Id,
    string SourceType,
    string Title,
    string? Url,
    DateTimeOffset RetrievedAt,
    string Content,
    string? ConfidenceNote,
    string? Car,
    string? Track,
    string? SessionType,
    string? Category)
{
    public static KnowledgeSource ManualNote(
        string title,
        string content,
        string? car = null,
        string? track = null,
        string? sessionType = null,
        string? category = null,
        string? confidenceNote = null)
    {
        return new KnowledgeSource(
            Guid.NewGuid(),
            KnowledgeSourceTypes.ManualNote,
            title,
            null,
            DateTimeOffset.UtcNow,
            content,
            confidenceNote ?? "Manual note; reliability depends on the author.",
            car,
            track,
            sessionType,
            category);
    }

    public static KnowledgeSource ImportedFile(
        string title,
        string content,
        string? url,
        string? car = null,
        string? track = null,
        string? sessionType = null,
        string? category = null,
        string? confidenceNote = null)
    {
        return new KnowledgeSource(
            Guid.NewGuid(),
            KnowledgeSourceTypes.ImportedFile,
            title,
            url,
            DateTimeOffset.UtcNow,
            content,
            confidenceNote ?? "Imported local file; verify it is current before relying on it.",
            car,
            track,
            sessionType,
            category);
    }
}

public sealed record ResearchBrief(
    string Title,
    string Content,
    IReadOnlyList<KnowledgeSource> Sources,
    string? UnavailableReason);
