using RaceEngineer.Core.Session;
using RaceEngineer.Core.Telemetry;

namespace RaceEngineer.Core.Analytics;

public sealed class LapIntelligenceService
{
    private readonly LapIntelligenceOptions options;

    public LapIntelligenceService(LapIntelligenceOptions? options = null)
    {
        this.options = options ?? new LapIntelligenceOptions();
    }

    public SessionLapIntelligence Analyze(LapIntelligenceInput input)
    {
        var validLaps = input.Session.CompletedLaps
            .Where(lap => lap.IsValid && lap.Duration.HasValue)
            .OrderBy(lap => lap.LapNumber)
            .ToArray();

        if (validLaps.Length == 0)
        {
            return SessionLapIntelligence.Empty;
        }

        var bestLap = validLaps.OrderBy(lap => lap.Duration!.Value).First();
        var selectedLap = ResolveSelectedLap(validLaps, input.SelectedLapNumber);
        var lapComparison = BuildLapComparison(bestLap, selectedLap);
        var paceDecay = ComputePaceDecay(validLaps);
        var fuelAdjusted = ComputeFuelAdjustedPace(bestLap, selectedLap, validLaps);

        if (input.Snapshots is not { Count: >= 4 })
        {
            var insights = BuildCoachingInsights(
                lapComparison,
                SectorDeltaAnalysis.Empty,
                TheoreticalBestLapMetric.Empty,
                BrakePointConsistencyMetric.Empty,
                ThrottleApplicationComparison.Empty,
                paceDecay,
                fuelAdjusted);
            return new SessionLapIntelligence(
                lapComparison,
                SectorDeltaAnalysis.Empty,
                TheoreticalBestLapMetric.Empty,
                CornerPhaseAnalysis.Empty,
                BrakePointConsistencyMetric.Empty,
                ThrottleApplicationComparison.Empty,
                paceDecay,
                fuelAdjusted,
                ConsistencyHeatmapData.Empty,
                insights,
                ExtractStrengths(insights),
                ExtractWeaknesses(insights));
        }

        var lapSnapshots = GroupSnapshotsByLap(input.Snapshots, validLaps);
        var sectorTimes = BuildSectorTimesByLap(lapSnapshots, validLaps);
        var sectorDeltas = ComputeSectorDeltas(sectorTimes, bestLap.LapNumber, selectedLap.LapNumber);
        var theoreticalBest = ComputeTheoreticalBest(sectorTimes, bestLap);
        var cornerPhases = AnalyzeCornerPhases(lapSnapshots, bestLap.LapNumber, selectedLap.LapNumber);
        var brakeConsistency = ComputeBrakePointConsistency(lapSnapshots, validLaps);
        var throttleComparison = CompareThrottleApplication(lapSnapshots, bestLap.LapNumber, selectedLap.LapNumber);
        var heatmap = BuildConsistencyHeatmap(sectorTimes, validLaps);
        var coachingInsights = BuildCoachingInsights(
            lapComparison,
            sectorDeltas,
            theoreticalBest,
            brakeConsistency,
            throttleComparison,
            paceDecay,
            fuelAdjusted);

        return new SessionLapIntelligence(
            lapComparison,
            sectorDeltas,
            theoreticalBest,
            cornerPhases,
            brakeConsistency,
            throttleComparison,
            paceDecay,
            fuelAdjusted,
            heatmap,
            coachingInsights,
            ExtractStrengths(coachingInsights),
            ExtractWeaknesses(coachingInsights));
    }

    private static CompletedLap ResolveSelectedLap(IReadOnlyList<CompletedLap> validLaps, int? selectedLapNumber)
    {
        if (selectedLapNumber is { } lapNumber)
        {
            return validLaps.FirstOrDefault(lap => lap.LapNumber == lapNumber) ?? validLaps[^1];
        }

        return validLaps[^1];
    }

    private static LapComparisonMetric BuildLapComparison(CompletedLap bestLap, CompletedLap selectedLap)
    {
        var bestSeconds = bestLap.Duration!.Value.TotalSeconds;
        var selectedSeconds = selectedLap.Duration!.Value.TotalSeconds;
        return new LapComparisonMetric(
            bestLap.LapNumber,
            selectedLap.LapNumber,
            Round(bestSeconds),
            Round(selectedSeconds),
            Round(selectedSeconds - bestSeconds),
            "Available");
    }

    private SectorDeltaAnalysis ComputeSectorDeltas(
        IReadOnlyDictionary<int, double[]> sectorTimesByLap,
        int bestLapNumber,
        int selectedLapNumber)
    {
        if (!sectorTimesByLap.TryGetValue(bestLapNumber, out var bestSectors)
            || !sectorTimesByLap.TryGetValue(selectedLapNumber, out var selectedSectors))
        {
            return SectorDeltaAnalysis.Empty;
        }

        var sectors = new List<SectorDeltaMetric>();
        for (var index = 0; index < options.SectorCount; index++)
        {
            var best = bestSectors[index];
            var selected = selectedSectors[index];
            var delta = selected - best;
            sectors.Add(new SectorDeltaMetric(
                index + 1,
                SectorName(index),
                Round(best),
                Round(selected),
                Round(delta),
                ClassifySectorDelta(delta)));
        }

        return new SectorDeltaAnalysis(sectors, "Available");
    }

    private TheoreticalBestLapMetric ComputeTheoreticalBest(
        IReadOnlyDictionary<int, double[]> sectorTimesByLap,
        CompletedLap bestLap)
    {
        if (sectorTimesByLap.Count == 0)
        {
            return TheoreticalBestLapMetric.Empty;
        }

        var bestSectorTimes = new double[options.SectorCount];
        for (var sectorIndex = 0; sectorIndex < options.SectorCount; sectorIndex++)
        {
            bestSectorTimes[sectorIndex] = sectorTimesByLap.Values
                .Where(values => values.Length > sectorIndex)
                .Select(values => values[sectorIndex])
                .DefaultIfEmpty(double.PositiveInfinity)
                .Min();
        }

        if (bestSectorTimes.Any(double.IsPositiveInfinity))
        {
            return TheoreticalBestLapMetric.Empty;
        }

        var theoretical = bestSectorTimes.Sum();
        var actualBest = bestLap.Duration!.Value.TotalSeconds;
        return new TheoreticalBestLapMetric(
            Round(theoretical),
            Round(actualBest),
            Round(actualBest - theoretical),
            "Available");
    }

    private CornerPhaseAnalysis AnalyzeCornerPhases(
        IReadOnlyDictionary<int, IReadOnlyList<TelemetrySnapshot>> lapSnapshots,
        int bestLapNumber,
        int selectedLapNumber)
    {
        if (!lapSnapshots.TryGetValue(bestLapNumber, out var bestSnapshots)
            || !lapSnapshots.TryGetValue(selectedLapNumber, out var selectedSnapshots))
        {
            return CornerPhaseAnalysis.Empty;
        }

        return new CornerPhaseAnalysis(
            SummarizeCornerPhases(bestSnapshots),
            SummarizeCornerPhases(selectedSnapshots),
            "Available");
    }

    private static CornerPhaseSummary SummarizeCornerPhases(IReadOnlyList<TelemetrySnapshot> snapshots)
    {
        var ordered = OrderSnapshots(snapshots);
        if (ordered.Count == 0)
        {
            return new CornerPhaseSummary(0, 0, 0, "No samples.");
        }

        var entry = 0;
        var apex = 0;
        var exit = 0;
        for (var index = 0; index < ordered.Count; index++)
        {
            switch (ClassifyCornerPhase(ordered[index], index > 0 ? ordered[index - 1] : null))
            {
                case CornerPhaseKind.Entry:
                    entry++;
                    break;
                case CornerPhaseKind.Apex:
                    apex++;
                    break;
                case CornerPhaseKind.Exit:
                    exit++;
                    break;
            }
        }

        return new CornerPhaseSummary(entry, apex, exit, "Available");
    }

    private BrakePointConsistencyMetric ComputeBrakePointConsistency(
        IReadOnlyDictionary<int, IReadOnlyList<TelemetrySnapshot>> lapSnapshots,
        IReadOnlyList<CompletedLap> validLaps)
    {
        var brakeOnProgress = new List<double>();
        foreach (var lap in validLaps)
        {
            if (!lapSnapshots.TryGetValue(lap.LapNumber, out var snapshots))
            {
                continue;
            }

            var progress = FindFirstBrakeOnProgress(snapshots);
            if (progress.HasValue)
            {
                brakeOnProgress.Add(progress.Value);
            }
        }

        if (brakeOnProgress.Count < 2)
        {
            return new BrakePointConsistencyMetric(null, null, brakeOnProgress.Count, "Need at least 2 brake-point samples.");
        }

        var stdDev = PopulationStandardDeviation(brakeOnProgress);
        var score = Clamp(100.0 - (stdDev * 400.0), 0, 100);
        return new BrakePointConsistencyMetric(Round(score), Round(stdDev), brakeOnProgress.Count, "Available");
    }

    private ThrottleApplicationComparison CompareThrottleApplication(
        IReadOnlyDictionary<int, IReadOnlyList<TelemetrySnapshot>> lapSnapshots,
        int bestLapNumber,
        int selectedLapNumber)
    {
        if (!lapSnapshots.TryGetValue(bestLapNumber, out var bestSnapshots)
            || !lapSnapshots.TryGetValue(selectedLapNumber, out var selectedSnapshots))
        {
            return ThrottleApplicationComparison.Empty;
        }

        var bestAverage = AverageExitThrottle(bestSnapshots);
        var selectedAverage = AverageExitThrottle(selectedSnapshots);
        if (!bestAverage.HasValue || !selectedAverage.HasValue)
        {
            return new ThrottleApplicationComparison(null, null, null, "Need exit-phase throttle samples.");
        }

        return new ThrottleApplicationComparison(
            Round(bestAverage.Value),
            Round(selectedAverage.Value),
            Round(selectedAverage.Value - bestAverage.Value),
            "Available");
    }

    private static PaceDecayMetric ComputePaceDecay(IReadOnlyList<CompletedLap> validLaps)
    {
        if (validLaps.Count < 2)
        {
            return new PaceDecayMetric(null, null, null, "-", "Need at least 2 timed laps.");
        }

        var midpoint = validLaps.Count / 2;
        var firstHalf = validLaps.Take(midpoint).Select(lap => lap.Duration!.Value.TotalSeconds).Average();
        var secondHalf = validLaps.Skip(midpoint).Select(lap => lap.Duration!.Value.TotalSeconds).Average();
        var delta = secondHalf - firstHalf;
        var trend = Math.Abs(delta) <= 0.150
            ? "Stable"
            : delta > 0
                ? "Decaying"
                : "Improving";

        return new PaceDecayMetric(
            Round(firstHalf),
            Round(secondHalf),
            Round(delta),
            trend,
            "Available");
    }

    private FuelAdjustedPaceComparison ComputeFuelAdjustedPace(
        CompletedLap bestLap,
        CompletedLap selectedLap,
        IReadOnlyList<CompletedLap> validLaps)
    {
        var fuelSamples = validLaps.Where(lap => lap.FuelUsed is > 0).Select(lap => lap.FuelUsed!.Value).ToArray();
        if (fuelSamples.Length == 0)
        {
            return new FuelAdjustedPaceComparison(null, null, null, "Need fuel usage per lap.");
        }

        var baselineFuel = Median(fuelSamples);
        var bestAdjusted = AdjustLapTime(bestLap, baselineFuel);
        var selectedAdjusted = AdjustLapTime(selectedLap, baselineFuel);
        if (!bestAdjusted.HasValue || !selectedAdjusted.HasValue)
        {
            return new FuelAdjustedPaceComparison(null, null, null, "Need fuel usage per lap.");
        }

        return new FuelAdjustedPaceComparison(
            Round(bestAdjusted.Value),
            Round(selectedAdjusted.Value),
            Round(selectedAdjusted.Value - bestAdjusted.Value),
            "Available");
    }

    private ConsistencyHeatmapData BuildConsistencyHeatmap(
        IReadOnlyDictionary<int, double[]> sectorTimesByLap,
        IReadOnlyList<CompletedLap> validLaps)
    {
        if (sectorTimesByLap.Count == 0)
        {
            return ConsistencyHeatmapData.Empty;
        }

        var bestBySector = new double[options.SectorCount];
        for (var sectorIndex = 0; sectorIndex < options.SectorCount; sectorIndex++)
        {
            bestBySector[sectorIndex] = sectorTimesByLap.Values
                .Where(values => values.Length > sectorIndex)
                .Select(values => values[sectorIndex])
                .Min();
        }

        var cells = new List<ConsistencyHeatmapCell>();
        foreach (var lap in validLaps)
        {
            if (!sectorTimesByLap.TryGetValue(lap.LapNumber, out var sectorTimes))
            {
                continue;
            }

            for (var sectorIndex = 0; sectorIndex < sectorTimes.Length; sectorIndex++)
            {
                var deviation = sectorTimes[sectorIndex] - bestBySector[sectorIndex];
                var intensity = Clamp((deviation / options.HeatmapMaxDeviationSeconds) * 100.0, 0, 100);
                cells.Add(new ConsistencyHeatmapCell(
                    lap.LapNumber,
                    sectorIndex + 1,
                    Round(sectorTimes[sectorIndex]),
                    Round(deviation),
                    Round(intensity)));
            }
        }

        return new ConsistencyHeatmapData(cells, options.SectorCount, "Available");
    }

    private IReadOnlyList<LapCoachingInsight> BuildCoachingInsights(
        LapComparisonMetric lapComparison,
        SectorDeltaAnalysis sectorDeltas,
        TheoreticalBestLapMetric theoreticalBest,
        BrakePointConsistencyMetric brakeConsistency,
        ThrottleApplicationComparison throttleComparison,
        PaceDecayMetric paceDecay,
        FuelAdjustedPaceComparison fuelAdjusted)
    {
        var insights = new List<LapCoachingInsight>();

        if (lapComparison.DeltaSeconds is > 0.100)
        {
            insights.Add(new LapCoachingInsight(
                "weakness",
                $"Selected lap {lapComparison.SelectedLapNumber} is {lapComparison.DeltaSeconds:0.000}s slower than best lap {lapComparison.BestLapNumber}."));
        }
        else if (lapComparison.DeltaSeconds is <= 0.050)
        {
            insights.Add(new LapCoachingInsight(
                "strength",
                $"Selected lap {lapComparison.SelectedLapNumber} is within {Math.Max(lapComparison.DeltaSeconds ?? 0, 0):0.000}s of your best."));
        }

        foreach (var sector in sectorDeltas.Sectors)
        {
            if (sector.DeltaSeconds is { } sectorDelta && sectorDelta >= options.SectorLossThresholdSeconds)
            {
                insights.Add(new LapCoachingInsight(
                    "weakness",
                    $"{sector.SectorName} loses {sectorDelta:0.000}s versus your best lap."));
            }
            else if (sector.DeltaSeconds is { } gainDelta && gainDelta <= -options.SectorGainThresholdSeconds)
            {
                insights.Add(new LapCoachingInsight(
                    "strength",
                    $"{sector.SectorName} gains {Math.Abs(gainDelta):0.000}s versus your best lap."));
            }
        }

        if (theoreticalBest.DeltaSeconds is >= 0.100)
        {
            insights.Add(new LapCoachingInsight(
                "opportunity",
                $"Theoretical best is {theoreticalBest.TheoreticalSeconds:0.000}s, leaving {theoreticalBest.DeltaSeconds:0.000}s on the table versus your best lap."));
        }

        if (brakeConsistency.Score0To100 is <= 60)
        {
            insights.Add(new LapCoachingInsight(
                "weakness",
                "Brake point progress varies across laps; aim for repeatable turn-in markers."));
        }
        else if (brakeConsistency.Score0To100 is >= 80)
        {
            insights.Add(new LapCoachingInsight(
                "strength",
                "Brake points are consistent across sampled laps."));
        }

        if (throttleComparison.Delta is >= 0.08)
        {
            insights.Add(new LapCoachingInsight(
                "weakness",
                "Exit throttle application is lower than your best lap in sampled corners."));
        }
        else if (throttleComparison.Delta is <= -0.05)
        {
            insights.Add(new LapCoachingInsight(
                "strength",
                "Exit throttle application matches or exceeds your best lap."));
        }

        if (paceDecay.TrendLabel == "Decaying")
        {
            insights.Add(new LapCoachingInsight(
                "weakness",
                $"Stint pace is decaying by {paceDecay.DeltaSeconds:0.000}s from first half to second half."));
        }
        else if (paceDecay.TrendLabel == "Improving")
        {
            insights.Add(new LapCoachingInsight(
                "strength",
                $"Stint pace improved by {Math.Abs(paceDecay.DeltaSeconds ?? 0):0.000}s from first half to second half."));
        }

        if (fuelAdjusted.DeltaSeconds is > 0.100)
        {
            insights.Add(new LapCoachingInsight(
                "weakness",
                "After fuel adjustment, selected lap pace is slower than your best."));
        }

        return insights
            .OrderBy(item => item.Category, StringComparer.Ordinal)
            .ThenBy(item => item.Message, StringComparer.Ordinal)
            .ToArray();
    }

    private static IReadOnlyList<string> ExtractStrengths(IReadOnlyList<LapCoachingInsight> insights)
    {
        return insights
            .Where(item => item.Category is "strength")
            .Select(item => item.Message)
            .ToArray();
    }

    private static IReadOnlyList<string> ExtractWeaknesses(IReadOnlyList<LapCoachingInsight> insights)
    {
        return insights
            .Where(item => item.Category is "weakness" or "opportunity")
            .Select(item => item.Message)
            .ToArray();
    }

    private Dictionary<int, double[]> BuildSectorTimesByLap(
        IReadOnlyDictionary<int, IReadOnlyList<TelemetrySnapshot>> lapSnapshots,
        IReadOnlyList<CompletedLap> validLaps)
    {
        var sectorTimes = new Dictionary<int, double[]>();
        foreach (var lap in validLaps)
        {
            if (!lapSnapshots.TryGetValue(lap.LapNumber, out var snapshots))
            {
                continue;
            }

            var times = ComputeSectorTimes(snapshots);
            if (times is not null)
            {
                sectorTimes[lap.LapNumber] = times;
            }
        }

        return sectorTimes;
    }

    private double[]? ComputeSectorTimes(IReadOnlyList<TelemetrySnapshot> snapshots)
    {
        var ordered = OrderSnapshots(snapshots);
        if (ordered.Count < 2)
        {
            return null;
        }

        var minProgress = ordered[0].Lap.LapProgress!.Value;
        var maxProgress = ordered[^1].Lap.LapProgress!.Value;
        var progressRange = maxProgress - minProgress;
        if (progressRange < 0.10)
        {
            return null;
        }

        var boundaries = BuildSectorBoundaries();
        var boundaryTimes = new double[boundaries.Length];
        boundaryTimes[0] = ordered[0].Timestamp.ToUnixTimeMilliseconds();
        for (var index = 1; index < boundaries.Length; index++)
        {
            var targetProgress = minProgress + (boundaries[index] * progressRange);
            var interpolated = InterpolateTimestampMs(ordered, targetProgress);
            if (!interpolated.HasValue)
            {
                return null;
            }

            boundaryTimes[index] = interpolated.Value;
        }

        var sectorTimes = new double[options.SectorCount];
        for (var index = 0; index < options.SectorCount; index++)
        {
            sectorTimes[index] = (boundaryTimes[index + 1] - boundaryTimes[index]) / 1000.0;
            if (sectorTimes[index] <= 0)
            {
                return null;
            }
        }

        return sectorTimes;
    }

    private double[] BuildSectorBoundaries()
    {
        var boundaries = new double[options.SectorCount + 1];
        for (var index = 0; index <= options.SectorCount; index++)
        {
            boundaries[index] = index / (double)options.SectorCount;
        }

        return boundaries;
    }

    private string SectorName(int sectorIndex)
    {
        return $"Sector {sectorIndex + 1}";
    }

    private string ClassifySectorDelta(double delta)
    {
        if (delta <= -options.SectorGainThresholdSeconds)
        {
            return "Gain";
        }

        if (delta >= options.SectorLossThresholdSeconds)
        {
            return "Loss";
        }

        return "Neutral";
    }

    private static Dictionary<int, IReadOnlyList<TelemetrySnapshot>> GroupSnapshotsByLap(
        IReadOnlyList<TelemetrySnapshot> snapshots,
        IReadOnlyList<CompletedLap> validLaps)
    {
        var grouped = new Dictionary<int, List<TelemetrySnapshot>>();
        foreach (var snapshot in snapshots.OrderBy(item => item.Timestamp))
        {
            var lapNumber = snapshot.Lap.LapNumber;
            if (lapNumber is null)
            {
                lapNumber = validLaps.FirstOrDefault(lap => snapshot.Timestamp >= lap.StartedAt && snapshot.Timestamp <= lap.EndedAt)?.LapNumber;
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

    private static List<TelemetrySnapshot> OrderSnapshots(IReadOnlyList<TelemetrySnapshot> snapshots)
    {
        return snapshots
            .Where(item => item.Lap.LapProgress.HasValue)
            .OrderBy(item => item.Lap.LapProgress!.Value)
            .ToList();
    }

    private static double? InterpolateTimestampMs(IReadOnlyList<TelemetrySnapshot> ordered, double targetProgress)
    {
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

        if (Math.Abs(targetProgress - ordered[0].Lap.LapProgress!.Value) < 0.0001)
        {
            return ordered[0].Timestamp.ToUnixTimeMilliseconds();
        }

        var last = ordered[^1];
        if (Math.Abs(targetProgress - last.Lap.LapProgress!.Value) < 0.0001)
        {
            return last.Timestamp.ToUnixTimeMilliseconds();
        }

        return null;
    }

    private double? FindFirstBrakeOnProgress(IReadOnlyList<TelemetrySnapshot> snapshots)
    {
        foreach (var snapshot in OrderSnapshots(snapshots))
        {
            if (snapshot.Inputs.Brake is { } brake && brake >= options.BrakeOnThreshold)
            {
                return snapshot.Lap.LapProgress;
            }
        }

        return null;
    }

    private static double? AverageExitThrottle(IReadOnlyList<TelemetrySnapshot> snapshots)
    {
        var ordered = OrderSnapshots(snapshots);
        if (ordered.Count == 0)
        {
            return null;
        }

        var samples = new List<double>();
        for (var index = 0; index < ordered.Count; index++)
        {
            if (ClassifyCornerPhase(ordered[index], index > 0 ? ordered[index - 1] : null) == CornerPhaseKind.Exit
                && ordered[index].Inputs.Throttle is { } throttle)
            {
                samples.Add(throttle);
            }
        }

        return samples.Count == 0 ? null : samples.Average();
    }

    private static CornerPhaseKind ClassifyCornerPhase(TelemetrySnapshot snapshot, TelemetrySnapshot? previous)
    {
        var brake = snapshot.Inputs.Brake ?? 0;
        var throttle = snapshot.Inputs.Throttle ?? 0;
        var steering = Math.Abs(snapshot.Inputs.Steering ?? 0);
        var speed = snapshot.Car.SpeedKmh ?? 0;

        if (brake >= 0.35 && throttle <= 0.25)
        {
            return CornerPhaseKind.Entry;
        }

        if (brake <= 0.12 && steering >= 0.30 && speed >= 40)
        {
            return CornerPhaseKind.Apex;
        }

        if (throttle >= 0.45 && brake <= 0.10)
        {
            return CornerPhaseKind.Exit;
        }

        if (previous?.Car.SpeedKmh is { } previousSpeed
            && speed < previousSpeed - 5
            && brake >= 0.20)
        {
            return CornerPhaseKind.Entry;
        }

        return CornerPhaseKind.None;
    }

    private double? AdjustLapTime(CompletedLap lap, double baselineFuel)
    {
        if (!lap.Duration.HasValue || lap.FuelUsed is not { } fuelUsed)
        {
            return null;
        }

        return lap.Duration.Value.TotalSeconds + ((fuelUsed - baselineFuel) * options.FuelAdjustSecondsPerUnit);
    }

    private static double Median(IReadOnlyList<double> values)
    {
        var ordered = values.OrderBy(value => value).ToArray();
        var middle = ordered.Length / 2;
        return ordered.Length % 2 == 0
            ? (ordered[middle - 1] + ordered[middle]) / 2.0
            : ordered[middle];
    }

    private static double PopulationStandardDeviation(IReadOnlyList<double> values)
    {
        var mean = values.Average();
        var variance = values.Sum(value => (value - mean) * (value - mean)) / values.Count;
        return Math.Sqrt(variance);
    }

    private static double Clamp(double value, double min, double max)
    {
        return Math.Max(min, Math.Min(max, value));
    }

    private static double Round(double value)
    {
        return Math.Round(value, 3, MidpointRounding.AwayFromZero);
    }

    private enum CornerPhaseKind
    {
        None,
        Entry,
        Apex,
        Exit
    }
}
