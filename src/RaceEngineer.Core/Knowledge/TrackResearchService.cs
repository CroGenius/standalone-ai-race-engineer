using RaceEngineer.Core.Storage;

namespace RaceEngineer.Core.Knowledge;

public sealed class TrackResearchService
{
    private readonly StorageService storageService;
    private readonly CachedTrackResearchService cache;
    private readonly ITrackResearchService refreshService;
    private readonly TrackResearchOptions options;

    public TrackResearchService(
        StorageService storageService,
        TrackResearchOptions options,
        ITrackResearchService? refreshService = null)
    {
        this.storageService = storageService;
        this.options = options;
        cache = new CachedTrackResearchService(storageService);
        this.refreshService = refreshService ?? CreateRefreshService(storageService, options);
    }

    public TrackResearchOptions Options => options;

    public ITrackResearchService RefreshService => refreshService;

    public static TrackResearchService FromSettings(StorageService storageService, AppSettings settings) =>
        new(storageService, TrackResearchOptions.FromAppSettings(settings));

    public static ITrackResearchService CreateRefreshService(StorageService storageService, TrackResearchOptions options) =>
        options.Enabled && options.Provider == "web"
            ? new FutureWebTrackResearchService(storageService)
            : DisabledTrackResearchService.Instance;

    public Task<TrackGuideLookupResult> GetCachedGuideAsync(string? trackName, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(trackName))
        {
            return Task.FromResult(new TrackGuideLookupResult(null, false, "Track name is unavailable."));
        }

        return cache.LookupCachedAsync(trackName.Trim(), cancellationToken);
    }

    public async Task<TrackGuideLookupResult> EnsureGuideCachedAsync(
        string? trackName,
        TrackResearchContext context,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(trackName))
        {
            return new TrackGuideLookupResult(null, false, "Track name is unavailable.");
        }

        var normalizedTrack = trackName.Trim();
        var cached = await cache.LookupCachedAsync(normalizedTrack, cancellationToken);
        if (cached.Guide is not null && !IsStale(cached.Guide))
        {
            return cached;
        }

        if (!options.WebFetchAllowed)
        {
            return cached.Guide is not null
                ? cached
                : new TrackGuideLookupResult(null, false, "Track web research is disabled.");
        }

        if (context.IsActiveDriving && !options.AllowTrackResearchDuringDriving)
        {
            return cached.Guide is not null
                ? cached
                : new TrackGuideLookupResult(
                    null,
                    false,
                    "Track guide refresh is deferred while driving. Refresh the guide before the session or when stationary.");
        }

        var fetch = await refreshService.RefreshGuideAsync(normalizedTrack, cancellationToken);
        if (fetch.Guide is null)
        {
            return cached.Guide is not null
                ? cached
                : new TrackGuideLookupResult(null, false, fetch.Message);
        }

        return new TrackGuideLookupResult(fetch.Guide, false, null);
    }

    public async Task<TrackGuideFetchResult> FetchGuideAsync(
        string? trackName,
        bool forceRefresh = true,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(trackName))
        {
            return new TrackGuideFetchResult(null, false, "Track name is unavailable.");
        }

        if (!options.Enabled)
        {
            return new TrackGuideFetchResult(null, false, "Track research is disabled in settings.");
        }

        if (options.Provider == "local")
        {
            var cached = await cache.LookupCachedAsync(trackName.Trim(), cancellationToken);
            return cached.Guide is null
                ? new TrackGuideFetchResult(null, false, "No cached track guide is stored. Enable web research to refresh one.")
                : new TrackGuideFetchResult(cached.Guide, false, "Loaded cached track guide.");
        }

        if (!forceRefresh)
        {
            var existing = await cache.LookupCachedAsync(trackName.Trim(), cancellationToken);
            if (existing.Guide is not null && !IsStale(existing.Guide))
            {
                return new TrackGuideFetchResult(existing.Guide, false, "Cached track guide is still fresh.");
            }
        }

        return await refreshService.RefreshGuideAsync(trackName.Trim(), cancellationToken);
    }

    public async Task<IReadOnlyList<KnowledgeSource>> LoadTrackKnowledgeAsync(
        string? trackName,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(trackName))
        {
            return [];
        }

        return await storageService.ListKnowledgeSourcesAsync(
            track: trackName.Trim(),
            category: "track",
            cancellationToken: cancellationToken);
    }

    public bool IsStale(TrackGuide guide)
    {
        var anchor = guide.RefreshedAt ?? guide.FetchedAt;
        return DateTimeOffset.UtcNow - anchor > TimeSpan.FromDays(options.TrackGuideRefreshDays);
    }

    public static TrackGuide? ResolveGuideFromSources(IEnumerable<KnowledgeSource> sources, string? trackName = null)
    {
        foreach (var source in sources.OrderByDescending(item => item.RetrievedAt))
        {
            if (!TrackGuideMapper.TryParse(source, out var guide))
            {
                continue;
            }

            if (string.IsNullOrWhiteSpace(trackName) || TrackGuideMatcher.Matches(guide, trackName))
            {
                return guide;
            }
        }

        return null;
    }
}
