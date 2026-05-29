using RaceEngineer.Core.Analytics;
using RaceEngineer.Core.Coaching;
using RaceEngineer.Core.Knowledge;
using RaceEngineer.Core.Profile;
using RaceEngineer.Core.RaceAwareness;
using RaceEngineer.Core.Session;

namespace RaceEngineer.Core.Strategy;

public enum StrategyKnowledgeDataSource
{
    Unavailable,
    LiveTelemetry,
    StoredCar,
    StoredClass,
    CachedTrackGuide,
    GenericClass
}

public sealed record StrategyKnowledgeRecommendation(
    string TrackName,
    string? CarName,
    string? CarClass,
    string? SessionType,
    int? TargetLapCount,
    double? ExpectedFuelPerLap,
    double? RecommendedStartingFuelLiters,
    double? FuelSafetyMarginLiters,
    string? TyreWarmupEstimate,
    string? TyreDegradationRisk,
    string? PitWindowEstimate,
    string? OvertakingDifficulty,
    string? BrakingStress,
    string? TractionStress,
    IReadOnlyList<string> SetupNotes,
    IReadOnlyList<string> DrivingPriorities,
    StrategyKnowledgeDataSource FuelPerLapSource,
    StrategyKnowledgeDataSource OverallSource,
    string ConfidenceLabel,
    string SourceLabel,
    string? ClassBaselineNote,
    string? LiveFuelNote,
    string? FuelForLapsNote)
{
    public static StrategyKnowledgeRecommendation Unavailable(string track, string? car, string? carClass, string reason) =>
        new(
            track,
            car,
            carClass,
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
            [],
            [],
            StrategyKnowledgeDataSource.Unavailable,
            StrategyKnowledgeDataSource.Unavailable,
            "Unavailable",
            reason,
            null,
            null,
            null);

    public bool HasFuelEstimate => ExpectedFuelPerLap is > 0;
}

public sealed record StrategyKnowledgeInput(
    string? TrackName,
    string? CarName,
    string? CarClass,
    string? SessionType,
    int? RaceLapCount,
    SessionState Session,
    SessionTelemetryAnalytics? Analytics,
    SessionStrategy? Strategy,
    SessionTyreIntelligence? TyreIntelligence,
    TrackMemoryRecord? TrackMemory,
    SessionMemorySummary? PreviousSessionMemory,
    TrackGuide? TrackGuide,
    RacePrepPlan? PrepPlan = null,
    double FuelSafetyMarginLaps = 1.0,
    int? RequestedLapCount = null);

public static class StrategyLapCountParser
{
    public static int? Parse(string? query, LiveRaceContext? raceContext, RacePrepPlan? prepPlan)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return raceContext?.TotalLaps ?? raceContext?.LapsRemaining;
        }

        var text = query.ToLowerInvariant();
        var match = System.Text.RegularExpressions.Regex.Match(
            text,
            @"(?:for|za)\s*(\d{1,3})\s*(?:laps?|krugova|krug|kruz|kola|rounds?)");
        if (match.Success && int.TryParse(match.Groups[1].Value, out var parsed))
        {
            return parsed;
        }

        if (text.Contains("utrku", StringComparison.Ordinal) || text.Contains("the race", StringComparison.Ordinal))
        {
            return raceContext?.TotalLaps ?? raceContext?.LapsRemaining ?? ParseTargetStintLaps(prepPlan?.TargetStintLength);
        }

        return raceContext?.TotalLaps ?? raceContext?.LapsRemaining;
    }

    private static int? ParseTargetStintLaps(string? targetStint)
    {
        if (string.IsNullOrWhiteSpace(targetStint))
        {
            return null;
        }

        var match = System.Text.RegularExpressions.Regex.Match(targetStint, @"(\d{1,3})");
        return match.Success && int.TryParse(match.Groups[1].Value, out var laps) ? laps : null;
    }
}

public static class StrategyKnowledgeFormatting
{
    public static string FormatLiters(double? value) =>
        value is { } liters
            ? $"{liters.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture)} L"
            : "unavailable";

    public static string FormatSourceLabel(StrategyKnowledgeDataSource source) =>
        source switch
        {
            StrategyKnowledgeDataSource.LiveTelemetry => "live telemetry",
            StrategyKnowledgeDataSource.StoredCar => "stored car history",
            StrategyKnowledgeDataSource.StoredClass => "stored class history",
            StrategyKnowledgeDataSource.CachedTrackGuide => "cached track guide",
            StrategyKnowledgeDataSource.GenericClass => "generic class baseline",
            _ => "unavailable"
        };
}
