namespace RaceEngineer.Core.Knowledge;

public interface ITrackResearchService
{
    string Name { get; }

    bool CanRefreshFromRemote { get; }

    Task<TrackGuideLookupResult> LookupCachedAsync(
        string trackName,
        CancellationToken cancellationToken = default);

    Task<TrackGuideFetchResult> RefreshGuideAsync(
        string trackName,
        CancellationToken cancellationToken = default);
}
