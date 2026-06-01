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
        return lookup.Items.Count == 0
            ? WebResearchBundle.Empty(track, car, carClass)
            : new WebResearchBundle(track, car, carClass, lookup.Items);
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

        var trackFetch = await provider.FetchTrackResearchAsync(track.Trim(), cancellationToken);
        if (trackFetch.Items.Count > 0)
        {
            await cache.SaveManyAsync(trackFetch.Items, cancellationToken);
        }

        if (!string.IsNullOrWhiteSpace(car) || !string.IsNullOrWhiteSpace(carClass))
        {
            var strategyFetch = await provider.FetchTrackCarStrategyAsync(
                track.Trim(),
                car,
                carClass,
                cancellationToken);
            if (strategyFetch.Items.Count > 0)
            {
                await cache.SaveManyAsync(strategyFetch.Items, cancellationToken);
            }
        }

        return await LoadBundleAsync(track, car, carClass, cancellationToken);
    }

    public async Task<WebResearchFetchResult> FetchTrackResearchAsync(
        string? track,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(track))
        {
            return new WebResearchFetchResult([], false, "Track must be detected before fetching track research.");
        }

        if (!options.Enabled)
        {
            return new WebResearchFetchResult([], false, "Web research is disabled in settings.");
        }

        var fetch = await provider.FetchTrackResearchAsync(track.Trim(), cancellationToken);
        if (fetch.Items.Count > 0)
        {
            await cache.SaveManyAsync(fetch.Items, cancellationToken);
        }

        return fetch;
    }

    public async Task<WebResearchFetchResult> FetchTrackCarStrategyAsync(
        string? track,
        string? car,
        string? carClass,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(track))
        {
            return new WebResearchFetchResult([], false, "Track must be detected before fetching track-car strategy.");
        }

        if (!options.Enabled)
        {
            return new WebResearchFetchResult([], false, "Web research is disabled in settings.");
        }

        var fetch = await provider.FetchTrackCarStrategyAsync(track.Trim(), car, carClass, cancellationToken);
        if (fetch.Items.Count > 0)
        {
            await cache.SaveManyAsync(fetch.Items, cancellationToken);
        }

        return fetch;
    }

    public async Task<WebResearchFetchResult> RefreshResearchAsync(
        string? track,
        string? car,
        string? carClass,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(track))
        {
            return new WebResearchFetchResult([], false, "Track must be detected before refreshing research.");
        }

        if (!options.Enabled)
        {
            var cached = await LoadBundleAsync(track, car, carClass, cancellationToken);
            return cached.HasResearch
                ? new WebResearchFetchResult(cached.Items, false, "Web research is disabled; loaded cached research only.")
                : new WebResearchFetchResult([], false, "Web research is disabled in settings.");
        }

        var trackFetch = await provider.FetchTrackResearchAsync(track.Trim(), cancellationToken);
        await cache.SaveManyAsync(trackFetch.Items, cancellationToken);
        var strategyFetch = await provider.FetchTrackCarStrategyAsync(track.Trim(), car, carClass, cancellationToken);
        await cache.SaveManyAsync(strategyFetch.Items, cancellationToken);
        var bundle = await LoadBundleAsync(track, car, carClass, cancellationToken);
        return new WebResearchFetchResult(
            bundle.Items,
            trackFetch.RefreshedFromRemote || strategyFetch.RefreshedFromRemote,
            bundle.HasResearch
                ? $"Refreshed cached research for {track.Trim()}."
                : trackFetch.Message);
    }

    public bool IsStale(WebResearchBundle bundle) =>
        ResearchCacheService.IsStale(bundle, options.ResearchRefreshDays);
}
