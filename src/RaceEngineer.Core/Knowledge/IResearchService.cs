namespace RaceEngineer.Core.Knowledge;

public interface IResearchService
{
    Task<IReadOnlyList<KnowledgeSource>> SearchAsync(string query, CancellationToken cancellationToken = default);

    Task<ResearchBrief> GetTrackBriefAsync(string trackName, CancellationToken cancellationToken = default);

    Task<ResearchBrief> GetCarBriefAsync(string carName, CancellationToken cancellationToken = default);

    Task<ResearchBrief> GetSetupNotesAsync(string carName, string trackName, CancellationToken cancellationToken = default);

    Task<ResearchBrief> GetStrategyNotesAsync(string trackName, string sessionType, CancellationToken cancellationToken = default);
}

