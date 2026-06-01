using RaceEngineer.Core.Analytics;
using RaceEngineer.Core.Events;
using RaceEngineer.Core.Session;
using RaceEngineer.Core.Telemetry;

namespace RaceEngineer.Core.TelemetryVisualization;

public sealed class TelemetryTraceBuilder
{
    private static readonly string[] ChannelOrder = ["Throttle", "Brake", "Steering", "Speed", "Delta"];

    private readonly TelemetryTraceBuilderOptions options;

    public TelemetryTraceBuilder(TelemetryTraceBuilderOptions? options = null)
    {
        this.options = options ?? new TelemetryTraceBuilderOptions();
    }

    public TelemetryTimeline Build(TelemetryTimelineInput input)
    {
        if (input.Snapshots.Count < 2)
        {
            return TelemetryTimeline.Empty with
            {
                CursorProgress = Clamp01(input.CursorProgress ?? 0),
                StatusText = "Need at least 2 telemetry snapshots."
            };
        }

        var validLaps = input.Session.CompletedLaps
            .Where(lap => lap.IsValid)
            .OrderBy(lap => lap.LapNumber)
            .ToArray();
        var bestLapNumber = input.Session.BestLap?.LapNumber;
        var selectedLapNumber = ResolveSelectedLapNumber(validLaps, input.SelectedLapNumber);
        var currentLapNumber = input.Session.CurrentLap;
        var grouped = GroupSnapshotsByLap(input.Snapshots, validLaps, currentLapNumber);
        if (grouped.Count == 0)
        {
            return TelemetryTimeline.Empty with { StatusText = "No lap-aligned snapshots available." };
        }

        var primaryLapNumber = selectedLapNumber ?? currentLapNumber;
        if (!grouped.ContainsKey(primaryLapNumber))
        {
            primaryLapNumber = grouped.Keys.Max();
        }

        var overlayLapNumber = bestLapNumber;
        if (overlayLapNumber is null || !grouped.ContainsKey(overlayLapNumber.Value))
        {
            overlayLapNumber = grouped.Keys
                .OrderBy(number => GetLapDuration(validLaps, number).TotalSeconds)
                .FirstOrDefault();
        }

        var rows = new List<TraceRow>();
        foreach (var channel in ChannelOrder)
        {
            if (channel == "Delta")
            {
                if (overlayLapNumber is { } best && grouped.TryGetValue(primaryLapNumber, out var selectedSnaps) && grouped.TryGetValue(best, out var bestSnaps))
                {
                    rows.Add(BuildDeltaRow(selectedSnaps, bestSnaps, primaryLapNumber, best));
                }

                continue;
            }

            var series = new List<LapTraceSeries>();
            if (grouped.TryGetValue(primaryLapNumber, out var primarySnapshots))
            {
                series.Add(BuildSeries(channel, primaryLapNumber, "Current", false, primarySnapshots));
            }

            if (overlayLapNumber is { } overlayLap && overlayLap != primaryLapNumber && grouped.TryGetValue(overlayLap, out var overlaySnapshots))
            {
                series.Add(BuildSeries(channel, overlayLap, "Best", true, overlaySnapshots));
            }

            if (series.Count > 0)
            {
                rows.Add(BuildRow(channel, series));
            }
        }

        var events = input.Events ?? input.Session.Events;
        var markers = BuildMarkers(grouped, primaryLapNumber, events);
        var cursor = ResolveCursorProgress(input, grouped, primaryLapNumber);
        var title = $"Telemetry Traces | Lap {primaryLapNumber}" +
                    (overlayLapNumber is { } bestOverlay && bestOverlay != primaryLapNumber ? $" vs best {bestOverlay}" : "");

        return new TelemetryTimeline(
            title,
            currentLapNumber,
            overlayLapNumber,
            selectedLapNumber,
            cursor,
            rows,
            markers,
            rows.Count == 0 ? "No trace rows generated." : "Available");
    }

    private TraceRow BuildRow(string channel, IReadOnlyList<LapTraceSeries> series)
    {
        var values = series.SelectMany(item => item.Points.Select(point => point.Value)).ToArray();
        var min = values.Length == 0 ? 0 : values.Min();
        var max = values.Length == 0 ? 1 : values.Max();
        if (Math.Abs(max - min) < 0.001)
        {
            max = min + 1;
        }

        return new TraceRow(channel, series, min, max);
    }

    private TraceRow BuildDeltaRow(
        IReadOnlyList<TelemetrySnapshot> selectedSnapshots,
        IReadOnlyList<TelemetrySnapshot> bestSnapshots,
        int selectedLapNumber,
        int bestLapNumber)
    {
        var selectedOrdered = NormalizeSnapshots(selectedSnapshots);
        var bestOrdered = NormalizeSnapshots(bestSnapshots);
        if (selectedOrdered.Count == 0 || bestOrdered.Count == 0)
        {
            return new TraceRow("Delta", [], 0, 1);
        }

        var selectedStartMs = selectedOrdered[0].Timestamp.ToUnixTimeMilliseconds();
        var bestStartMs = bestOrdered[0].Timestamp.ToUnixTimeMilliseconds();
        var selectedLapDuration = Math.Max(
            1,
            (selectedOrdered[^1].Timestamp.ToUnixTimeMilliseconds() - selectedStartMs) / 1000.0);
        var bestLapDuration = Math.Max(
            1,
            (bestOrdered[^1].Timestamp.ToUnixTimeMilliseconds() - bestStartMs) / 1000.0);
        var referenceDuration = Math.Max(selectedLapDuration, bestLapDuration);
        var points = new List<TracePoint>();
        for (var step = 0; step <= 40; step++)
        {
            var progress = step / 40.0;
            var selectedMs = InterpolateTimestampMs(selectedOrdered, progress);
            var bestMs = InterpolateTimestampMs(bestOrdered, progress);
            if (!selectedMs.HasValue || !bestMs.HasValue)
            {
                continue;
            }

            var selectedElapsed = (selectedMs.Value - selectedStartMs) / 1000.0;
            var bestElapsed = (bestMs.Value - bestStartMs) / 1000.0;
            var deltaSeconds = selectedElapsed - bestElapsed;
            if (!LapDeltaSanity.IsReasonableLapDelta(deltaSeconds, referenceDuration))
            {
                continue;
            }

            points.Add(new TracePoint(progress, deltaSeconds, deltaSeconds));
        }

        var series = new[]
        {
            new LapTraceSeries("Delta", selectedLapNumber, $"Δ vs lap {bestLapNumber}", false, Downsample(points), "#F5A524")
        };
        var values = points.Select(point => point.Value).ToArray();
        var maxAbs = values.Length == 0 ? 1 : Math.Max(Math.Abs(values.Min()), Math.Abs(values.Max()));
        return new TraceRow("Delta", series, -maxAbs, maxAbs);
    }

    private LapTraceSeries BuildSeries(
        string channel,
        int lapNumber,
        string label,
        bool isOverlay,
        IReadOnlyList<TelemetrySnapshot> snapshots)
    {
        var points = new List<TracePoint>();
        foreach (var snapshot in NormalizeSnapshots(snapshots))
        {
            var progress = snapshot.Lap.LapProgress ?? 0;
            var raw = ReadChannelValue(channel, snapshot);
            if (!raw.HasValue)
            {
                continue;
            }

            var normalized = channel switch
            {
                "Speed" => raw.Value,
                "Steering" => (raw.Value + 1.0) / 2.0,
                _ => raw.Value
            };
            points.Add(new TracePoint(progress, normalized, raw.Value));
        }

        var color = channel switch
        {
            "Throttle" => isOverlay ? "#66FF9A" : "#2ECC71",
            "Brake" => isOverlay ? "#FF7A7A" : "#E74C3C",
            "Steering" => isOverlay ? "#9FA8FF" : "#6C7CFF",
            "Speed" => isOverlay ? "#FFD27A" : "#F5A524",
            _ => isOverlay ? "#9AA6AC" : "#EFF3F4"
        };

        return new LapTraceSeries(channel, lapNumber, label, isOverlay, Downsample(points), color);
    }

    private IReadOnlyList<TimelineMarker> BuildMarkers(
        IReadOnlyDictionary<int, IReadOnlyList<TelemetrySnapshot>> grouped,
        int primaryLapNumber,
        IReadOnlyList<TelemetryEvent> events)
    {
        var markers = new List<TimelineMarker>();
        for (var sectorIndex = 1; sectorIndex < options.SectorCount; sectorIndex++)
        {
            var progress = sectorIndex / (double)options.SectorCount;
            markers.Add(new TimelineMarker(progress, $"S{sectorIndex + 1}", "Sector"));
        }

        if (grouped.TryGetValue(primaryLapNumber, out var snapshots))
        {
            var ordered = NormalizeSnapshots(snapshots);
            TelemetrySnapshot? previous = null;
            foreach (var snapshot in ordered)
            {
                var brake = snapshot.Inputs.Brake ?? 0;
                var previousBrake = previous?.Inputs.Brake ?? 0;
                if (brake >= options.BrakeCornerThreshold && previousBrake < options.BrakeCornerThreshold && snapshot.Lap.LapProgress is { } progress)
                {
                    markers.Add(new TimelineMarker(progress, "Corner", "Corner"));
                }

                previous = snapshot;
            }
        }

        foreach (var item in events)
        {
            if (item.LapNumber != primaryLapNumber || item.LapProgress is not { } progress)
            {
                continue;
            }

            if (item.Type is EventType.LapStart or EventType.LapEnd)
            {
                continue;
            }

            markers.Add(new TimelineMarker(NormalizeProgress(progress), item.Type.ToString(), "Event"));
        }

        return markers
            .OrderBy(marker => marker.Progress)
            .ThenBy(marker => marker.Category, StringComparer.Ordinal)
            .ThenBy(marker => marker.Label, StringComparer.Ordinal)
            .ToArray();
    }

    private static double ResolveCursorProgress(
        TelemetryTimelineInput input,
        IReadOnlyDictionary<int, IReadOnlyList<TelemetrySnapshot>> grouped,
        int primaryLapNumber)
    {
        if (input.CursorProgress is { } explicitCursor)
        {
            return Clamp01(explicitCursor);
        }

        var latest = input.Session.LatestSnapshot;
        if (latest?.Lap.LapNumber == primaryLapNumber && latest.Lap.LapProgress is { } latestProgress)
        {
            return Clamp01(NormalizeProgress(latestProgress));
        }

        if (grouped.TryGetValue(primaryLapNumber, out var snapshots))
        {
            var last = NormalizeSnapshots(snapshots).LastOrDefault();
            if (last?.Lap.LapProgress is { } progress)
            {
                return Clamp01(NormalizeProgress(progress));
            }
        }

        return 0;
    }

    private static int? ResolveSelectedLapNumber(IReadOnlyList<CompletedLap> validLaps, int? selectedLapNumber)
    {
        if (selectedLapNumber is { } lapNumber && validLaps.Any(lap => lap.LapNumber == lapNumber))
        {
            return lapNumber;
        }

        return validLaps.LastOrDefault()?.LapNumber;
    }

    private static double? ReadChannelValue(string channel, TelemetrySnapshot snapshot)
    {
        return channel switch
        {
            "Throttle" => snapshot.Inputs.Throttle,
            "Brake" => snapshot.Inputs.Brake,
            "Steering" => snapshot.Inputs.Steering,
            "Speed" => snapshot.Car.SpeedKmh,
            _ => null
        };
    }

    private List<TracePoint> Downsample(IReadOnlyList<TracePoint> points)
    {
        if (points.Count <= options.MaxPointsPerSeries)
        {
            return points.ToList();
        }

        var sampled = new List<TracePoint>(options.MaxPointsPerSeries);
        var step = (points.Count - 1) / (double)(options.MaxPointsPerSeries - 1);
        for (var index = 0; index < options.MaxPointsPerSeries; index++)
        {
            sampled.Add(points[(int)Math.Round(index * step)]);
        }

        return sampled;
    }

    private static List<TelemetrySnapshot> NormalizeSnapshots(IReadOnlyList<TelemetrySnapshot> snapshots)
    {
        var ordered = snapshots
            .Where(item => item.Lap.LapProgress.HasValue)
            .OrderBy(item => item.Lap.LapProgress!.Value)
            .ToList();
        if (ordered.Count == 0)
        {
            return ordered;
        }

        ordered = FilterWrapBleed(ordered);

        var min = NormalizeProgress(ordered[0].Lap.LapProgress!.Value);
        var max = NormalizeProgress(ordered[^1].Lap.LapProgress!.Value);
        if (min <= 0.15 && max >= 0.75)
        {
            return ordered
                .Select(item => item with
                {
                    Lap = item.Lap with { LapProgress = Clamp01(NormalizeProgress(item.Lap.LapProgress!.Value)) }
                })
                .ToList();
        }

        var range = Math.Max(max - min, 0.0001);
        return ordered
            .Select(item => item with
            {
                Lap = item.Lap with { LapProgress = Clamp01((NormalizeProgress(item.Lap.LapProgress!.Value) - min) / range) }
            })
            .ToList();
    }

    private static List<TelemetrySnapshot> FilterWrapBleed(IReadOnlyList<TelemetrySnapshot> ordered)
    {
        var normalized = ordered
            .Select(item => item with { Lap = item.Lap with { LapProgress = NormalizeProgress(item.Lap.LapProgress!.Value) } })
            .ToList();
        var hasNearFinish = normalized.Any(item => item.Lap.LapProgress >= 0.90);
        if (!hasNearFinish)
        {
            return normalized;
        }

        return normalized
            .Where(item => item.Lap.LapProgress > 0.10)
            .ToList();
    }

    private static Dictionary<int, IReadOnlyList<TelemetrySnapshot>> GroupSnapshotsByLap(
        IReadOnlyList<TelemetrySnapshot> snapshots,
        IReadOnlyList<CompletedLap> validLaps,
        int currentLapNumber)
    {
        var grouped = new Dictionary<int, List<TelemetrySnapshot>>();
        var lastCompletedEnd = validLaps.LastOrDefault()?.EndedAt;
        foreach (var snapshot in snapshots.OrderBy(item => item.Timestamp))
        {
            var lapNumber = snapshot.Lap.LapNumber;
            if (lapNumber is null)
            {
                lapNumber = validLaps.FirstOrDefault(lap => snapshot.Timestamp >= lap.StartedAt && snapshot.Timestamp <= lap.EndedAt)?.LapNumber;
            }

            if (lapNumber is null && lastCompletedEnd is null)
            {
                lapNumber = Math.Max(currentLapNumber, 1);
            }
            else if (lapNumber is null && snapshot.Timestamp >= lastCompletedEnd)
            {
                lapNumber = currentLapNumber;
            }

            if (lapNumber is null)
            {
                continue;
            }

            if (!grouped.TryGetValue(lapNumber.Value, out var list))
            {
                list = [];
                grouped[lapNumber.Value] = list;
            }

            list.Add(snapshot);
        }

        return grouped.ToDictionary(pair => pair.Key, pair => (IReadOnlyList<TelemetrySnapshot>)pair.Value);
    }

    private static double? InterpolateTimestampMs(IReadOnlyList<TelemetrySnapshot> ordered, double targetProgress)
    {
        if (ordered.Count == 0)
        {
            return null;
        }

        for (var index = 1; index < ordered.Count; index++)
        {
            var previous = ordered[index - 1];
            var current = ordered[index];
            var previousProgress = previous.Lap.LapProgress!.Value;
            var currentProgress = current.Lap.LapProgress!.Value;
            if (targetProgress < previousProgress || targetProgress > currentProgress)
            {
                continue;
            }

            if (Math.Abs(currentProgress - previousProgress) < 0.0001)
            {
                return current.Timestamp.ToUnixTimeMilliseconds();
            }

            var ratio = (targetProgress - previousProgress) / (currentProgress - previousProgress);
            var previousMs = previous.Timestamp.ToUnixTimeMilliseconds();
            var currentMs = current.Timestamp.ToUnixTimeMilliseconds();
            return previousMs + ((currentMs - previousMs) * ratio);
        }

        return ordered[0].Timestamp.ToUnixTimeMilliseconds();
    }

    private static TimeSpan GetLapDuration(IReadOnlyList<CompletedLap> validLaps, int lapNumber)
    {
        return validLaps.FirstOrDefault(lap => lap.LapNumber == lapNumber)?.Duration ?? TimeSpan.MaxValue;
    }

    private static double NormalizeProgress(double progress)
    {
        return progress > 1.0 && progress <= 100.0 ? progress / 100.0 : Clamp01(progress);
    }

    private static double Clamp01(double value)
    {
        return Math.Max(0, Math.Min(1, value));
    }
}
