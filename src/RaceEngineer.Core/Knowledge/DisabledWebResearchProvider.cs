namespace RaceEngineer.Core.Knowledge;

public sealed class DisabledWebResearchProvider : IWebResearchProvider
{
    public static DisabledWebResearchProvider Instance { get; } = new();

    public string Name => "disabled";

    public bool CanFetchFromRemote => false;

    public Task<WebResearchFetchResult> FetchTrackResearchAsync(string trackName, CancellationToken cancellationToken = default) =>
        Task.FromResult(new WebResearchFetchResult([], false, "Web research is disabled in settings."));

    public Task<WebResearchFetchResult> FetchTrackCarStrategyAsync(
        string trackName,
        string? carName,
        string? carClass,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(new WebResearchFetchResult([], false, "Web research is disabled in settings."));
}
