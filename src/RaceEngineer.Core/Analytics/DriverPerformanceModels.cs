namespace RaceEngineer.Core.Analytics;

public sealed record PerformanceZone(
    int ZoneNumber,
    string Label,
    int SectorIndex,
    double ProgressStart,
    double ProgressEnd);

public sealed record ZonePerformanceMetric(
    PerformanceZone Zone,
    double? EstimatedLossSeconds,
    IReadOnlyList<string> Behaviors);

public sealed record PerformanceQualityMetric(
    string Label,
    double? Score0To100,
    string Detail,
    string Availability)
{
    public static PerformanceQualityMetric Empty(string label) =>
        new(label, null, "-", SessionDriverPerformance.NeedCleanLapMessage);
}

public sealed record MistakeCluster(
    string Behavior,
    int Count,
    string ZoneLabel);

public sealed record SessionDriverPerformance(
    string Availability,
    int ValidLapCount,
    int ZoneCount,
    ZonePerformanceMetric? BiggestTimeLoss,
    string? MainWeakness,
    PerformanceQualityMetric BrakingQuality,
    PerformanceQualityMetric ThrottleQuality,
    PerformanceQualityMetric Consistency,
    double? CurrentVsBestDeltaSeconds,
    string? StoredBaselineComparison,
    IReadOnlyList<ZonePerformanceMetric> ZoneMetrics,
    IReadOnlyList<MistakeCluster> MistakeClusters,
    IReadOnlyList<string> CoachingMessages)
{
    public const string NeedCleanLapMessage = "Need at least one clean lap.";

    public static SessionDriverPerformance Unavailable(string reason) => new(
        reason,
        0,
        0,
        null,
        null,
        PerformanceQualityMetric.Empty("Braking"),
        PerformanceQualityMetric.Empty("Throttle"),
        PerformanceQualityMetric.Empty("Consistency"),
        null,
        null,
        [],
        [],
        []);
}

public sealed record DriverPerformanceInput(
    Session.SessionState Session,
    IReadOnlyList<Telemetry.TelemetrySnapshot>? Snapshots = null,
    IReadOnlyList<Events.TelemetryEvent>? Events = null,
    SessionTelemetryAnalytics? Analytics = null,
    SessionLapIntelligence? LapIntelligence = null,
    RaceAwareness.TrackMemoryRecord? TrackMemory = null,
    RaceAwareness.TrackMemoryComparison? TrackMemoryComparison = null,
    int? SelectedLapNumber = null);
