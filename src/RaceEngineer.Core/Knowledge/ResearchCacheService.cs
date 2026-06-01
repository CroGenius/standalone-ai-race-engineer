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
        var sources = await storageService.ListKnowledgeSourcesAsync(cancellationToken: cancellationToken);
        return sources
            .Where(ResearchKnowledgeMapper.IsResearchSource)
            .Select(source =>
            {
                ResearchKnowledgeMapper.TryParse(source, out var item);
                return item;
            })
            .Where(item => item is not null)
            .Cast<ResearchKnowledgeItem>()
            .Where(item => MatchesTrack(item.Track, track))
            .Where(item => MatchesCarScope(item, car, carClass))
            .Where(item => string.IsNullOrWhiteSpace(topic)
                || string.Equals(item.Topic, topic, StringComparison.OrdinalIgnoreCase))
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
        return items.Count == 0
            ? new WebResearchLookupResult([], false, "No cached web research is stored for this track.")
            : new WebResearchLookupResult(items, true, null);
    }

    public async Task SaveAsync(ResearchKnowledgeItem item, CancellationToken cancellationToken = default)
    {
        await storageService.SaveKnowledgeSourceAsync(
            ResearchKnowledgeMapper.ToKnowledgeSource(NormalizeStoredItem(item)),
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

    internal static bool MatchesTrack(string? itemTrack, string? requestedTrack)
    {
        if (string.IsNullOrWhiteSpace(requestedTrack))
        {
            return true;
        }

        if (string.IsNullOrWhiteSpace(itemTrack))
        {
            return false;
        }

        return TrackGuideCatalogIdentity.Matches(itemTrack, requestedTrack);
    }

    internal static bool MatchesCarScope(ResearchKnowledgeItem item, string? car, string? carClass)
    {
        if (string.IsNullOrWhiteSpace(item.Car) && string.IsNullOrWhiteSpace(item.CarClass))
        {
            return true;
        }

        var normalizedClass = TrackCarKnowledgeCatalog.NormalizeCarClass(carClass ?? car);
        if (!string.IsNullOrWhiteSpace(normalizedClass)
            && !string.IsNullOrWhiteSpace(item.CarClass)
            && string.Equals(item.CarClass, normalizedClass, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (!string.IsNullOrWhiteSpace(car)
            && !string.IsNullOrWhiteSpace(item.Car)
            && string.Equals(item.Car, car, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return string.IsNullOrWhiteSpace(car) && string.IsNullOrWhiteSpace(carClass);
    }

    private static ResearchKnowledgeItem NormalizeStoredItem(ResearchKnowledgeItem item)
    {
        var canonicalTrack = TrackGuideCatalogIdentity.CanonicalName(item.Track) ?? item.Track?.Trim();
        var normalizedClass = TrackCarKnowledgeCatalog.NormalizeCarClass(item.CarClass ?? item.Car);
        return item with
        {
            Track = canonicalTrack,
            CarClass = normalizedClass ?? item.CarClass
        };
    }
}
