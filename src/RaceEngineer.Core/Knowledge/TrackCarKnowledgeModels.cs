using RaceEngineer.Core.Analytics;
using RaceEngineer.Core.Coaching;
using RaceEngineer.Core.RaceAwareness;
using RaceEngineer.Core.Session;
using RaceEngineer.Core.Strategy;

namespace RaceEngineer.Core.Knowledge;

public enum TrackCarKnowledgeDataSource
{
    Unavailable,
    LiveTelemetry,
    StoredKnowledge
}

public sealed record TrackKnowledge(
    string TrackName,
    string FuelUsageExpectation,
    string TyreWearExpectation,
    string TyreWarmupExpectation,
    string BrakeDemand,
    string TopSpeedSensitivity,
    string DownforceSensitivity,
    string OvertakingDifficulty,
    IReadOnlyList<string> PitStrategyNotes,
    IReadOnlyList<string> SetupPriorities,
    IReadOnlyList<string> OvertakingZones);

public sealed record CarClassKnowledge(
    string ClassName,
    string FuelUsageExpectation,
    string TyreWearExpectation,
    string TyreWarmupExpectation,
    string BrakeDemand,
    string TopSpeedSensitivity,
    string DownforceSensitivity,
    string OvertakingDifficulty,
    IReadOnlyList<string> PitStrategyNotes,
    IReadOnlyList<string> SetupPriorities,
    double? BaselineFuelPerLapLiters,
    double FuelSafetyMarginLaps = 1.0);

public sealed record TrackCarKnowledge(
    string TrackName,
    string CarClass,
    string FuelUsageExpectation,
    string TyreWearExpectation,
    string TyreWarmupExpectation,
    string BrakeDemand,
    string TopSpeedSensitivity,
    string DownforceSensitivity,
    string OvertakingDifficulty,
    IReadOnlyList<string> PitStrategyNotes,
    IReadOnlyList<string> SetupPriorities,
    IReadOnlyList<string> OvertakingZones,
    double? BaselineFuelPerLapLiters,
    double FuelSafetyMarginLaps = 1.0);

public sealed record TrackCarKnowledgeRecommendation(
    string TrackName,
    string? CarName,
    string? CarClass,
    string FuelUsageExpectation,
    string TyreWearExpectation,
    string TyreWarmupExpectation,
    string BrakeDemand,
    string TopSpeedSensitivity,
    string DownforceSensitivity,
    string OvertakingDifficulty,
    IReadOnlyList<string> PitStrategyNotes,
    IReadOnlyList<string> SetupPriorities,
    IReadOnlyList<string> OvertakingZones,
    int? TargetLapCount,
    double? LiveFuelPerLapLiters,
    double? BaselineFuelPerLapLiters,
    double? RecommendedFuelLiters,
    TrackCarKnowledgeDataSource FuelSource,
    TrackCarKnowledgeDataSource OverallSource,
    string ConfidenceLabel,
    string SourceLabel,
    string? LiveFuelNote,
    string? KnowledgeFuelNote,
    string KnowledgeSummary)
{
    public bool IsAvailable => OverallSource != TrackCarKnowledgeDataSource.Unavailable;

    public bool HasFuelEstimate => LiveFuelPerLapLiters is > 0 || BaselineFuelPerLapLiters is > 0;

    public static TrackCarKnowledgeRecommendation Unavailable(string? track, string? car, string? carClass, string reason) =>
        new(
            track ?? "unknown",
            car,
            carClass,
            reason,
            reason,
            reason,
            reason,
            reason,
            reason,
            reason,
            [],
            [],
            [],
            null,
            null,
            null,
            null,
            TrackCarKnowledgeDataSource.Unavailable,
            TrackCarKnowledgeDataSource.Unavailable,
            "Unavailable",
            reason,
            null,
            null,
            reason);
}

public sealed record TrackCarKnowledgeInput(
    string? TrackName,
    string? CarName,
    string? CarClass,
    SessionState Session,
    SessionTelemetryAnalytics? Analytics,
    TrackMemoryRecord? TrackMemory,
    SessionMemorySummary? PreviousSessionMemory,
    int? RequestedLapCount = null);
