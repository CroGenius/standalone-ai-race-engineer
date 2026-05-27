namespace RaceEngineer.Core.TelemetryVisualization;

public sealed record TracePoint(double Progress, double Value, double? RawValue);

public sealed record LapTraceSeries(
    string Channel,
    int LapNumber,
    string Label,
    bool IsOverlay,
    IReadOnlyList<TracePoint> Points,
    string ColorHint);

public sealed record TraceRow(
    string Name,
    IReadOnlyList<LapTraceSeries> Series,
    double MinValue,
    double MaxValue);

public sealed record TimelineMarker(
    double Progress,
    string Label,
    string Category);

public sealed record TelemetryTimeline(
    string Title,
    int? CurrentLapNumber,
    int? BestLapNumber,
    int? SelectedLapNumber,
    double CursorProgress,
    IReadOnlyList<TraceRow> Rows,
    IReadOnlyList<TimelineMarker> Markers,
    string StatusText)
{
    public static TelemetryTimeline Empty { get; } = new(
        "Telemetry Traces",
        null,
        null,
        null,
        0,
        [],
        [],
        "Waiting for telemetry snapshots.");
}

public sealed record TelemetryTimelineInput(
    Session.SessionState Session,
    IReadOnlyList<Telemetry.TelemetrySnapshot> Snapshots,
    IReadOnlyList<Events.TelemetryEvent>? Events = null,
    int? SelectedLapNumber = null,
    double? CursorProgress = null);

public sealed class TelemetryTraceBuilderOptions
{
    public int SectorCount { get; init; } = 3;
    public int MaxPointsPerSeries { get; init; } = 120;
    public double BrakeCornerThreshold { get; init; } = 0.35;
}
