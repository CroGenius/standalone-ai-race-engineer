using RaceEngineer.Core.Events;
using RaceEngineer.Core.Session;
using RaceEngineer.Core.Telemetry;

namespace RaceEngineer.Core.Analytics;

public sealed class TelemetryAnalyticsService
{
    private static readonly EventType[] IncidentEventTypes =
    [
        EventType.UnstableBraking,
        EventType.AbruptBrakeRelease,
        EventType.ThrottleHesitation,
        EventType.EarlyThrottleWithSteering,
        EventType.SteeringOveruse,
        EventType.TractionLoss,
        EventType.TyreOverheating,
        EventType.BrakeOverheating,
        EventType.LowFuel,
        EventType.InvalidLapOrFlags
    ];

    private readonly TelemetryAnalyticsOptions options;

    public TelemetryAnalyticsService(TelemetryAnalyticsOptions? options = null)
    {
        this.options = options ?? new TelemetryAnalyticsOptions();
    }

    public SessionTelemetryAnalytics Analyze(SessionAnalyticsInput input)
    {
        var session = input.Session;
        var events = input.Events ?? session.Events;
        var validLaps = session.CompletedLaps
            .Where(lap => lap.IsValid && lap.Duration.HasValue)
            .OrderBy(lap => lap.LapNumber)
            .ToArray();

        var lapConsistency = ComputeLapConsistency(validLaps);
        var fuelTrend = ComputeFuelTrend(session, validLaps);
        var brakeStability = ComputeBrakeStability(events, validLaps.Length, input.Snapshots);
        var throttleSmoothness = ComputeThrottleSmoothness(events, validLaps.Length, input.Snapshots);
        var paceTrend = ComputePaceTrend(validLaps);
        var incidents = ComputeIncidentSummary(events, validLaps.Length);
        var bestVsAverage = ComputeBestVsAverage(validLaps);
        var driverProfile = ComputeDriverProfile(lapConsistency, fuelTrend, brakeStability, throttleSmoothness, paceTrend, incidents);

        return new SessionTelemetryAnalytics(
            lapConsistency,
            fuelTrend,
            brakeStability,
            throttleSmoothness,
            paceTrend,
            incidents,
            bestVsAverage,
            driverProfile);
    }

    private LapConsistencyMetric ComputeLapConsistency(IReadOnlyList<CompletedLap> validLaps)
    {
        if (validLaps.Count < 2)
        {
            return new LapConsistencyMetric(null, null, validLaps.Count, "Need at least 2 timed laps.");
        }

        var seconds = validLaps.Select(lap => lap.Duration!.Value.TotalSeconds).ToArray();
        var stdDev = PopulationStandardDeviation(seconds);
        var mean = seconds.Average();
        var coefficient = mean > 0 ? stdDev / mean : 0;
        var score = Clamp(100.0 - (coefficient * 250.0), 0, 100);
        return new LapConsistencyMetric(Round(score), Round(stdDev), validLaps.Count, "Available");
    }

    private FuelTrendMetric ComputeFuelTrend(SessionState session, IReadOnlyList<CompletedLap> validLaps)
    {
        var fuelSamples = validLaps
            .Where(lap => lap.FuelUsed is > 0)
            .Select(lap => lap.FuelUsed!.Value)
            .ToArray();

        double? fuelPerLap = fuelSamples.Length > 0 ? Round(fuelSamples.Average()) : session.FuelUsedPerLap is { } perLap ? Round(perLap) : null;
        double? stintFuelUsed = fuelSamples.Length > 0 ? Round(fuelSamples.Sum()) : null;
        if (stintFuelUsed is null && validLaps.Count > 0)
        {
            var firstFuel = validLaps[0].FuelStart;
            var lastFuel = session.LatestFuelLevel ?? validLaps[^1].FuelEnd;
            if (firstFuel is { } start && lastFuel is { } end && start >= end)
            {
                stintFuelUsed = Round(start - end);
            }
        }

        var trendLabel = "Stable";
        var availability = fuelPerLap.HasValue ? "Available" : "No fuel samples yet.";
        if (fuelSamples.Length >= 4)
        {
            var recent = fuelSamples.TakeLast(3).Average();
            var prior = fuelSamples.Skip(fuelSamples.Length - 6).Take(3).Average();
            if (recent > prior + options.FuelTrendDeltaThreshold)
            {
                trendLabel = "Increasing consumption";
            }
            else if (recent < prior - options.FuelTrendDeltaThreshold)
            {
                trendLabel = "Improving efficiency";
            }
        }
        else if (fuelSamples.Length >= 2)
        {
            trendLabel = fuelSamples[^1] > fuelSamples[0] + options.FuelTrendDeltaThreshold
                ? "Increasing consumption"
                : fuelSamples[^1] < fuelSamples[0] - options.FuelTrendDeltaThreshold
                    ? "Improving efficiency"
                    : "Stable";
        }

        return new FuelTrendMetric(
            fuelPerLap,
            stintFuelUsed,
            session.EstimatedLapsRemaining is { } remaining ? Round(remaining) : null,
            trendLabel,
            availability);
    }

    private BrakeStabilityMetric ComputeBrakeStability(
        IReadOnlyList<TelemetryEvent> events,
        int validLapCount,
        IReadOnlyList<TelemetrySnapshot>? snapshots)
    {
        var unstableCount = events.Count(item => item.Type == EventType.UnstableBraking);
        var abruptCount = events.Count(item => item.Type == EventType.AbruptBrakeRelease);
        var lapsAnalyzed = Math.Max(validLapCount, 1);
        var eventScore = Clamp(100.0 - ((unstableCount * 10.0) + (abruptCount * 15.0)) / lapsAnalyzed, 0, 100);
        double? score = Round(eventScore);
        var availability = "Event-based estimate";

        if (snapshots is { Count: >= 5 })
        {
            var deltas = new List<double>();
            for (var index = 1; index < snapshots.Count; index++)
            {
                var previousBrake = snapshots[index - 1].Inputs.Brake;
                var currentBrake = snapshots[index].Inputs.Brake;
                if (previousBrake is >= 0.15 && currentBrake is >= 0.15)
                {
                    deltas.Add(Math.Abs(currentBrake.Value - previousBrake!.Value));
                }
            }

            if (deltas.Count >= 5)
            {
                score = Round(Clamp(100.0 - (deltas.Average() * 180.0), 0, 100));
                availability = "Snapshot + event analysis";
            }
        }

        if (validLapCount == 0 && unstableCount == 0 && abruptCount == 0)
        {
            availability = "No laps analyzed yet.";
            score = null;
        }

        return new BrakeStabilityMetric(score, unstableCount, abruptCount, validLapCount, availability);
    }

    private ThrottleSmoothnessMetric ComputeThrottleSmoothness(
        IReadOnlyList<TelemetryEvent> events,
        int validLapCount,
        IReadOnlyList<TelemetrySnapshot>? snapshots)
    {
        var hesitationCount = events.Count(item => item.Type == EventType.ThrottleHesitation);
        var earlyThrottleCount = events.Count(item => item.Type == EventType.EarlyThrottleWithSteering);
        var lapsAnalyzed = Math.Max(validLapCount, 1);
        var eventScore = Clamp(100.0 - ((hesitationCount * 8.0) + (earlyThrottleCount * 12.0)) / lapsAnalyzed, 0, 100);
        double? score = Round(eventScore);
        var availability = "Event-based estimate";

        if (snapshots is { Count: >= 5 })
        {
            var jerkyChanges = 0;
            var samples = 0;
            for (var index = 1; index < snapshots.Count; index++)
            {
                var previousThrottle = snapshots[index - 1].Inputs.Throttle;
                var currentThrottle = snapshots[index].Inputs.Throttle;
                if (previousThrottle is { } previous && currentThrottle is { } current && current >= 0.10)
                {
                    samples++;
                    if (Math.Abs(current - previous) >= 0.35)
                    {
                        jerkyChanges++;
                    }
                }
            }

            if (samples >= 5)
            {
                var jerkRate = jerkyChanges / (double)samples;
                score = Round(Clamp(100.0 - (jerkRate * 250.0), 0, 100));
                availability = "Snapshot + event analysis";
            }
        }

        if (validLapCount == 0 && hesitationCount == 0 && earlyThrottleCount == 0)
        {
            availability = "No laps analyzed yet.";
            score = null;
        }

        return new ThrottleSmoothnessMetric(score, hesitationCount, earlyThrottleCount, validLapCount, availability);
    }

    private PaceTrendMetric ComputePaceTrend(IReadOnlyList<CompletedLap> validLaps)
    {
        var window = options.PaceTrendLapWindow;
        if (validLaps.Count < window + 1)
        {
            return new PaceTrendMetric(null, window, "-", null, null, $"Need at least {window + 1} timed laps.");
        }

        var recent = validLaps.TakeLast(window).Select(lap => lap.Duration!.Value.TotalSeconds).Average();
        var prior = validLaps.Skip(validLaps.Count - (window * 2)).Take(window).Select(lap => lap.Duration!.Value.TotalSeconds).Average();
        var delta = recent - prior;
        var trendLabel = Math.Abs(delta) <= options.PaceStableDeltaSeconds
            ? "Stable"
            : delta < 0
                ? "Improving"
                : "Slowing";

        return new PaceTrendMetric(
            Round(delta),
            window,
            trendLabel,
            Round(recent),
            Round(prior),
            "Available");
    }

    private IncidentFrequencySummary ComputeIncidentSummary(IReadOnlyList<TelemetryEvent> events, int validLapCount)
    {
        var incidentEvents = events.Where(item => IncidentEventTypes.Contains(item.Type)).ToArray();
        if (incidentEvents.Length == 0)
        {
            return new IncidentFrequencySummary(
                new Dictionary<string, int>(),
                0,
                0,
                validLapCount == 0 ? "No incidents recorded." : "Available");
        }

        var counts = incidentEvents
            .GroupBy(item => item.Type.ToString())
            .OrderBy(group => group.Key, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.Count());
        var divisor = Math.Max(validLapCount, 1);
        return new IncidentFrequencySummary(
            counts,
            Round(incidentEvents.Length / (double)divisor),
            incidentEvents.Length,
            "Available");
    }

    private BestVsAverageDeltaMetric ComputeBestVsAverage(IReadOnlyList<CompletedLap> validLaps)
    {
        if (validLaps.Count == 0)
        {
            return new BestVsAverageDeltaMetric(null, null, null, "Need at least one timed lap.");
        }

        var seconds = validLaps.Select(lap => lap.Duration!.Value.TotalSeconds).ToArray();
        var best = seconds.Min();
        var average = seconds.Average();
        return new BestVsAverageDeltaMetric(Round(best), Round(average), Round(average - best), "Available");
    }

    private DriverProfileSummary ComputeDriverProfile(
        LapConsistencyMetric lapConsistency,
        FuelTrendMetric fuelTrend,
        BrakeStabilityMetric brakeStability,
        ThrottleSmoothnessMetric throttleSmoothness,
        PaceTrendMetric paceTrend,
        IncidentFrequencySummary incidents)
    {
        var strengths = new List<string>();
        var weaknesses = new List<string>();

        if (lapConsistency.Score0To100 is { } consistencyScore && consistencyScore >= options.ConsistencyStrongThreshold)
        {
            strengths.Add("Consistent lap times");
        }
        else if (lapConsistency.Score0To100 is { } weakConsistency && weakConsistency <= options.ConsistencyWeakThreshold)
        {
            weaknesses.Add("Lap time spread is high");
        }

        if (brakeStability.Score0To100 is { } brakeScore && brakeScore >= options.BrakeStrongThreshold)
        {
            strengths.Add("Stable braking");
        }
        else if (brakeStability.Score0To100 is { } weakBrake && weakBrake <= options.BrakeWeakThreshold)
        {
            weaknesses.Add("Brake release instability");
        }

        if (throttleSmoothness.Score0To100 is { } throttleScore && throttleScore >= options.ThrottleStrongThreshold)
        {
            strengths.Add("Smooth throttle application");
        }
        else if (throttleSmoothness.Score0To100 is { } weakThrottle && weakThrottle <= options.ThrottleWeakThreshold)
        {
            weaknesses.Add("Throttle hesitation or early application");
        }

        if (paceTrend.TrendLabel == "Improving")
        {
            strengths.Add("Recent pace is improving");
        }
        else if (paceTrend.TrendLabel == "Slowing")
        {
            weaknesses.Add("Recent pace is slowing");
        }

        if (fuelTrend.TrendLabel == "Improving efficiency")
        {
            strengths.Add("Fuel usage is improving");
        }
        else if (fuelTrend.TrendLabel == "Increasing consumption")
        {
            weaknesses.Add("Fuel consumption is rising");
        }

        if (incidents.IncidentsPerLap <= options.IncidentLowPerLapThreshold && incidents.TotalIncidents > 0)
        {
            strengths.Add("Low incident frequency");
        }
        else if (incidents.IncidentsPerLap >= options.IncidentHighPerLapThreshold)
        {
            weaknesses.Add("High incident frequency");
        }

        return new DriverProfileSummary(
            strengths.OrderBy(item => item, StringComparer.Ordinal).ToArray(),
            weaknesses.OrderBy(item => item, StringComparer.Ordinal).ToArray());
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
}
