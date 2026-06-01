namespace RaceEngineer.Core.Knowledge;

public interface IWebResearchProvider
{
    string Name { get; }

    bool CanFetchFromRemote { get; }

    Task<WebResearchFetchResult> FetchTrackResearchAsync(
        string trackName,
        CancellationToken cancellationToken = default);

    Task<WebResearchFetchResult> FetchTrackCarStrategyAsync(
        string trackName,
        string? carName,
        string? carClass,
        CancellationToken cancellationToken = default);
}
