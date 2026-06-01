using RaceEngineer.Core.Storage;

namespace RaceEngineer.Core.Knowledge;

public sealed class WebResearchService
{
    private readonly StorageService storageService;
    private readonly ResearchCacheService cache;
    private readonly IWebResearchProvider provider;
    private readonly WebResearchOptions options;

    public WebResearchService(
        StorageService storageService,
        WebResearchOptions options,
        IWebResearchProvider? provider = null)
    {
        this.storageService = storageService;
        this.options = options;
        cache = new ResearchCacheService(storageService);
        this.provider = provider ?? CreateProvider(options);
    }

    public WebResearchOptions Options => options;

    public IWebResearchProvider Provider => provider;

    public static WebResearchService FromSettings(StorageService storageService, AppSettings settings) =>
        new(storageService, WebResearchOptions.FromAppSettings(settings));

    public static IWebResearchProvider CreateProvider(WebResearchOptions options) =>
        options.Enabled ? new WebSearchResearchProvider() : DisabledWebResearchProvider.Instance;

    public Task<WebResearchLookupResult> GetCachedAsync(
        string? track,
        string? car = null,
        string? carClass = null,
        CancellationToken cancellationToken = default) =>
        cache.LookupAsync(track, car, carClass, cancellationToken);

    public async Task<WebResearchBundle> LoadBundleAsync(
        string? track,
        string? car = null,
        string? carClass = null,
        CancellationToken cancellationToken = default)
    {
        var lookup = await cache.LookupAsync(track, car, carClass, cancellationToken);
        var displayTrack = track ?? lookup.Items.FirstOrDefault()?.Track;
        return lookup.Items.Count == 0
            ? WebResearchBundle.Empty(displayTrack, car, carClass)
            : new WebResearchBundle(displayTrack, car, carClass, lookup.Items);
    }

    public async Task<WebResearchBundle> EnsureCachedAsync(
        string? track,
        string? car,
        string? carClass,
        WebResearchContext context,
        CancellationToken cancellationToken = default)
    {
        var bundle = await LoadBundleAsync(track, car, carClass, cancellationToken);
        if (bundle.HasResearch && !ResearchCacheService.IsStale(bundle, options.ResearchRefreshDays))
        {
            return bundle;
        }

        if (!options.WebFetchAllowed)
        {
            return bundle;
        }

        if (context.IsActiveDriving && !options.AllowWebResearchDuringDriving)
        {
            return bundle;
        }

        if (string.IsNullOrWhiteSpace(track))
        {
            return bundle;
        }

        var trackFetch = await FetchTrackResearchAsync(track, car, carClass, cancellationToken);
        if (trackFetch.Items.Count == 0 && !string.IsNullOrWhiteSpace(carClass ?? car))
        {
            _ = await FetchTrackCarStrategyAsync(track, car, carClass, cancellationToken);
        }

        return await LoadBundleAsync(track, car, carClass, cancellationToken);
    }

    public async Task<WebResearchFetchResult> FetchTrackResearchAsync(
        string? track,
        string? car = null,
        string? carClass = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(track))
        {
            return WebResearchFetchResult.Failed(
                "Track must be detected before fetching track research.",
                provider.Name);
        }

        if (!options.Enabled)
        {
            return WebResearchFetchResult.Disabled("Web research is disabled in settings.");
        }

        var before = await cache.LookupAsync(track, car, carClass, cancellationToken);
        var cacheStatus = before.Items.Count > 0 ? "hit" : "miss";
        var fetch = await provider.FetchTrackResearchAsync(track.Trim(), cancellationToken);
        if (fetch.Items.Count > 0)
        {
            await cache.SaveManyAsync(fetch.Items, cancellationToken);
            cacheStatus = "updated";
        }

        return BuildFetchResult(fetch, cacheStatus);
    }

    public async Task<WebResearchFetchResult> FetchTrackCarStrategyAsync(
        string? track,
        string? car,
        string? carClass,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(track))
        {
            return WebResearchFetchResult.Failed(
                "Track must be detected before fetching track-car strategy.",
                provider.Name);
        }

        if (!options.Enabled)
        {
            return WebResearchFetchResult.Disabled("Web research is disabled in settings.");
        }

        var before = await cache.LookupAsync(track, car, carClass, cancellationToken);
        var cacheStatus = before.Items.Count > 0 ? "hit" : "miss";
        var fetch = await provider.FetchTrackCarStrategyAsync(track.Trim(), car, carClass, cancellationToken);
        if (fetch.Items.Count > 0)
        {
            await cache.SaveManyAsync(fetch.Items, cancellationToken);
            cacheStatus = "updated";
        }

        return BuildFetchResult(fetch, cacheStatus);
    }

    public async Task<WebResearchFetchResult> RefreshResearchAsync(
        string? track,
        string? car,
        string? carClass,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(track))
        {
            return WebResearchFetchResult.Failed(
                "Track must be detected before refreshing research.",
                provider.Name);
        }

        if (!options.Enabled)
        {
            var cached = await LoadBundleAsync(track, car, carClass, cancellationToken);
            return cached.HasResearch
                ? new WebResearchFetchResult(
                    cached.Items,
                    false,
                    "Web research is disabled; loaded cached research only.",
                    provider.Name,
                    "hit",
                    "failed",
                    "Web research is disabled in settings.")
                : WebResearchFetchResult.Disabled("Web research is disabled in settings.");
        }

        var before = await cache.LookupAsync(track, car, carClass, cancellationToken);
        var cacheStatus = before.Items.Count > 0 ? "hit" : "miss";
        var trackFetch = await provider.FetchTrackResearchAsync(track.Trim(), cancellationToken);
        if (trackFetch.Items.Count > 0)
        {
            await cache.SaveManyAsync(trackFetch.Items, cancellationToken);
        }

        var strategyFetch = await provider.FetchTrackCarStrategyAsync(track.Trim(), car, carClass, cancellationToken);
        if (strategyFetch.Items.Count > 0)
        {
            await cache.SaveManyAsync(strategyFetch.Items, cancellationToken);
        }

        var bundle = await LoadBundleAsync(track, car, carClass, cancellationToken);
        if (bundle.HasResearch)
        {
            cacheStatus = "updated";
        }

        return bundle.HasResearch
            ? WebResearchFetchResult.Succeeded(
                bundle.Items,
                $"Refreshed cached research for {TrackGuideCatalogIdentity.CanonicalName(track) ?? track.Trim()}.",
                provider.Name,
                cacheStatus)
            : WebResearchFetchResult.Failed(
                trackFetch.Message,
                provider.Name,
                cacheStatus);
    }

    public bool IsStale(WebResearchBundle bundle) =>
        ResearchCacheService.IsStale(bundle, options.ResearchRefreshDays);

    private WebResearchFetchResult BuildFetchResult(
        WebResearchFetchResult fetch,
        string cacheStatus)
    {
        if (fetch.Items.Count > 0)
        {
            return WebResearchFetchResult.Succeeded(
                fetch.Items,
                fetch.Message,
                provider.Name,
                cacheStatus,
                fetch.RefreshedFromRemote);
        }

        return WebResearchFetchResult.Failed(fetch.Message, provider.Name, cacheStatus);
    }
}
