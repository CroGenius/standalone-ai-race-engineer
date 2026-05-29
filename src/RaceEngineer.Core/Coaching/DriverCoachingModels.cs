using RaceEngineer.Core.Analytics;

namespace RaceEngineer.Core.Coaching;

public enum DriverProgressTrend
{
    Unavailable,
    Improving,
    Stable,
    Declining,
    Inconsistent
}

public sealed record DriverCoachingRecommendation(
    bool HasData,
    string Availability,
    DriverProgressTrend ProgressTrend,
    string ProgressTrendSummary,
    string? BiggestWeakness,
    string? StrongestArea,
    string? WeakestArea,
    string? ConsistencySummary,
    string? PreviousSessionDeltaSummary,
    IReadOnlyList<string> RepeatedWeaknesses,
    IReadOnlyList<string> TopCoachingTargets,
    IReadOnlyList<string> CoachingInsights,
    string Summary)
{
    public static DriverCoachingRecommendation Unavailable { get; } = new(
        false,
        SessionDriverPerformance.NeedCleanLapMessage,
        DriverProgressTrend.Unavailable,
        "Progress trend is unavailable until clean lap data exists.",
        null,
        null,
        null,
        null,
        null,
        [],
        [],
        [],
        Analytics.SessionDriverPerformance.NeedCleanLapMessage);
}

public sealed record DriverCoachingInput(
    Analytics.SessionDriverPerformance Performance,
    Analytics.SessionTelemetryAnalytics? Analytics = null,
    Analytics.SessionLapIntelligence? LapIntelligence = null,
    RaceAwareness.TrackMemoryComparison? TrackMemoryComparison = null,
    RaceAwareness.SessionMemorySummary? PreviousStoredSessionMemory = null,
    IReadOnlyList<RaceAwareness.SessionMemorySummary>? RecentStoredSessionMemories = null,
    Knowledge.TrackGuide? TrackGuide = null,
    RaceAwareness.TrackMemoryRecord? TrackMemory = null,
    string? CarClass = null);
