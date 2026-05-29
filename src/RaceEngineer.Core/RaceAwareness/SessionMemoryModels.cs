namespace RaceEngineer.Core.RaceAwareness;

public sealed record SessionMemorySummary(
    Guid SessionId,
    string TrackKey,
    string TrackName,
    string CarName,
    string? SessionType,
    DateTimeOffset RecordedAt,
    double? BestLapSeconds,
    double? AverageCleanLapSeconds,
    double? ConsistencyScore,
    double? FuelUsedPerLap,
    string? TyreWarmupNotes,
    string? TyreDegradationNotes,
    IReadOnlyList<string> BrakingWeaknesses,
    IReadOnlyList<string> ThrottleWeaknesses,
    IReadOnlyList<string> MainTimeLossZones,
    IReadOnlyList<string> Incidents,
    IReadOnlyList<string> StrategyNotes,
    IReadOnlyList<string> ImprovementTargets)
{
    public string OneLineSummary =>
        $"Stored session data ({RecordedAt:yyyy-MM-dd}): best {SessionMemoryFormatting.FormatLapTime(BestLapSeconds)}, " +
        $"avg {SessionMemoryFormatting.FormatLapTime(AverageCleanLapSeconds)}, " +
        $"fuel {SessionMemoryFormatting.FormatFuelPerLap(FuelUsedPerLap)}.";
}

public sealed record SessionDebrief(
    IReadOnlyList<string> Strengths,
    IReadOnlyList<string> Weaknesses,
    string FuelAnalysis,
    string TyreAnalysis,
    string BrakingAnalysis,
    string ThrottleAnalysis,
    string ConsistencyAnalysis,
    string StrategyNotes,
    IReadOnlyList<string> NextSessionActions,
    string Markdown);

public sealed record SessionMemoryBuildInput(
    string TrackName,
    string CarName,
    string? SessionType,
    Session.SessionState Session,
    Analytics.SessionTelemetryAnalytics? Analytics,
    Analytics.SessionLapIntelligence? LapIntelligence,
    Analytics.SessionTyreIntelligence? TyreIntelligence,
    Strategy.SessionStrategy? Strategy,
    Analytics.SessionDriverPerformance? DriverPerformance);

public static class SessionMemoryFormatting
{
    public static string FormatLapTime(double? seconds) =>
        TrackMemoryService.FormatLapTime(seconds);

    public static string FormatFuelPerLap(double? fuelPerLap) =>
        fuelPerLap is { } value
            ? $"{value.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture)} L/lap"
            : "unavailable";
}
