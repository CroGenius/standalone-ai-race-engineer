using RaceEngineer.Core.Storage;

namespace RaceEngineer.Core.Knowledge;

public sealed class StoredResearchService(StorageService storageService) : IResearchService
{
    public Task<IReadOnlyList<KnowledgeSource>> SearchAsync(string query, CancellationToken cancellationToken = default)
    {
        return storageService.SearchKnowledgeSourcesAsync(query, cancellationToken);
    }

    public async Task<ResearchBrief> GetTrackBriefAsync(string trackName, CancellationToken cancellationToken = default)
    {
        var sources = await storageService.ListKnowledgeSourcesAsync(track: trackName, category: "track", cancellationToken: cancellationToken);
        return Brief($"Track brief: {trackName}", sources, "No stored track notes are available. External research is unavailable in offline mode.");
    }

    public async Task<ResearchBrief> GetCarBriefAsync(string carName, CancellationToken cancellationToken = default)
    {
        var sources = await storageService.ListKnowledgeSourcesAsync(car: carName, category: "car", cancellationToken: cancellationToken);
        return Brief($"Car brief: {carName}", sources, "No stored car notes are available. External research is unavailable in offline mode.");
    }

    public async Task<ResearchBrief> GetSetupNotesAsync(string carName, string trackName, CancellationToken cancellationToken = default)
    {
        var sources = await storageService.ListKnowledgeSourcesAsync(car: carName, track: trackName, category: "setup", cancellationToken: cancellationToken);
        return Brief($"Setup notes: {carName} at {trackName}", sources, "No stored setup notes are available. External research is unavailable in offline mode.");
    }

    public async Task<ResearchBrief> GetStrategyNotesAsync(string trackName, string sessionType, CancellationToken cancellationToken = default)
    {
        var sources = await storageService.ListKnowledgeSourcesAsync(track: trackName, sessionType: sessionType, category: "strategy", cancellationToken: cancellationToken);
        return Brief($"Strategy notes: {trackName} {sessionType}", sources, "No stored strategy notes are available. External research is unavailable in offline mode.");
    }

    private static ResearchBrief Brief(string title, IReadOnlyList<KnowledgeSource> sources, string unavailableReason)
    {
        if (sources.Count == 0)
        {
            return new ResearchBrief(title, "", sources, unavailableReason);
        }

        var content = string.Join(Environment.NewLine, sources.Select(source => $"- {source.Title}: {Trim(source.Content)}"));
        return new ResearchBrief(title, content, sources, null);
    }

    private static string Trim(string value)
    {
        const int maxLength = 300;
        var normalized = value.ReplaceLineEndings(" ").Trim();
        return normalized.Length <= maxLength ? normalized : normalized[..maxLength] + "...";
    }
}
