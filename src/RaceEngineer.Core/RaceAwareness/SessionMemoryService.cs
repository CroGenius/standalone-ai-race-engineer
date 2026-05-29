using RaceEngineer.Core.Storage;

namespace RaceEngineer.Core.RaceAwareness;

public sealed class SessionMemoryService
{
    public SessionMemorySummary BuildSummary(SessionMemoryBuildInput input) =>
        SessionMemorySummaryBuilder.Build(input);

    public SessionDebrief GenerateDebrief(SessionMemorySummary summary, SessionMemoryBuildInput? input = null) =>
        SessionDebriefGenerator.Generate(summary, input);

    public async Task SaveSummaryAsync(
        StorageService storage,
        SessionMemorySummary summary,
        CancellationToken cancellationToken = default)
    {
        await storage.SaveSessionMemorySummaryAsync(summary, cancellationToken);
    }

    public Task<IReadOnlyList<SessionMemorySummary>> LoadRecentAsync(
        StorageService storage,
        string? track,
        string? car,
        int limit = 5,
        Guid? excludeSessionId = null,
        CancellationToken cancellationToken = default) =>
        storage.LoadRecentSessionMemorySummariesAsync(track, car, limit, excludeSessionId, cancellationToken);

    public async Task<SessionMemorySummary?> LoadLatestPreviousAsync(
        StorageService storage,
        string? track,
        string? car,
        Guid? currentSessionId = null,
        CancellationToken cancellationToken = default)
    {
        var recent = await LoadRecentAsync(storage, track, car, 1, currentSessionId, cancellationToken);
        return recent.FirstOrDefault();
    }

    public async Task<(TrackMemoryRecord TrackMemory, SessionMemorySummary Summary)> PersistSessionAsync(
        StorageService storage,
        TrackMemoryService trackMemoryService,
        SessionMemoryBuildInput input,
        string? sessionSummaryMarkdown = null,
        CancellationToken cancellationToken = default)
    {
        var summary = BuildSummary(input);
        await SaveSummaryAsync(storage, summary, cancellationToken);
        var trackMemory = await trackMemoryService.UpsertFromSessionAsync(
            storage,
            new TrackMemoryInput(
                input.TrackName,
                input.CarName,
                input.Session,
                input.Analytics,
                input.LapIntelligence,
                input.TyreIntelligence,
                input.Strategy,
                sessionSummaryMarkdown,
                RaceResult: null,
                DriverPerformance: input.DriverPerformance),
            cancellationToken);
        return (trackMemory, summary);
    }
}
