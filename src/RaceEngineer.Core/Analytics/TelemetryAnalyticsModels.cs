namespace RaceEngineer.Core.Analytics;

public sealed record LapConsistencyMetric(
    double? Score0To100,
    double? StandardDeviationSeconds,
    int SampleCount,
    string Availability);

public sealed record FuelTrendMetric(
    double? FuelPerLap,
    double? EstimatedStintFuelUsed,
    double? EstimatedLapsRemaining,
    string TrendLabel,
    string Availability);

public sealed record BrakeStabilityMetric(
    double? Score0To100,
    int UnstableBrakingCount,
    int AbruptReleaseCount,
    int LapsAnalyzed,
    string Availability);

public sealed record ThrottleSmoothnessMetric(
    double? Score0To100,
    int HesitationCount,
    int EarlyThrottleCount,
    int LapsAnalyzed,
    string Availability);

public sealed record PaceTrendMetric(
    double? DeltaSeconds,
    int LapWindow,
    string TrendLabel,
    double? RecentAverageSeconds,
    double? PriorAverageSeconds,
    string Availability);

public sealed record IncidentFrequencySummary(
    IReadOnlyDictionary<string, int> CountsByType,
    double IncidentsPerLap,
    int TotalIncidents,
    string Availability);

public sealed record BestVsAverageDeltaMetric(
    double? BestLapSeconds,
    double? AverageLapSeconds,
    double? DeltaSeconds,
    string Availability);

public sealed record DriverProfileSummary(
    IReadOnlyList<string> Strengths,
    IReadOnlyList<string> Weaknesses);

public sealed record SessionTelemetryAnalytics(
    LapConsistencyMetric LapConsistency,
    FuelTrendMetric FuelTrend,
    BrakeStabilityMetric BrakeStability,
    ThrottleSmoothnessMetric ThrottleSmoothness,
    PaceTrendMetric PaceTrend,
    IncidentFrequencySummary Incidents,
    BestVsAverageDeltaMetric BestVsAverage,
    DriverProfileSummary DriverProfile)
{
    public static SessionTelemetryAnalytics Empty { get; } = new(
        new LapConsistencyMetric(null, null, 0, "No completed laps yet."),
        new FuelTrendMetric(null, null, null, "-", "No fuel samples yet."),
        new BrakeStabilityMetric(null, 0, 0, 0, "No laps analyzed yet."),
        new ThrottleSmoothnessMetric(null, 0, 0, 0, "No laps analyzed yet."),
        new PaceTrendMetric(null, 5, "-", null, null, "Need more completed laps."),
        new IncidentFrequencySummary(new Dictionary<string, int>(), 0, 0, "No incidents recorded."),
        new BestVsAverageDeltaMetric(null, null, null, "Need at least one timed lap."),
        new DriverProfileSummary([], []));
}

public sealed record SessionAnalyticsInput(
    Session.SessionState Session,
    IReadOnlyList<Telemetry.TelemetrySnapshot>? Snapshots = null,
    IReadOnlyList<Events.TelemetryEvent>? Events = null);

public sealed class TelemetryAnalyticsOptions
{
    public int PaceTrendLapWindow { get; init; } = 5;
    public double PaceStableDeltaSeconds { get; init; } = 0.300;
    public double FuelTrendDeltaThreshold { get; init; } = 0.050;
    public double ConsistencyStrongThreshold { get; init; } = 75;
    public double ConsistencyWeakThreshold { get; init; } = 50;
    public double BrakeStrongThreshold { get; init; } = 75;
    public double BrakeWeakThreshold { get; init; } = 55;
    public double ThrottleStrongThreshold { get; init; } = 75;
    public double ThrottleWeakThreshold { get; init; } = 55;
    public double IncidentLowPerLapThreshold { get; init; } = 0.75;
    public double IncidentHighPerLapThreshold { get; init; } = 2.0;
}
