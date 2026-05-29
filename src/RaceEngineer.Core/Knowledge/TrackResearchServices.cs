using RaceEngineer.Core.Storage;

namespace RaceEngineer.Core.Knowledge;

public sealed class DisabledTrackResearchService : ITrackResearchService
{
    public static DisabledTrackResearchService Instance { get; } = new();

    public string Name => "disabled";

    public bool CanRefreshFromRemote => false;

    public Task<TrackGuideLookupResult> LookupCachedAsync(string trackName, CancellationToken cancellationToken = default) =>
        Task.FromResult(new TrackGuideLookupResult(null, false, "Track research is disabled."));

    public Task<TrackGuideFetchResult> RefreshGuideAsync(string trackName, CancellationToken cancellationToken = default) =>
        Task.FromResult(new TrackGuideFetchResult(null, false, "Track research is disabled."));
}

public sealed class CachedTrackResearchService(StorageService storageService) : ITrackResearchService
{
    public string Name => "cached";

    public bool CanRefreshFromRemote => false;

    public Task<TrackGuideLookupResult> LookupCachedAsync(string trackName, CancellationToken cancellationToken = default) =>
        LookupInternalAsync(trackName, cancellationToken);

    public Task<TrackGuideFetchResult> RefreshGuideAsync(string trackName, CancellationToken cancellationToken = default) =>
        Task.FromResult(new TrackGuideFetchResult(
            null,
            false,
            "Cached track research only reads stored guides. Use Refresh Guide when remote research is enabled."));

    public async Task<TrackGuide?> LoadGuideAsync(string trackName, CancellationToken cancellationToken = default)
    {
        var lookup = await LookupInternalAsync(trackName, cancellationToken);
        return lookup.Guide;
    }

    public async Task SaveGuideAsync(TrackGuide guide, string sourceType, CancellationToken cancellationToken = default)
    {
        await storageService.SaveKnowledgeSourceAsync(
            TrackGuideMapper.ToKnowledgeSource(guide, sourceType),
            cancellationToken);
    }

    private async Task<TrackGuideLookupResult> LookupInternalAsync(string trackName, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(trackName))
        {
            return new TrackGuideLookupResult(null, false, "Track name is unavailable.");
        }

        var sources = await storageService.ListKnowledgeSourcesAsync(
            track: trackName.Trim(),
            category: "track",
            cancellationToken: cancellationToken);
        foreach (var source in sources.OrderByDescending(item => item.RetrievedAt))
        {
            if (TrackGuideMapper.TryParse(source, out var guide) && TrackGuideMatcher.Matches(guide, trackName))
            {
                return new TrackGuideLookupResult(TrackGuideNormalizer.Normalize(guide), true, null);
            }
        }

        var allTrackSources = await storageService.ListKnowledgeSourcesAsync(
            category: "track",
            cancellationToken: cancellationToken);
        foreach (var source in allTrackSources.OrderByDescending(item => item.RetrievedAt))
        {
            if (TrackGuideMapper.TryParse(source, out var guide) && TrackGuideMatcher.Matches(guide, trackName))
            {
                return new TrackGuideLookupResult(TrackGuideNormalizer.Normalize(guide), true, null);
            }
        }

        return new TrackGuideLookupResult(null, false, "No cached track guide is stored for this track.");
    }
}

public sealed class FutureWebTrackResearchService(StorageService storageService) : ITrackResearchService
{
    private readonly CachedTrackResearchService cache = new(storageService);

    public string Name => "future-web";

    public bool CanRefreshFromRemote => true;

    public Task<TrackGuideLookupResult> LookupCachedAsync(string trackName, CancellationToken cancellationToken = default) =>
        cache.LookupCachedAsync(trackName, cancellationToken);

    public async Task<TrackGuideFetchResult> RefreshGuideAsync(string trackName, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(trackName))
        {
            return new TrackGuideFetchResult(null, false, "Track name is unavailable.");
        }

        if (!TrackGuideWebCatalog.TryGetGuide(trackName, out var guide))
        {
            return new TrackGuideFetchResult(
                null,
                false,
                $"No remote track guide is available yet for {trackName.Trim()}.");
        }

        var now = DateTimeOffset.UtcNow;
        var refreshed = guide with
        {
            Id = TrackGuide.IdForTrack(trackName),
            TrackName = trackName.Trim(),
            FetchedAt = guide.FetchedAt == default ? now : guide.FetchedAt,
            RefreshedAt = now,
            ProviderName = "future-web-catalog"
        };
        refreshed = TrackGuideNormalizer.Normalize(refreshed);
        await cache.SaveGuideAsync(refreshed, KnowledgeSourceTypes.Web, cancellationToken);
        return new TrackGuideFetchResult(refreshed, true, $"Cached track guide saved for {refreshed.TrackName}.");
    }
}

public static class TrackGuideMatcher
{
    public static bool Matches(TrackGuide guide, string trackName)
    {
        if (string.IsNullOrWhiteSpace(trackName))
        {
            return false;
        }

        var key = TrackGuide.NormalizeTrackKey(trackName);
        if (TrackGuide.NormalizeTrackKey(guide.TrackName) == key)
        {
            return true;
        }

        return guide.Aliases.Any(alias =>
        {
            var aliasKey = TrackGuide.NormalizeTrackKey(alias);
            return key.Contains(aliasKey, StringComparison.Ordinal)
                || aliasKey.Contains(key, StringComparison.Ordinal);
        });
    }
}

public static class TrackGuideNormalizer
{
    public static TrackGuide Normalize(TrackGuide guide) =>
        guide with
        {
            Aliases = guide.Aliases ?? [],
            SectorNotes = guide.SectorNotes ?? [],
            MajorBrakingZones = guide.MajorBrakingZones ?? [],
            TractionZones = guide.TractionZones ?? [],
            HighSpeedSections = guide.HighSpeedSections ?? [],
            OvertakingZones = guide.OvertakingZones ?? [],
            SetupPriorities = guide.SetupPriorities ?? [],
            TyreStressNotes = guide.TyreStressNotes ?? [],
            TyreWarmupNotes = guide.TyreWarmupNotes ?? [],
            TyreDegradationNotes = guide.TyreDegradationNotes ?? [],
            FuelCharacteristics = guide.FuelCharacteristics ?? [],
            FuelStrategyNotes = guide.FuelStrategyNotes ?? [],
            Corners = guide.Corners ?? [],
            Sources = guide.Sources ?? []
        };
}
