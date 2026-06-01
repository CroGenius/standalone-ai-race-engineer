using RaceEngineer.Core.Storage;

namespace RaceEngineer.Core.Knowledge;

public sealed class ResearchCacheService(StorageService storageService)
{
    public async Task<IReadOnlyList<ResearchKnowledgeItem>> ListCachedAsync(
        string? track = null,
        string? car = null,
        string? carClass = null,
        string? topic = null,
        CancellationToken cancellationToken = default)
    {
        var sources = await storageService.ListKnowledgeSourcesAsync(
            car: car,
            track: track,
            cancellationToken: cancellationToken);
        return sources
            .Where(ResearchKnowledgeMapper.IsResearchSource)
            .Select(source =>
            {
                ResearchKnowledgeMapper.TryParse(source, out var item);
                return item;
            })
            .Where(item => item is not null)
            .Cast<ResearchKnowledgeItem>()
            .Where(item => string.IsNullOrWhiteSpace(topic)
                || string.Equals(item.Topic, topic, StringComparison.OrdinalIgnoreCase))
            .Where(item => string.IsNullOrWhiteSpace(carClass)
                || string.IsNullOrWhiteSpace(item.CarClass)
                || string.Equals(item.CarClass, carClass, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(item => item.FetchedAt)
            .ToArray();
    }

    public async Task<WebResearchLookupResult> LookupAsync(
        string? track,
        string? car = null,
        string? carClass = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(track))
        {
            return new WebResearchLookupResult([], false, "Track is unavailable.");
        }

        var items = await ListCachedAsync(track.Trim(), car, carClass, cancellationToken: cancellationToken);
        if (items.Count == 0)
        {
            var allTrackItems = await ListCachedAsync(track.Trim(), cancellationToken: cancellationToken);
            items = allTrackItems;
        }

        return items.Count == 0
            ? new WebResearchLookupResult([], false, "No cached web research is stored for this track.")
            : new WebResearchLookupResult(items, true, null);
    }

    public async Task SaveAsync(ResearchKnowledgeItem item, CancellationToken cancellationToken = default)
    {
        await storageService.SaveKnowledgeSourceAsync(
            ResearchKnowledgeMapper.ToKnowledgeSource(item),
            cancellationToken);
    }

    public async Task SaveManyAsync(IReadOnlyList<ResearchKnowledgeItem> items, CancellationToken cancellationToken = default)
    {
        foreach (var item in items)
        {
            await SaveAsync(item, cancellationToken);
        }
    }

    public static bool IsStale(ResearchKnowledgeItem item, int refreshDays) =>
        DateTimeOffset.UtcNow - item.FetchedAt > TimeSpan.FromDays(refreshDays);

    public static bool IsStale(WebResearchBundle bundle, int refreshDays) =>
        bundle.LastFetchedAt is null || DateTimeOffset.UtcNow - bundle.LastFetchedAt.Value > TimeSpan.FromDays(refreshDays);
}
