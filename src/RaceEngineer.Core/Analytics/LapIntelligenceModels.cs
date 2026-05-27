namespace RaceEngineer.Core.Analytics;

public sealed record LapComparisonMetric(
    int? BestLapNumber,
    int? SelectedLapNumber,
    double? BestLapSeconds,
    double? SelectedLapSeconds,
    double? DeltaSeconds,
    string Availability);

public sealed record SectorDeltaMetric(
    int SectorIndex,
    string SectorName,
    double? BestSectorSeconds,
    double? SelectedSectorSeconds,
    double? DeltaSeconds,
    string GainLossLabel);

public sealed record SectorDeltaAnalysis(
    IReadOnlyList<SectorDeltaMetric> Sectors,
    string Availability)
{
    public static SectorDeltaAnalysis Empty { get; } = new([], "Need telemetry snapshots.");
}

public sealed record TheoreticalBestLapMetric(
    double? TheoreticalSeconds,
    double? ActualBestSeconds,
    double? DeltaSeconds,
    string Availability)
{
    public static TheoreticalBestLapMetric Empty { get; } = new(null, null, null, "Need sector timing data.");
}

public sealed record CornerPhaseSummary(
    int EntrySamples,
    int ApexSamples,
    int ExitSamples,
    string Availability);

public sealed record CornerPhaseAnalysis(
    CornerPhaseSummary BestLap,
    CornerPhaseSummary SelectedLap,
    string Availability)
{
    public static CornerPhaseAnalysis Empty { get; } = new(
        new CornerPhaseSummary(0, 0, 0, "Need telemetry snapshots."),
        new CornerPhaseSummary(0, 0, 0, "Need telemetry snapshots."),
        "Need telemetry snapshots.");
}

public sealed record BrakePointConsistencyMetric(
    double? Score0To100,
    double? ProgressStandardDeviation,
    int LapsSampled,
    string Availability)
{
    public static BrakePointConsistencyMetric Empty { get; } = new(null, null, 0, "Need telemetry snapshots.");
}

public sealed record ThrottleApplicationComparison(
    double? BestLapExitThrottleAverage,
    double? SelectedLapExitThrottleAverage,
    double? Delta,
    string Availability)
{
    public static ThrottleApplicationComparison Empty { get; } = new(null, null, null, "Need telemetry snapshots.");
}

public sealed record PaceDecayMetric(
    double? FirstHalfAverageSeconds,
    double? SecondHalfAverageSeconds,
    double? DeltaSeconds,
    string TrendLabel,
    string Availability);

public sealed record FuelAdjustedPaceComparison(
    double? BestAdjustedSeconds,
    double? SelectedAdjustedSeconds,
    double? DeltaSeconds,
    string Availability);

public sealed record ConsistencyHeatmapCell(
    int LapNumber,
    int SectorIndex,
    double SectorSeconds,
    double DeviationFromBestSeconds,
    double Intensity0To100);

public sealed record ConsistencyHeatmapData(
    IReadOnlyList<ConsistencyHeatmapCell> Cells,
    int SectorCount,
    string Availability)
{
    public static ConsistencyHeatmapData Empty { get; } = new([], 3, "Need sector timing data.");
}

public sealed record LapCoachingInsight(
    string Category,
    string Message);

public sealed record SessionLapIntelligence(
    LapComparisonMetric LapComparison,
    SectorDeltaAnalysis SectorDeltas,
    TheoreticalBestLapMetric TheoreticalBest,
    CornerPhaseAnalysis CornerPhases,
    BrakePointConsistencyMetric BrakePointConsistency,
    ThrottleApplicationComparison ThrottleComparison,
    PaceDecayMetric PaceDecay,
    FuelAdjustedPaceComparison FuelAdjustedPace,
    ConsistencyHeatmapData ConsistencyHeatmap,
    IReadOnlyList<LapCoachingInsight> CoachingInsights,
    IReadOnlyList<string> Strengths,
    IReadOnlyList<string> Weaknesses)
{
    public static SessionLapIntelligence Empty { get; } = new(
        new LapComparisonMetric(null, null, null, null, null, "Need completed laps."),
        new SectorDeltaAnalysis([], "Need telemetry snapshots."),
        new TheoreticalBestLapMetric(null, null, null, "Need sector timing data."),
        new CornerPhaseAnalysis(
            new CornerPhaseSummary(0, 0, 0, "Need telemetry snapshots."),
            new CornerPhaseSummary(0, 0, 0, "Need telemetry snapshots."),
            "Need telemetry snapshots."),
        new BrakePointConsistencyMetric(null, null, 0, "Need telemetry snapshots."),
        new ThrottleApplicationComparison(null, null, null, "Need telemetry snapshots."),
        new PaceDecayMetric(null, null, null, "-", "Need at least 2 timed laps."),
        new FuelAdjustedPaceComparison(null, null, null, "Need fuel-adjusted lap samples."),
        new ConsistencyHeatmapData([], 3, "Need sector timing data."),
        [],
        [],
        []);
}

public sealed record LapIntelligenceInput(
    Session.SessionState Session,
    IReadOnlyList<Telemetry.TelemetrySnapshot>? Snapshots = null,
    int? SelectedLapNumber = null);

public sealed class LapIntelligenceOptions
{
    public int SectorCount { get; init; } = 3;
    public double SectorGainThresholdSeconds { get; init; } = 0.050;
    public double SectorLossThresholdSeconds { get; init; } = 0.150;
    public double PaceDecayThresholdSeconds { get; init; } = 0.250;
    public double FuelAdjustSecondsPerUnit { get; init; } = 0.150;
    public double BrakeOnThreshold { get; init; } = 0.40;
    public double HeatmapMaxDeviationSeconds { get; init; } = 0.500;
}
