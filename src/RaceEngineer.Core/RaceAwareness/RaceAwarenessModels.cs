using RaceEngineer.Core.Telemetry;

namespace RaceEngineer.Core.RaceAwareness;

public enum RaceContextConfidence
{
    Unavailable,
    Partial,
    Good
}

public sealed record RaceAwarenessState(
    string? TrackName,
    string? CircuitId,
    string? CarName,
    string? CarClass,
    string? SessionType,
    int? CurrentLap,
    int? TotalLaps,
    int? Position,
    int? TotalCars,
    double? GapAheadSeconds,
    double? GapBehindSeconds,
    string? CarAhead,
    string? CarBehind,
    string? PitState,
    string? Flags,
    int? Sector,
    double? SessionTimeRemainingSeconds,
    int? LapsRemaining);

public sealed record RaceFieldDiagnostics(
    IReadOnlyList<string> PresentFields,
    IReadOnlyList<string> MissingFields,
    string Summary);

public sealed record LiveRaceContext(
    string? TrackName,
    string? CircuitId,
    string? CarName,
    string? CarClass,
    string? SessionType,
    int? CurrentLap,
    int? TotalLaps,
    int? Position,
    int? TotalCars,
    double? GapAheadSeconds,
    double? GapBehindSeconds,
    string? CarAhead,
    string? CarBehind,
    string? PitState,
    string? Flags,
    int? Sector,
    double? SessionTimeRemainingSeconds,
    int? LapsRemaining,
    RaceContextConfidence Confidence,
    RaceFieldDiagnostics Diagnostics)
{
    public static LiveRaceContext Unavailable(string reason) => new(
        null,
        null,
        null,
        null,
        null,
        null,
        null,
        null,
        null,
        null,
        null,
        null,
        null,
        null,
        null,
        null,
        null,
        null,
        RaceContextConfidence.Unavailable,
        new RaceFieldDiagnostics([], RaceContextService.AllTrackedFields, reason));
}

public sealed record TrackMemoryRecord(
    string TrackKey,
    string TrackName,
    string CarName,
    int SessionCount,
    double? BestLapSeconds,
    double? AverageCleanLapSeconds,
    double? ConsistencyScore,
    IReadOnlyList<string> BrakingWeaknesses,
    IReadOnlyList<string> ThrottleWeaknesses,
    string? TyreWarmupNotes,
    double? FuelUsedPerLap,
    double? IncidentRate,
    IReadOnlyList<string> StrategyNotes,
    IReadOnlyList<string> PerformanceWeaknessPatterns,
    string? PreviousRaceResult,
    DateTimeOffset? LastSessionAt,
    IReadOnlyList<string> SessionSummaries)
{
    public static TrackMemoryRecord Empty(string trackKey, string trackName, string carName) => new(
        trackKey,
        trackName,
        carName,
        0,
        null,
        null,
        null,
        [],
        [],
        null,
        null,
        null,
        [],
        [],
        null,
        null,
        []);
}

public sealed record TrackMemoryComparison(
    bool HasHistoricalData,
    string TrackName,
    string CarName,
    double? HistoricalBestLapSeconds,
    double? HistoricalAverageCleanLapSeconds,
    double? CurrentBestLapSeconds,
    double? CurrentAverageCleanLapSeconds,
    double? BestLapDeltaSeconds,
    double? AverageLapDeltaSeconds,
    double? HistoricalFuelUsedPerLap,
    double? CurrentFuelUsedPerLap,
    IReadOnlyList<string> HistoricalWeaknesses,
    IReadOnlyList<string> HistoricalStrategyNotes,
    string Summary);

public sealed record TrackMemoryInput(
    string TrackName,
    string CarName,
    Session.SessionState Session,
    Analytics.SessionTelemetryAnalytics? Analytics,
    Analytics.SessionLapIntelligence? LapIntelligence,
    Analytics.SessionTyreIntelligence? TyreIntelligence,
    Strategy.SessionStrategy? Strategy,
    string? SessionSummaryMarkdown = null,
    string? RaceResult = null,
    Analytics.SessionDriverPerformance? DriverPerformance = null);
