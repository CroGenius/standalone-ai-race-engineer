using System.Globalization;
using RaceEngineer.Core.Analytics;
using RaceEngineer.Core.Knowledge;
using RaceEngineer.Core.RaceAwareness;

namespace RaceEngineer.Core.Coaching;

public static class DriverCoachingIntelligenceService
{
    private const double TrendThresholdSeconds = 0.15;
    private const double WeakConsistencyThreshold = 50;

    public static DriverCoachingRecommendation Build(DriverCoachingInput input)
    {
        var performance = input.Performance;
        if (performance.ValidLapCount == 0 || performance.Availability != "Available")
        {
            return DriverCoachingRecommendation.Unavailable;
        }

        var mappedZones = performance.ZoneMetrics
            .Select(metric => metric with { Zone = metric.Zone with { Label = FormatZoneLabel(metric.Zone, input.TrackGuide) } })
            .ToArray();
        var biggestLoss = mappedZones
            .OrderByDescending(metric => metric.EstimatedLossSeconds ?? 0)
            .FirstOrDefault(metric => metric.EstimatedLossSeconds is > 0.030);
        var strongest = ResolveStrongestArea(mappedZones, input.LapIntelligence, input.TrackGuide);
        var weakest = biggestLoss is not null
            ? $"{biggestLoss.Zone.Label}: {ShortBehavior(biggestLoss.Behaviors)}"
            : performance.MainWeakness;
        var repeatedWeaknesses = BuildRepeatedWeaknesses(performance, input.TrackMemory, input.PreviousStoredSessionMemory);
        var progress = DetectProgressTrend(input);
        var previousDelta = BuildPreviousSessionDeltaSummary(input);
        var targets = BuildTopCoachingTargets(performance, mappedZones, repeatedWeaknesses, input.TrackGuide);
        var insights = BuildCoachingInsights(performance, mappedZones, progress, previousDelta, targets, input.TrackGuide);
        var summary = BuildSummary(progress, weakest, strongest, performance.Consistency.Detail, previousDelta);

        return new DriverCoachingRecommendation(
            true,
            performance.Availability,
            progress.Trend,
            progress.Summary,
            performance.MainWeakness ?? weakest,
            strongest,
            weakest,
            performance.Consistency.Detail,
            previousDelta,
            repeatedWeaknesses,
            targets,
            insights,
            summary);
    }

    public static string FormatZoneLabel(PerformanceZone zone, TrackGuide? guide)
    {
        if (guide?.Corners is { Count: > 0 })
        {
            var mid = (zone.ProgressStart + zone.ProgressEnd) / 2.0;
            foreach (var corner in guide.Corners)
            {
                if (corner.LapProgressStart is { } start
                    && corner.LapProgressEnd is { } end
                    && mid >= start
                    && mid <= end)
                {
                    return corner.Name;
                }
            }
        }

        return zone.Label;
    }

    private static string? ResolveStrongestArea(
        IReadOnlyList<ZonePerformanceMetric> zones,
        SessionLapIntelligence? lapIntelligence,
        TrackGuide? guide)
    {
        var bestZone = zones
            .Where(metric => metric.EstimatedLossSeconds is <= 0.010 or null)
            .OrderBy(metric => metric.EstimatedLossSeconds ?? 0)
            .FirstOrDefault();
        if (bestZone is not null)
        {
            return bestZone.Zone.Label;
        }

        var bestSector = lapIntelligence?.SectorDeltas.Sectors
            .Where(sector => sector.DeltaSeconds is < 0)
            .OrderBy(sector => sector.DeltaSeconds)
            .FirstOrDefault();
        if (bestSector is not null)
        {
            return FormatSectorName(bestSector.SectorName, guide);
        }

        return lapIntelligence?.Strengths.FirstOrDefault();
    }

    private static IReadOnlyList<string> BuildRepeatedWeaknesses(
        SessionDriverPerformance performance,
        TrackMemoryRecord? trackMemory,
        SessionMemorySummary? previousSession)
    {
        var items = new List<string>();
        items.AddRange(performance.MistakeClusters
            .Where(cluster => cluster.Count >= 2)
            .Select(cluster => $"{cluster.Behavior} in {cluster.ZoneLabel}"));
        items.AddRange(trackMemory?.RepeatedWeaknesses ?? []);
        items.AddRange(previousSession?.RepeatedWeaknesses ?? []);
        items.AddRange(trackMemory?.PerformanceWeaknessPatterns ?? []);

        return Distinct(items).Take(5).ToArray();
    }

    private static (DriverProgressTrend Trend, string Summary) DetectProgressTrend(DriverCoachingInput input)
    {
        var performance = input.Performance;
        var currentAverage = AverageCleanLapSeconds(performance, input.Analytics);
        var consistencyScore = performance.Consistency.Score0To100 ?? input.Analytics?.LapConsistency.Score0To100;

        if (consistencyScore is <= WeakConsistencyThreshold && performance.ValidLapCount >= 2)
        {
            return (DriverProgressTrend.Inconsistent, "Consistency is inconsistent; lap-to-lap variation is still high.");
        }

        double? delta = null;
        if (input.PreviousStoredSessionMemory?.AverageCleanLapSeconds is { } previousAverage
            && currentAverage is { } currentAvg)
        {
            delta = currentAvg - previousAverage;
        }
        else if (input.TrackMemoryComparison is { HasHistoricalData: true, AverageLapDeltaSeconds: { } comparisonDelta })
        {
            delta = comparisonDelta;
        }
        else if (input.Analytics?.PaceTrend.DeltaSeconds is { } paceDelta)
        {
            delta = paceDelta;
        }

        if (delta is < -TrendThresholdSeconds)
        {
            return (DriverProgressTrend.Improving, BuildTrendSummary("improving", delta, input.PreviousStoredSessionMemory, input.TrackMemory));
        }

        if (delta is > TrendThresholdSeconds)
        {
            return (DriverProgressTrend.Declining, BuildTrendSummary("declining", delta, input.PreviousStoredSessionMemory, input.TrackMemory));
        }

        if (input.Analytics?.PaceTrend.TrendLabel.Contains("improv", StringComparison.OrdinalIgnoreCase) == true)
        {
            return (DriverProgressTrend.Improving, "Pace trend is improving across recent laps.");
        }

        if (input.Analytics?.PaceTrend.TrendLabel.Contains("declin", StringComparison.OrdinalIgnoreCase) == true
            || input.Analytics?.PaceTrend.TrendLabel.Contains("slower", StringComparison.OrdinalIgnoreCase) == true)
        {
            return (DriverProgressTrend.Declining, "Pace trend is declining across recent laps.");
        }

        return (DriverProgressTrend.Stable, "Progress is stable versus your recent baseline.");
    }

    private static string? BuildPreviousSessionDeltaSummary(DriverCoachingInput input)
    {
        var currentAverage = AverageCleanLapSeconds(input.Performance, input.Analytics);
        if (input.PreviousStoredSessionMemory?.AverageCleanLapSeconds is { } previousAverage && currentAverage is { } currentAvg)
        {
            var delta = currentAvg - previousAverage;
            if (Math.Abs(delta) <= 0.010)
            {
                return $"You are matching your previous {input.PreviousStoredSessionMemory.TrackName} average.";
            }

            var direction = delta < 0 ? "faster" : "slower";
            return $"You are {Math.Abs(delta).ToString("0.0", CultureInfo.InvariantCulture)}s {direction} than your previous {input.PreviousStoredSessionMemory.TrackName} average.";
        }

        if (!string.IsNullOrWhiteSpace(input.Performance.StoredBaselineComparison))
        {
            return input.Performance.StoredBaselineComparison;
        }

        if (input.TrackMemoryComparison is { HasHistoricalData: true, BestLapDeltaSeconds: { } bestDelta })
        {
            var direction = bestDelta < -0.05 ? "faster than" : bestDelta > 0.05 ? "slower than" : "matching";
            return $"Current best is {direction} stored baseline by {Math.Abs(bestDelta).ToString("0.000", CultureInfo.InvariantCulture)}s.";
        }

        return null;
    }

    private static IReadOnlyList<string> BuildTopCoachingTargets(
        SessionDriverPerformance performance,
        IReadOnlyList<ZonePerformanceMetric> mappedZones,
        IReadOnlyList<string> repeatedWeaknesses,
        TrackGuide? guide)
    {
        var targets = new List<string>();
        if (!string.IsNullOrWhiteSpace(performance.MainWeakness))
        {
            targets.Add(performance.MainWeakness);
        }

        foreach (var zone in mappedZones.Where(metric => metric.EstimatedLossSeconds is > 0.05).Take(2))
        {
            targets.Add(BuildActionableTarget(zone, guide));
        }

        targets.AddRange(performance.CoachingMessages.Take(2));
        targets.AddRange(repeatedWeaknesses.Take(2));

        return Distinct(targets).Take(3).ToArray();
    }

    private static IReadOnlyList<string> BuildCoachingInsights(
        SessionDriverPerformance performance,
        IReadOnlyList<ZonePerformanceMetric> mappedZones,
        (DriverProgressTrend Trend, string Summary) progress,
        string? previousDelta,
        IReadOnlyList<string> targets,
        TrackGuide? guide)
    {
        var insights = new List<string>();
        if (performance.BrakingQuality.Score0To100 is < 70)
        {
            insights.Add($"Brake release quality: {performance.BrakingQuality.Detail}");
        }

        if (performance.ThrottleQuality.Score0To100 is < 70)
        {
            insights.Add($"Throttle pickup/smoothness: {performance.ThrottleQuality.Detail}");
        }

        var delayedThrottle = mappedZones.FirstOrDefault(metric =>
            metric.Behaviors.Any(behavior => behavior.Contains("throttle", StringComparison.OrdinalIgnoreCase)));
        if (delayedThrottle is not null)
        {
            insights.Add($"Your main loss is delayed throttle pickup after {delayedThrottle.Zone.Label}.");
        }

        var abruptBrake = mappedZones.FirstOrDefault(metric =>
            metric.Behaviors.Any(behavior => behavior.Contains("abrupt brake release", StringComparison.OrdinalIgnoreCase)));
        if (abruptBrake is not null)
        {
            insights.Add("Brake release is inconsistent; focus on trailing off smoother.");
        }

        if (!string.IsNullOrWhiteSpace(progress.Summary))
        {
            insights.Add(progress.Summary);
        }

        if (!string.IsNullOrWhiteSpace(previousDelta))
        {
            insights.Add(previousDelta);
        }

        insights.AddRange(targets);
        if (guide?.SectorNotes.Count > 0)
        {
            insights.Add($"Track guide note: {guide.SectorNotes[0]}");
        }

        return Distinct(insights).Take(6).ToArray();
    }

    private static string BuildSummary(
        (DriverProgressTrend Trend, string Summary) progress,
        string? weakest,
        string? strongest,
        string consistency,
        string? previousDelta)
    {
        var parts = new List<string> { progress.Summary };
        if (!string.IsNullOrWhiteSpace(weakest))
        {
            parts.Add($"Weakest area: {weakest}.");
        }

        if (!string.IsNullOrWhiteSpace(strongest))
        {
            parts.Add($"Strongest area: {strongest}.");
        }

        if (!string.IsNullOrWhiteSpace(consistency) && consistency != SessionDriverPerformance.NeedCleanLapMessage)
        {
            parts.Add($"Consistency: {consistency}");
        }

        if (!string.IsNullOrWhiteSpace(previousDelta))
        {
            parts.Add(previousDelta);
        }

        return string.Join(" ", parts.Where(part => !string.IsNullOrWhiteSpace(part)));
    }

    private static string BuildActionableTarget(ZonePerformanceMetric zone, TrackGuide? guide)
    {
        var behavior = ShortBehavior(zone.Behaviors);
        if (behavior.Contains("throttle", StringComparison.OrdinalIgnoreCase))
        {
            return $"Improve throttle pickup through {zone.Zone.Label}.";
        }

        if (behavior.Contains("brak", StringComparison.OrdinalIgnoreCase))
        {
            return $"Smooth brake release into {zone.Zone.Label}.";
        }

        return $"Reduce time loss in {zone.Zone.Label}: {behavior}.";
    }

    private static string BuildTrendSummary(
        string direction,
        double? delta,
        SessionMemorySummary? previousSession,
        TrackMemoryRecord? trackMemory)
    {
        var track = previousSession?.TrackName ?? trackMemory?.TrackName ?? "this track";
        if (delta is { } value)
        {
            return $"You are {direction} by {Math.Abs(value).ToString("0.0", CultureInfo.InvariantCulture)}s versus your previous {track} average.";
        }

        return $"Progress is {direction} versus your previous {track} baseline.";
    }

    private static double? AverageCleanLapSeconds(
        SessionDriverPerformance performance,
        SessionTelemetryAnalytics? analytics)
    {
        if (analytics?.BestVsAverage.AverageLapSeconds is { } average)
        {
            return average;
        }

        return performance.ValidLapCount > 0 ? null : null;
    }

    private static string FormatSectorName(string sectorName, TrackGuide? guide)
    {
        if (guide?.SectorNotes.FirstOrDefault(note => note.Contains("Sector 2", StringComparison.OrdinalIgnoreCase)) is { } note
            && sectorName.Contains('2'))
        {
            return note.Split('.')[0];
        }

        return sectorName;
    }

    private static string ShortBehavior(IReadOnlyList<string> behaviors)
    {
        var behavior = behaviors.FirstOrDefault(item => item.Contains("throttle", StringComparison.OrdinalIgnoreCase))
            ?? behaviors.FirstOrDefault()
            ?? "time loss";
        return behavior.Split('(')[0].Trim().ToLowerInvariant();
    }

    private static IReadOnlyList<string> Distinct(IEnumerable<string> items) =>
        items.Where(item => !string.IsNullOrWhiteSpace(item)).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
}
