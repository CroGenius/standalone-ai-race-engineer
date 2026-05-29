using System.Globalization;
using RaceEngineer.Core.Analytics;
using RaceEngineer.Core.Events;
using RaceEngineer.Core.Strategy;

namespace RaceEngineer.Core.RaceAwareness;

public static class SessionMemorySummaryBuilder
{
    public static SessionMemorySummary Build(SessionMemoryBuildInput input)
    {
        var track = input.TrackName.Trim();
        var car = input.CarName.Trim();
        var validLaps = input.Session.CompletedLaps
            .Where(lap => lap.IsValid && lap.Duration.HasValue)
            .Select(lap => lap.Duration!.Value.TotalSeconds)
            .ToArray();
        var best = input.Session.BestLap?.Duration?.TotalSeconds;
        var average = validLaps.Length > 0 ? validLaps.Average() : (double?)null;
        var consistency = input.Analytics?.LapConsistency.Score0To100 ?? input.DriverPerformance?.Consistency.Score0To100;
        var fuelPerLap = input.Session.FuelUsedPerLap ?? input.Analytics?.FuelTrend.FuelPerLap;

        return new SessionMemorySummary(
            input.Session.SessionId,
            TrackMemoryService.BuildKey(track, car),
            track,
            car,
            string.IsNullOrWhiteSpace(input.SessionType) ? null : input.SessionType.Trim(),
            DateTimeOffset.UtcNow,
            best,
            average,
            consistency,
            fuelPerLap,
            BuildTyreWarmupNotes(input.TyreIntelligence),
            BuildTyreDegradationNotes(input.TyreIntelligence),
            BuildBrakingWeaknesses(input),
            BuildThrottleWeaknesses(input),
            BuildMainTimeLossZones(input.DriverPerformance),
            BuildIncidents(input),
            BuildStrategyNotes(input),
            BuildImprovementTargets(input),
            input.DriverCoaching?.StrongestArea,
            input.DriverCoaching?.WeakestArea ?? input.DriverCoaching?.BiggestWeakness,
            input.DriverCoaching?.RepeatedWeaknesses ?? BuildRepeatedWeaknesses(input),
            input.DriverCoaching?.ProgressTrendSummary,
            input.DriverCoaching?.TopCoachingTargets ?? BuildImprovementTargets(input));
    }

    private static string? BuildTyreWarmupNotes(SessionTyreIntelligence? tyreIntelligence)
    {
        if (tyreIntelligence is not { HasReliableData: true })
        {
            return null;
        }

        return string.IsNullOrWhiteSpace(tyreIntelligence.TyreConditionMessage)
            ? tyreIntelligence.CoachingMessage
            : tyreIntelligence.TyreConditionMessage;
    }

    private static string? BuildTyreDegradationNotes(SessionTyreIntelligence? tyreIntelligence)
    {
        if (tyreIntelligence is not { HasReliableData: true })
        {
            return null;
        }

        if (tyreIntelligence.Readiness is TyreReadiness.Fading or TyreReadiness.Overheated
            || tyreIntelligence.WarmupState is TyreWarmupState.Fading or TyreWarmupState.Overheating)
        {
            return string.IsNullOrWhiteSpace(tyreIntelligence.PushGuidance)
                ? tyreIntelligence.OverheatingRisk
                : $"{tyreIntelligence.OverheatingRisk} {tyreIntelligence.PushGuidance}".Trim();
        }

        return string.IsNullOrWhiteSpace(tyreIntelligence.OverheatingRisk)
            || tyreIntelligence.OverheatingRisk.Equals("Unknown", StringComparison.OrdinalIgnoreCase)
            ? null
            : tyreIntelligence.OverheatingRisk;
    }

    private static IReadOnlyList<string> BuildBrakingWeaknesses(SessionMemoryBuildInput input)
    {
        var items = new List<string>();
        if (input.DriverPerformance?.BrakingQuality is { Score0To100: { } score, Detail: var detail }
            && score < 70
            && !string.IsNullOrWhiteSpace(detail)
            && detail != SessionDriverPerformance.NeedCleanLapMessage)
        {
            items.Add(detail);
        }

        items.AddRange(input.Analytics?.DriverProfile.Weaknesses
            .Where(item => item.Contains("brak", StringComparison.OrdinalIgnoreCase)) ?? []);
        items.AddRange(input.DriverPerformance?.MistakeClusters
            .Where(item => item.Behavior.Contains("brak", StringComparison.OrdinalIgnoreCase))
            .Select(item => $"{item.Behavior} in {item.ZoneLabel}") ?? []);

        return DistinctNonEmpty(items).Take(5).ToArray();
    }

    private static IReadOnlyList<string> BuildThrottleWeaknesses(SessionMemoryBuildInput input)
    {
        var items = new List<string>();
        if (input.DriverPerformance?.ThrottleQuality is { Score0To100: { } score, Detail: var detail }
            && score < 70
            && !string.IsNullOrWhiteSpace(detail)
            && detail != SessionDriverPerformance.NeedCleanLapMessage)
        {
            items.Add(detail);
        }

        items.AddRange(input.Analytics?.DriverProfile.Weaknesses
            .Where(item => item.Contains("throttle", StringComparison.OrdinalIgnoreCase)
                || item.Contains("exit", StringComparison.OrdinalIgnoreCase)) ?? []);
        items.AddRange(input.DriverPerformance?.MistakeClusters
            .Where(item => item.Behavior.Contains("throttle", StringComparison.OrdinalIgnoreCase)
                || item.Behavior.Contains("traction", StringComparison.OrdinalIgnoreCase))
            .Select(item => $"{item.Behavior} in {item.ZoneLabel}") ?? []);

        return DistinctNonEmpty(items).Take(5).ToArray();
    }

    private static IReadOnlyList<string> BuildMainTimeLossZones(SessionDriverPerformance? performance)
    {
        if (performance is not { Availability: "Available" })
        {
            return [];
        }

        var zones = performance.ZoneMetrics
            .Where(metric => metric.EstimatedLossSeconds is > 0.05)
            .OrderByDescending(metric => metric.EstimatedLossSeconds)
            .Take(3)
            .Select(metric =>
            {
                var behavior = metric.Behaviors.FirstOrDefault() ?? "time loss";
                return $"{metric.Zone.Label}: {behavior.Trim()} ({metric.EstimatedLossSeconds!.Value.ToString("0.0", CultureInfo.InvariantCulture)}s)";
            })
            .ToArray();

        if (zones.Length > 0)
        {
            return zones;
        }

        return performance.BiggestTimeLoss is { EstimatedLossSeconds: > 0.05 } biggest
            ? [$"{biggest.Zone.Label}: {biggest.Behaviors.FirstOrDefault() ?? "time loss"}"]
            : [];
    }

    private static IReadOnlyList<string> BuildIncidents(SessionMemoryBuildInput input)
    {
        var incidents = new List<string>();
        if (input.Analytics?.Incidents.CountsByType is { Count: > 0 } counts)
        {
            incidents.AddRange(counts
                .OrderByDescending(pair => pair.Value)
                .Take(5)
                .Select(pair => $"{pair.Key}: {pair.Value}"));
        }
        else
        {
            incidents.AddRange(input.Session.RecentEvents
                .Where(item => item.Type is not EventType.LapStart and not EventType.LapEnd and not EventType.HeavyBraking)
                .GroupBy(item => item.Type)
                .OrderByDescending(group => group.Count())
                .Take(5)
                .Select(group => $"{group.Key}: {group.Count()}"));
        }

        return DistinctNonEmpty(incidents).ToArray();
    }

    private static IReadOnlyList<string> BuildStrategyNotes(SessionMemoryBuildInput input)
    {
        var notes = new List<string>();
        if (!string.IsNullOrWhiteSpace(input.Strategy?.Summary))
        {
            notes.Add(input.Strategy.Summary);
        }

        if (input.Strategy?.Fuel.RiskLevel is { } risk)
        {
            notes.Add($"Fuel risk: {risk}.");
        }

        if (input.Strategy?.Pit.Recommendation is PitRecommendation recommendation
            && recommendation != PitRecommendation.Unknown)
        {
            notes.Add(string.IsNullOrWhiteSpace(input.Strategy.Pit.RecommendationReason)
                ? $"Pit recommendation: {recommendation}."
                : input.Strategy.Pit.RecommendationReason);
        }

        return DistinctNonEmpty(notes).Take(4).ToArray();
    }

    private static IReadOnlyList<string> BuildImprovementTargets(SessionMemoryBuildInput input)
    {
        var targets = new List<string>();
        if (!string.IsNullOrWhiteSpace(input.DriverPerformance?.MainWeakness))
        {
            targets.Add(input.DriverPerformance.MainWeakness);
        }

        targets.AddRange(BuildMainTimeLossZones(input.DriverPerformance));
        targets.AddRange(BuildBrakingWeaknesses(input));
        targets.AddRange(BuildThrottleWeaknesses(input));
        targets.AddRange(input.DriverPerformance?.CoachingMessages ?? []);
        targets.AddRange(input.Analytics?.DriverProfile.Weaknesses ?? []);
        targets.AddRange(input.DriverCoaching?.TopCoachingTargets ?? []);

        return DistinctNonEmpty(targets).Take(3).ToArray();
    }

    private static IReadOnlyList<string> BuildRepeatedWeaknesses(SessionMemoryBuildInput input)
    {
        var items = new List<string>();
        items.AddRange(input.DriverPerformance?.MistakeClusters
            .Where(cluster => cluster.Count >= 2)
            .Select(cluster => $"{cluster.Behavior} in {cluster.ZoneLabel}") ?? []);
        items.AddRange(input.DriverCoaching?.RepeatedWeaknesses ?? []);
        return DistinctNonEmpty(items).Take(5).ToArray();
    }

    private static IReadOnlyList<string> DistinctNonEmpty(IEnumerable<string> items) =>
        items.Where(item => !string.IsNullOrWhiteSpace(item)).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
}
