using System.Globalization;
using RaceEngineer.Core.Storage;

namespace RaceEngineer.Core.RaceAwareness;

public sealed class TrackMemoryService
{
    public static string BuildKey(string? track, string? car) =>
        $"{Normalize(track)}|{Normalize(car)}";

    public async Task<TrackMemoryRecord?> LoadAsync(
        StorageService storage,
        string? track,
        string? car,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(track) || string.IsNullOrWhiteSpace(car))
        {
            return null;
        }

        return await storage.LoadTrackCarMemoryAsync(track.Trim(), car.Trim(), cancellationToken);
    }

    public async Task<TrackMemoryRecord> UpsertFromSessionAsync(
        StorageService storage,
        TrackMemoryInput input,
        CancellationToken cancellationToken = default)
    {
        var track = input.TrackName.Trim();
        var car = input.CarName.Trim();
        var existing = await storage.LoadTrackCarMemoryAsync(track, car, cancellationToken)
            ?? TrackMemoryRecord.Empty(BuildKey(track, car), track, car);

        var currentBest = input.Session.BestLap?.Duration?.TotalSeconds;
        var validLaps = input.Session.CompletedLaps.Where(lap => lap.IsValid && lap.Duration.HasValue).Select(lap => lap.Duration!.Value.TotalSeconds).ToArray();
        var currentAverage = validLaps.Length > 0 ? validLaps.Average() : (double?)null;
        var consistency = input.Analytics?.LapConsistency.Score0To100;
        var fuelPerLap = input.Session.FuelUsedPerLap ?? input.Analytics?.FuelTrend.FuelPerLap;
        var incidentRate = input.Analytics?.Incidents.IncidentsPerLap;
        var brakingWeaknesses = input.Analytics?.DriverProfile.Weaknesses
            .Where(item => item.Contains("brak", StringComparison.OrdinalIgnoreCase))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray() ?? [];
        var throttleWeaknesses = input.Analytics?.DriverProfile.Weaknesses
            .Where(item => item.Contains("throttle", StringComparison.OrdinalIgnoreCase) || item.Contains("exit", StringComparison.OrdinalIgnoreCase))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray() ?? [];
        var performancePatterns = BuildPerformancePatterns(input.DriverPerformance);
        var strategyNotes = new List<string>(existing.StrategyNotes);
        if (!string.IsNullOrWhiteSpace(input.Strategy?.Summary))
        {
            strategyNotes.Add(input.Strategy.Summary);
        }

        if (!string.IsNullOrWhiteSpace(input.SessionSummaryMarkdown))
        {
            strategyNotes.Add(TrimSummary(input.SessionSummaryMarkdown));
        }

        var updated = existing with
        {
            SessionCount = existing.SessionCount + 1,
            BestLapSeconds = MinNullable(existing.BestLapSeconds, currentBest),
            AverageCleanLapSeconds = MergeAverage(existing.AverageCleanLapSeconds, existing.SessionCount, currentAverage),
            ConsistencyScore = consistency ?? existing.ConsistencyScore,
            BrakingWeaknesses = MergeDistinct(existing.BrakingWeaknesses, brakingWeaknesses),
            ThrottleWeaknesses = MergeDistinct(existing.ThrottleWeaknesses, throttleWeaknesses),
            PerformanceWeaknessPatterns = MergeDistinct(existing.PerformanceWeaknessPatterns, performancePatterns).TakeLast(8).ToArray(),
            TyreWarmupNotes = input.TyreIntelligence?.TyreConditionMessage ?? existing.TyreWarmupNotes,
            FuelUsedPerLap = fuelPerLap ?? existing.FuelUsedPerLap,
            IncidentRate = incidentRate ?? existing.IncidentRate,
            StrategyNotes = strategyNotes.TakeLast(8).ToArray(),
            PreviousRaceResult = input.RaceResult ?? existing.PreviousRaceResult,
            LastSessionAt = DateTimeOffset.UtcNow,
            SessionSummaries = MergeDistinct(
                existing.SessionSummaries,
                [BuildSessionSummary(input, currentBest, currentAverage, fuelPerLap)]).TakeLast(6).ToArray()
        };

        await storage.SaveTrackCarMemoryAsync(updated, cancellationToken);
        return updated;
    }

    public TrackMemoryComparison Compare(
        TrackMemoryRecord? memory,
        Session.SessionState session,
        Analytics.SessionTelemetryAnalytics? analytics)
    {
        var track = memory?.TrackName ?? "this track";
        var car = memory?.CarName ?? "this car";
        if (memory is null || memory.SessionCount == 0)
        {
            return new TrackMemoryComparison(
                false,
                track,
                car,
                null,
                null,
                session.BestLap?.Duration?.TotalSeconds,
                AverageValidLap(session),
                null,
                null,
                null,
                session.FuelUsedPerLap ?? analytics?.FuelTrend.FuelPerLap,
                [],
                [],
                "No stored session history is available for this track and car combination.");
        }

        var currentBest = session.BestLap?.Duration?.TotalSeconds;
        var currentAverage = AverageValidLap(session);
        var bestDelta = currentBest.HasValue && memory.BestLapSeconds.HasValue
            ? currentBest.Value - memory.BestLapSeconds.Value
            : (double?)null;
        var averageDelta = currentAverage.HasValue && memory.AverageCleanLapSeconds.HasValue
            ? currentAverage.Value - memory.AverageCleanLapSeconds.Value
            : (double?)null;

        var summary = BuildComparisonSummary(track, memory, currentBest, currentAverage, bestDelta, averageDelta);
        return new TrackMemoryComparison(
            true,
            track,
            car,
            memory.BestLapSeconds,
            memory.AverageCleanLapSeconds,
            currentBest,
            currentAverage,
            bestDelta,
            averageDelta,
            memory.FuelUsedPerLap,
            session.FuelUsedPerLap ?? analytics?.FuelTrend.FuelPerLap,
            memory.BrakingWeaknesses
                .Concat(memory.ThrottleWeaknesses)
                .Concat(memory.PerformanceWeaknessPatterns)
                .ToArray(),
            memory.StrategyNotes,
            summary);
    }

    public static string FormatLapTime(double? seconds)
    {
        if (seconds is not { } value)
        {
            return "unavailable";
        }

        var minutes = (int)(value / 60);
        var remainder = value - (minutes * 60);
        return minutes > 0
            ? $"{minutes}:{remainder:00.0}"
            : $"{remainder:0.000}s";
    }

    private static string BuildComparisonSummary(
        string track,
        TrackMemoryRecord memory,
        double? currentBest,
        double? currentAverage,
        double? bestDelta,
        double? averageDelta)
    {
        if (bestDelta is { } best && currentBest.HasValue && memory.BestLapSeconds.HasValue)
        {
            var direction = best < -0.05 ? "faster" : best > 0.05 ? "slower" : "about the same pace as";
            return $"Stored session data for {track}: previous best {FormatLapTime(memory.BestLapSeconds)}; today best {FormatLapTime(currentBest)} — you are {direction} stored history by {Math.Abs(best):0.000}s.";
        }

        if (averageDelta is { } average && currentAverage.HasValue && memory.AverageCleanLapSeconds.HasValue)
        {
            var direction = average < -0.05 ? "faster" : average > 0.05 ? "slower" : "matching";
            return $"Stored session data for {track}: previous average clean lap {FormatLapTime(memory.AverageCleanLapSeconds)}; today average {FormatLapTime(currentAverage)} — {direction} by {Math.Abs(average):0.000}s per lap.";
        }

        return $"Stored session data exists for {track}, but there is not enough current lap data for a comparison yet.";
    }

    private static string BuildSessionSummary(
        TrackMemoryInput input,
        double? best,
        double? average,
        double? fuelPerLap)
    {
        return
            $"Session {input.Session.SessionId.ToString()[..8]} best {FormatLapTime(best)} avg {FormatLapTime(average)} fuel {fuelPerLap?.ToString("0.00", CultureInfo.InvariantCulture) ?? "n/a"}/lap";
    }

    private static IReadOnlyList<string> BuildPerformancePatterns(Analytics.SessionDriverPerformance? performance)
    {
        if (performance is not { Availability: "Available" })
        {
            return [];
        }

        var patterns = new List<string>();
        if (!string.IsNullOrWhiteSpace(performance.MainWeakness))
        {
            patterns.Add(performance.MainWeakness);
        }

        foreach (var metric in performance.ZoneMetrics.Take(3))
        {
            if (metric.Behaviors.Count == 0)
            {
                continue;
            }

            patterns.Add($"{metric.Zone.Label}: {metric.Behaviors[0].Split('(')[0].Trim()}");
        }

        foreach (var cluster in performance.MistakeClusters.Take(2))
        {
            patterns.Add($"{cluster.Behavior} in {cluster.ZoneLabel}");
        }

        return patterns
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static double? AverageValidLap(Session.SessionState session)
    {
        var laps = session.CompletedLaps.Where(lap => lap.IsValid && lap.Duration.HasValue).Select(lap => lap.Duration!.Value.TotalSeconds).ToArray();
        return laps.Length > 0 ? laps.Average() : null;
    }

    private static double? MinNullable(double? left, double? right)
    {
        if (!left.HasValue)
        {
            return right;
        }

        if (!right.HasValue)
        {
            return left;
        }

        return Math.Min(left.Value, right.Value);
    }

    private static double? MergeAverage(double? existingAverage, int existingCount, double? currentAverage)
    {
        if (!currentAverage.HasValue)
        {
            return existingAverage;
        }

        if (!existingAverage.HasValue || existingCount <= 0)
        {
            return currentAverage;
        }

        return ((existingAverage.Value * existingCount) + currentAverage.Value) / (existingCount + 1);
    }

    private static IReadOnlyList<string> MergeDistinct(IReadOnlyList<string> existing, IReadOnlyList<string> incoming)
    {
        return existing.Concat(incoming).Where(item => !string.IsNullOrWhiteSpace(item)).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
    }

    private static string TrimSummary(string markdown) =>
        markdown.Length <= 160 ? markdown : markdown[..160].Trim() + "...";

    private static string Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value) ? "" : value.Trim().ToLowerInvariant();
}
