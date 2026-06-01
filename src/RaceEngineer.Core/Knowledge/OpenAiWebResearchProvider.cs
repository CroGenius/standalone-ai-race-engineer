namespace RaceEngineer.Core.Knowledge;

public sealed class OpenAiWebResearchProvider : IWebResearchProvider
{
    public string Name => "openai-placeholder";

    public bool CanFetchFromRemote => false;

    public Task<WebResearchFetchResult> FetchTrackResearchAsync(string trackName, CancellationToken cancellationToken = default) =>
        Task.FromResult(new WebResearchFetchResult(
            [],
            false,
            "OpenAI web research provider is not configured yet. Use WebSearchResearchProvider or manual notes."));

    public Task<WebResearchFetchResult> FetchTrackCarStrategyAsync(
        string trackName,
        string? carName,
        string? carClass,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(new WebResearchFetchResult(
            [],
            false,
            "OpenAI web research provider is not configured yet. Use WebSearchResearchProvider or manual notes."));
}
