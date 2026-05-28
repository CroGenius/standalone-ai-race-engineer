using System.Globalization;
using RaceEngineer.Core.Events;
using RaceEngineer.Core.Session;
using RaceEngineer.Core.Telemetry;

namespace RaceEngineer.Core.Analytics;

public sealed class DriverPerformanceIntelligenceService
{
    private const double BrakeOnThreshold = 0.40;
    private const double BrakeOffThreshold = 0.10;
    private const double ThrottlePickupThreshold = 0.45;
    private const int SectorCount = 3;

    public SessionDriverPerformance Analyze(DriverPerformanceInput input)
    {
        var validLaps = input.Session.CompletedLaps
            .Where(lap => lap.IsValid && lap.Duration.HasValue)
            .OrderBy(lap => lap.LapNumber)
            .ToArray();

        if (validLaps.Length == 0)
        {
            return SessionDriverPerformance.Unavailable(SessionDriverPerformance.NeedCleanLapMessage);
        }

        var bestLap = validLaps.OrderBy(lap => lap.Duration!.Value).First();
        var selectedLap = ResolveSelectedLap(validLaps, input.SelectedLapNumber);
        var events = input.Events ?? input.Session.Events;
        var lapSnapshots = input.Snapshots is { Count: > 0 }
            ? GroupSnapshotsByLap(input.Snapshots, validLaps)
            : new Dictionary<int, IReadOnlyList<TelemetrySnapshot>>();

        lapSnapshots.TryGetValue(selectedLap.LapNumber, out var selectedSnapshots);
        selectedSnapshots ??= [];
        if (selectedSnapshots.Count < 4)
        {
            return BuildPartialPerformance(validLaps, selectedLap, bestLap, input, events);
        }

        lapSnapshots.TryGetValue(bestLap.LapNumber, out var bestSnapshots);
        bestSnapshots ??= selectedSnapshots;

        var zones = BuildZones(selectedSnapshots);
        if (zones.Count == 0)
        {
            zones = BuildFallbackSectorZones();
        }

        var zoneMetrics = zones
            .Select(zone => AnalyzeZone(zone, bestSnapshots, selectedSnapshots, events, input.LapIntelligence))
            .OrderByDescending(metric => metric.EstimatedLossSeconds ?? 0)
            .ToArray();

        var biggestLoss = zoneMetrics.FirstOrDefault(metric => metric.EstimatedLossSeconds is > 0.030);
        var mistakeClusters = BuildMistakeClusters(zoneMetrics, events, selectedLap.LapNumber);
        var brakingQuality = BuildBrakingQuality(input.Analytics, zoneMetrics, events, selectedLap.LapNumber);
        var throttleQuality = BuildThrottleQuality(input.Analytics, zoneMetrics, events, selectedLap.LapNumber);
        var consistency = BuildConsistencyQuality(input.Analytics, input.LapIntelligence);
        var currentVsBest = ResolveCurrentVsBestDelta(selectedLap, bestLap, input.LapIntelligence);
        var baseline = BuildStoredBaselineComparison(input.TrackMemory, input.TrackMemoryComparison, zoneMetrics);
        var mainWeakness = DeriveMainWeakness(zoneMetrics, mistakeClusters, input.Analytics, input.TrackMemory);
        var coachingMessages = BuildCoachingMessages(zoneMetrics, mistakeClusters, brakingQuality, throttleQuality, consistency, baseline, input.TrackMemory);

        return new SessionDriverPerformance(
            "Available",
            validLaps.Length,
            zones.Count,
            biggestLoss,
            mainWeakness,
            brakingQuality,
            throttleQuality,
            consistency,
            currentVsBest,
            baseline,
            zoneMetrics,
            mistakeClusters,
            coachingMessages);
    }

    private static CompletedLap ResolveSelectedLap(IReadOnlyList<CompletedLap> validLaps, int? selectedLapNumber)
    {
        if (selectedLapNumber is { } lapNumber)
        {
            return validLaps.FirstOrDefault(lap => lap.LapNumber == lapNumber) ?? validLaps[^1];
        }

        return validLaps[^1];
    }

    private static double? ResolveCurrentVsBestDelta(
        CompletedLap selectedLap,
        CompletedLap bestLap,
        SessionLapIntelligence? lapIntelligence)
    {
        var computed = Round(selectedLap.Duration!.Value.TotalSeconds - bestLap.Duration!.Value.TotalSeconds);
        if (lapIntelligence?.LapComparison.SelectedLapNumber == selectedLap.LapNumber
            && lapIntelligence.LapComparison.DeltaSeconds is { } intelligenceDelta)
        {
            return intelligenceDelta;
        }

        return computed;
    }

    private static SessionDriverPerformance BuildPartialPerformance(
        IReadOnlyList<CompletedLap> validLaps,
        CompletedLap selectedLap,
        CompletedLap bestLap,
        DriverPerformanceInput input,
        IReadOnlyList<TelemetryEvent> events)
    {
        var zones = BuildFallbackSectorZones();
        var zoneMetrics = BuildPartialZoneMetrics(zones, input.LapIntelligence);
        var mistakeClusters = BuildMistakeClusters(zoneMetrics, events, selectedLap.LapNumber);
        var brakingQuality = BuildBrakingQuality(input.Analytics, zoneMetrics, events, selectedLap.LapNumber);
        var throttleQuality = BuildThrottleQuality(input.Analytics, zoneMetrics, events, selectedLap.LapNumber);
        var consistency = BuildConsistencyQuality(input.Analytics, input.LapIntelligence);
        var currentVsBest = ResolveCurrentVsBestDelta(selectedLap, bestLap, input.LapIntelligence);
        var baseline = BuildStoredBaselineComparison(input.TrackMemory, input.TrackMemoryComparison, zoneMetrics);
        var biggestLoss = zoneMetrics.FirstOrDefault(metric => metric.EstimatedLossSeconds is > 0.030);
        var mainWeakness = DeriveMainWeakness(zoneMetrics, mistakeClusters, input.Analytics, input.TrackMemory)
            ?? input.LapIntelligence?.Weaknesses.FirstOrDefault()
            ?? input.Analytics?.DriverProfile.Weaknesses.FirstOrDefault();
        var coachingMessages = BuildCoachingMessages(
            zoneMetrics,
            mistakeClusters,
            brakingQuality,
            throttleQuality,
            consistency,
            baseline,
            input.TrackMemory);
        if (coachingMessages.Count == 0 && !string.IsNullOrWhiteSpace(mainWeakness))
        {
            coachingMessages = [$"Priority improvement: {mainWeakness}."];
        }
        else if (coachingMessages.Count == 0 && currentVsBest is > 0.010)
        {
            coachingMessages = [$"Selected lap is {currentVsBest:0.000}s slower than session best."];
        }

        return new SessionDriverPerformance(
            "Available",
            validLaps.Count,
            zones.Count,
            biggestLoss,
            mainWeakness,
            brakingQuality,
            throttleQuality,
            consistency,
            currentVsBest,
            baseline,
            zoneMetrics,
            mistakeClusters,
            coachingMessages);
    }

    private static IReadOnlyList<ZonePerformanceMetric> BuildPartialZoneMetrics(
        IReadOnlyList<PerformanceZone> zones,
        SessionLapIntelligence? lapIntelligence)
    {
        var metrics = lapIntelligence?.SectorDeltas.Sectors
            .Where(sector => sector.DeltaSeconds is > 0.030)
            .Select(sector =>
            {
                var zone = zones.FirstOrDefault(item => item.SectorIndex == sector.SectorIndex)
                    ?? zones[Math.Clamp(sector.SectorIndex - 1, 0, zones.Count - 1)];
                return new ZonePerformanceMetric(
                    zone,
                    sector.DeltaSeconds,
                    [$"{sector.GainLossLabel} ({sector.DeltaSeconds:+0.000;-0.000;0.000}s)"]);
            })
            .ToArray();

        if (metrics is { Length: > 0 })
        {
            return metrics;
        }

        return zones
            .Select(zone => new ZonePerformanceMetric(zone, null, []))
            .ToArray();
    }

    private static IReadOnlyList<PerformanceZone> BuildZones(IReadOnlyList<TelemetrySnapshot> snapshots)
    {
        var ordered = snapshots
            .Where(item => item.Lap.LapProgress.HasValue)
            .OrderBy(item => item.Lap.LapProgress)
            .ToArray();
        if (ordered.Length < 4)
        {
            return [];
        }

        var brakeStarts = new List<double> { 0.0 };
        for (var index = 1; index < ordered.Length; index++)
        {
            var previousBrake = ordered[index - 1].Inputs.Brake ?? 0;
            var currentBrake = ordered[index].Inputs.Brake ?? 0;
            if (previousBrake < 0.35 && currentBrake >= BrakeOnThreshold)
            {
                brakeStarts.Add(ordered[index].Lap.LapProgress!.Value);
            }
        }

        if (brakeStarts.Count <= 1)
        {
            return [];
        }

        brakeStarts.Add(1.0);
        var zones = new List<PerformanceZone>();
        for (var index = 0; index < brakeStarts.Count - 1; index++)
        {
            var start = brakeStarts[index];
            var end = brakeStarts[index + 1];
            if (end - start < 0.03)
            {
                continue;
            }

            var zoneNumber = zones.Count + 1;
            var midpoint = (start + end) / 2.0;
            zones.Add(new PerformanceZone(
                zoneNumber,
                $"Zone {zoneNumber}",
                SectorIndexForProgress(midpoint),
                Round(start),
                Round(end)));
        }

        return zones;
    }

    private static IReadOnlyList<PerformanceZone> BuildFallbackSectorZones()
    {
        return Enumerable.Range(1, SectorCount)
            .Select(index =>
            {
                var start = (index - 1) / (double)SectorCount;
                var end = index / (double)SectorCount;
                return new PerformanceZone(index, $"Zone {index}", index, Round(start), Round(end));
            })
            .ToArray();
    }

    private static ZonePerformanceMetric AnalyzeZone(
        PerformanceZone zone,
        IReadOnlyList<TelemetrySnapshot> bestSnapshots,
        IReadOnlyList<TelemetrySnapshot> selectedSnapshots,
        IReadOnlyList<TelemetryEvent> events,
        SessionLapIntelligence? lapIntelligence)
    {
        var bestInZone = SnapshotsInZone(bestSnapshots, zone);
        var selectedInZone = SnapshotsInZone(selectedSnapshots, zone);
        var behaviors = new List<string>();

        var throttlePickupDelta = CompareThrottlePickupProgress(bestInZone, selectedInZone);
        if (throttlePickupDelta is >= 0.015)
        {
            behaviors.Add($"delayed throttle pickup (+{throttlePickupDelta.Value.ToString("0.000", CultureInfo.InvariantCulture)} lap progress vs best)");
        }

        var abruptReleaseCount = CountEventsInZone(events, zone, EventType.AbruptBrakeRelease);
        if (abruptReleaseCount > 0)
        {
            behaviors.Add(abruptReleaseCount == 1
                ? "abrupt brake release"
                : $"abrupt brake release ({abruptReleaseCount}x)");
        }

        var unstableBrakingCount = CountEventsInZone(events, zone, EventType.UnstableBraking);
        if (unstableBrakingCount > 0)
        {
            behaviors.Add(unstableBrakingCount == 1
                ? "unstable braking"
                : $"unstable braking ({unstableBrakingCount}x)");
        }

        var hesitationCount = CountEventsInZone(events, zone, EventType.ThrottleHesitation);
        if (hesitationCount > 0)
        {
            behaviors.Add(hesitationCount == 1
                ? "throttle hesitation"
                : $"throttle hesitation ({hesitationCount}x)");
        }

        var steeringCorrections = CountSteeringCorrections(selectedInZone);
        if (steeringCorrections >= 3)
        {
            behaviors.Add($"steering corrections ({steeringCorrections}x)");
        }

        var entryStability = EntryStabilityScore(selectedInZone);
        if (entryStability is < 55)
        {
            behaviors.Add("corner entry instability");
        }

        var exitStability = ExitStabilityScore(selectedInZone);
        if (exitStability is < 55)
        {
            behaviors.Add("corner exit instability");
        }

        var sectorLoss = lapIntelligence?.SectorDeltas.Sectors
            .FirstOrDefault(sector => sector.SectorIndex == zone.SectorIndex && sector.DeltaSeconds is > 0.030)
            ?.DeltaSeconds;
        var estimatedLoss = sectorLoss ?? EstimateLossFromBehaviors(behaviors);

        return new ZonePerformanceMetric(zone, estimatedLoss, behaviors);
    }

    private static PerformanceQualityMetric BuildBrakingQuality(
        SessionTelemetryAnalytics? analytics,
        IReadOnlyList<ZonePerformanceMetric> zoneMetrics,
        IReadOnlyList<TelemetryEvent> events,
        int lapNumber)
    {
        var score = analytics?.BrakeStability.Score0To100;
        var abrupt = events.Count(item => item.Type == EventType.AbruptBrakeRelease && item.LapNumber == lapNumber);
        var unstable = events.Count(item => item.Type == EventType.UnstableBraking && item.LapNumber == lapNumber);
        var zoneIssue = zoneMetrics.FirstOrDefault(metric =>
            metric.Behaviors.Any(behavior => behavior.Contains("brake", StringComparison.OrdinalIgnoreCase)));

        var detail = score is { } value
            ? $"{value:0}/100"
            : "-";
        if (zoneIssue is not null && zoneIssue.Behaviors.Any(behavior => behavior.Contains("abrupt", StringComparison.OrdinalIgnoreCase)))
        {
            detail = $"Abrupt release in {zoneIssue.Zone.Label}; {detail}";
        }
        else if (unstable > 0)
        {
            detail = $"{unstable} unstable event(s); {detail}";
        }

        return new PerformanceQualityMetric("Braking", score, detail, score.HasValue ? "Available" : "Need at least one clean lap.");
    }

    private static PerformanceQualityMetric BuildThrottleQuality(
        SessionTelemetryAnalytics? analytics,
        IReadOnlyList<ZonePerformanceMetric> zoneMetrics,
        IReadOnlyList<TelemetryEvent> events,
        int lapNumber)
    {
        var score = analytics?.ThrottleSmoothness.Score0To100;
        var hesitation = events.Count(item => item.Type == EventType.ThrottleHesitation && item.LapNumber == lapNumber);
        var zoneIssue = zoneMetrics.FirstOrDefault(metric =>
            metric.Behaviors.Any(behavior => behavior.Contains("throttle", StringComparison.OrdinalIgnoreCase)));

        var detail = score is { } value
            ? $"{value:0}/100"
            : "-";
        if (zoneIssue is not null)
        {
            detail = $"{zoneIssue.Zone.Label} exit pickup issue; {detail}";
        }
        else if (hesitation > 0)
        {
            detail = $"{hesitation} hesitation event(s); {detail}";
        }

        return new PerformanceQualityMetric("Throttle", score, detail, score.HasValue ? "Available" : "Need at least one clean lap.");
    }

    private static PerformanceQualityMetric BuildConsistencyQuality(
        SessionTelemetryAnalytics? analytics,
        SessionLapIntelligence? lapIntelligence)
    {
        var score = analytics?.LapConsistency.Score0To100;
        var inconsistentSector = lapIntelligence?.SectorDeltas.Sectors
            .OrderByDescending(sector => Math.Abs(sector.DeltaSeconds ?? 0))
            .FirstOrDefault(sector => sector.DeltaSeconds is > 0.080);

        var detail = score is { } value
            ? $"{value:0}/100"
            : "-";
        if (inconsistentSector is not null)
        {
            detail = $"{inconsistentSector.SectorName} inconsistent; {detail}";
        }

        return new PerformanceQualityMetric(
            "Consistency",
            score,
            detail,
            score.HasValue ? "Available" : "Need at least one clean lap.");
    }

    private static string? BuildStoredBaselineComparison(
        RaceAwareness.TrackMemoryRecord? memory,
        RaceAwareness.TrackMemoryComparison? comparison,
        IReadOnlyList<ZonePerformanceMetric> zoneMetrics)
    {
        if (comparison is not { HasHistoricalData: true })
        {
            return null;
        }

        var repeated = memory?.PerformanceWeaknessPatterns
            .FirstOrDefault(pattern => zoneMetrics.Any(metric =>
                metric.Behaviors.Any(behavior => behavior.Contains(pattern, StringComparison.OrdinalIgnoreCase))));
        if (!string.IsNullOrWhiteSpace(repeated))
        {
            return $"Stored baseline still shows {repeated.ToLowerInvariant()}.";
        }

        if (comparison.BestLapDeltaSeconds is { } delta)
        {
            var direction = delta < -0.05 ? "faster than" : delta > 0.05 ? "slower than" : "matching";
            return $"Current best is {direction} stored baseline by {Math.Abs(delta):0.000}s.";
        }

        return "Stored baseline available for this track and car.";
    }

    private static string? DeriveMainWeakness(
        IReadOnlyList<ZonePerformanceMetric> zoneMetrics,
        IReadOnlyList<MistakeCluster> mistakeClusters,
        SessionTelemetryAnalytics? analytics,
        RaceAwareness.TrackMemoryRecord? memory)
    {
        var topZone = zoneMetrics.FirstOrDefault(metric => metric.Behaviors.Count > 0);
        if (topZone is not null)
        {
            return $"{topZone.Zone.Label}: {topZone.Behaviors[0]}";
        }

        var profileWeakness = analytics?.DriverProfile.Weaknesses.FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(profileWeakness))
        {
            return profileWeakness;
        }

        var historical = memory?.PerformanceWeaknessPatterns.FirstOrDefault();
        return historical;
    }

    private static IReadOnlyList<MistakeCluster> BuildMistakeClusters(
        IReadOnlyList<ZonePerformanceMetric> zoneMetrics,
        IReadOnlyList<TelemetryEvent> events,
        int lapNumber)
    {
        var clusters = new Dictionary<string, MistakeCluster>(StringComparer.OrdinalIgnoreCase);
        foreach (var metric in zoneMetrics)
        {
            foreach (var behavior in metric.Behaviors)
            {
                var key = behavior.Split('(')[0].Trim();
                if (clusters.TryGetValue(key, out var existing))
                {
                    clusters[key] = existing with { Count = existing.Count + 1 };
                }
                else
                {
                    clusters[key] = new MistakeCluster(key, 1, metric.Zone.Label);
                }
            }
        }

        foreach (var eventItem in events.Where(item => item.LapNumber == lapNumber))
        {
            var label = eventItem.Type switch
            {
                EventType.AbruptBrakeRelease => "abrupt brake release",
                EventType.UnstableBraking => "unstable braking",
                EventType.ThrottleHesitation => "throttle hesitation",
                EventType.EarlyThrottleWithSteering => "early throttle",
                EventType.SteeringOveruse => "steering corrections",
                _ => null
            };
            if (label is null)
            {
                continue;
            }

            if (clusters.TryGetValue(label, out var existing))
            {
                clusters[label] = existing with { Count = existing.Count + 1 };
            }
            else
            {
                clusters[label] = new MistakeCluster(label, 1, "multiple zones");
            }
        }

        return clusters.Values.OrderByDescending(item => item.Count).Take(4).ToArray();
    }

    private static IReadOnlyList<string> BuildCoachingMessages(
        IReadOnlyList<ZonePerformanceMetric> zoneMetrics,
        IReadOnlyList<MistakeCluster> mistakeClusters,
        PerformanceQualityMetric brakingQuality,
        PerformanceQualityMetric throttleQuality,
        PerformanceQualityMetric consistency,
        string? baseline,
        RaceAwareness.TrackMemoryRecord? memory)
    {
        var messages = new List<string>();
        var losingZones = zoneMetrics
            .Where(metric => metric.Behaviors.Count > 0)
            .Take(3)
            .ToArray();

        if (losingZones.Any(metric => metric.Behaviors.Any(behavior => behavior.Contains("throttle", StringComparison.OrdinalIgnoreCase))))
        {
            var zone = losingZones.First(metric =>
                metric.Behaviors.Any(behavior => behavior.Contains("throttle", StringComparison.OrdinalIgnoreCase)));
            messages.Add($"You are losing time on corner exits, mainly from {ShortBehavior(zone.Behaviors)} in {zone.Zone.Label}.");
        }

        var abruptZone = losingZones.FirstOrDefault(metric =>
            metric.Behaviors.Any(behavior => behavior.Contains("abrupt brake release", StringComparison.OrdinalIgnoreCase)));
        if (abruptZone is not null)
        {
            messages.Add($"Brake release is abrupt in {abruptZone.Zone.Label}.");
        }

        if (consistency.Score0To100 is >= 70
            && consistency.Detail.Contains("Sector", StringComparison.OrdinalIgnoreCase))
        {
            messages.Add($"Your pace is stable, but {consistency.Detail.Split(';')[0].Trim().ToLowerInvariant()}.");
        }
        else if (consistency.Score0To100 is >= 70)
        {
            messages.Add("Your pace is stable across recent laps.");
        }

        if (throttleQuality.Score0To100 is >= 70 && brakingQuality.Score0To100 is >= 65)
        {
            messages.Add("You can push more on exits; throttle and braking patterns look stable.");
        }

        if (!string.IsNullOrWhiteSpace(baseline))
        {
            messages.Add(baseline);
        }

        if (messages.Count == 0 && losingZones.Length > 0)
        {
            var top = losingZones[0];
            messages.Add($"Biggest loss is in {top.Zone.Label}: {string.Join(", ", top.Behaviors)}.");
        }

        if (messages.Count == 0 && mistakeClusters.Count > 0)
        {
            messages.Add($"Repeated issue: {mistakeClusters[0].Behavior} in {mistakeClusters[0].ZoneLabel}.");
        }

        if (messages.Count == 0 && memory?.PerformanceWeaknessPatterns.Count > 0)
        {
            messages.Add($"Historical weakness on this track: {memory.PerformanceWeaknessPatterns[0]}.");
        }

        return messages;
    }

    public static string BuildLosingTimeAnswer(SessionDriverPerformance performance)
    {
        if (performance.Availability != "Available")
        {
            return SessionDriverPerformance.NeedCleanLapMessage;
        }

        var lossMessage = performance.CoachingMessages.FirstOrDefault(message =>
            message.Contains("losing", StringComparison.OrdinalIgnoreCase)
            || message.Contains("loss", StringComparison.OrdinalIgnoreCase)
            || message.Contains("Zone", StringComparison.OrdinalIgnoreCase)
            || message.Contains("Sector", StringComparison.OrdinalIgnoreCase));
        if (!string.IsNullOrWhiteSpace(lossMessage))
        {
            return lossMessage;
        }

        if (performance.BiggestTimeLoss is { } loss && loss.Behaviors.Count > 0)
        {
            return $"Biggest loss is in {loss.Zone.Label}: {string.Join(", ", loss.Behaviors)}.";
        }

        if (!string.IsNullOrWhiteSpace(performance.MainWeakness))
        {
            return $"You are losing time on {performance.MainWeakness}.";
        }

        if (performance.CurrentVsBestDeltaSeconds is > 0.010)
        {
            return $"You are losing {performance.CurrentVsBestDeltaSeconds:0.000}s versus session best.";
        }

        return performance.CoachingMessages.FirstOrDefault()
            ?? "No clear time-loss pattern yet; keep building clean laps.";
    }

    public static string BuildImprovementAnswer(SessionDriverPerformance performance)
    {
        if (performance.Availability != "Available")
        {
            return SessionDriverPerformance.NeedCleanLapMessage;
        }

        if (!string.IsNullOrWhiteSpace(performance.MainWeakness))
        {
            return $"Priority improvement: {performance.MainWeakness}.";
        }

        return performance.CoachingMessages.FirstOrDefault()
            ?? "Keep building clean laps to identify a priority improvement area.";
    }

    public static string BuildBrakingAnswer(SessionDriverPerformance performance)
    {
        if (performance.Availability != "Available")
        {
            return SessionDriverPerformance.NeedCleanLapMessage;
        }

        var abrupt = performance.ZoneMetrics.FirstOrDefault(metric =>
            metric.Behaviors.Any(behavior => behavior.Contains("abrupt brake release", StringComparison.OrdinalIgnoreCase)));
        if (abrupt is not null)
        {
            return $"Brake release is abrupt in {abrupt.Zone.Label}.";
        }

        return $"Braking quality is {performance.BrakingQuality.Detail}.";
    }

    public static string BuildThrottleAnswer(SessionDriverPerformance performance)
    {
        if (performance.Availability != "Available")
        {
            return SessionDriverPerformance.NeedCleanLapMessage;
        }

        var pickup = performance.ZoneMetrics.FirstOrDefault(metric =>
            metric.Behaviors.Any(behavior => behavior.Contains("throttle", StringComparison.OrdinalIgnoreCase)));
        if (pickup is not null)
        {
            return $"Throttle pickup is delayed in {pickup.Zone.Label}: {ShortBehavior(pickup.Behaviors)}.";
        }

        return $"Throttle quality is {performance.ThrottleQuality.Detail}.";
    }

    public static string BuildPushAnswer(SessionDriverPerformance performance)
    {
        if (performance.Availability != "Available")
        {
            return SessionDriverPerformance.NeedCleanLapMessage;
        }

        var pushMessage = performance.CoachingMessages.FirstOrDefault(message =>
            message.Contains("push", StringComparison.OrdinalIgnoreCase));
        if (!string.IsNullOrWhiteSpace(pushMessage))
        {
            return pushMessage;
        }

        if (performance.ThrottleQuality.Score0To100 is >= 70 && performance.BrakingQuality.Score0To100 is >= 65)
        {
            return "You can push more on exits; throttle and braking patterns look stable.";
        }

        return "Keep building clean laps before recommending extra push.";
    }

    public static string BuildLapComparisonAnswer(
        SessionDriverPerformance performance,
        RaceAwareness.TrackMemoryComparison? comparison = null)
    {
        if (performance.Availability != "Available")
        {
            return SessionDriverPerformance.NeedCleanLapMessage;
        }

        var parts = new List<string>();
        if (performance.CurrentVsBestDeltaSeconds is { } delta)
        {
            parts.Add(delta <= 0.010
                ? "Selected lap matches your session best."
                : $"Selected lap is {delta:0.000}s slower than session best.");
        }

        if (!string.IsNullOrWhiteSpace(performance.StoredBaselineComparison))
        {
            parts.Add(performance.StoredBaselineComparison);
        }
        else if (comparison is { HasHistoricalData: true, BestLapDeltaSeconds: { } bestDelta })
        {
            var direction = bestDelta < -0.05 ? "faster than" : bestDelta > 0.05 ? "slower than" : "matching";
            parts.Add($"Current best is {direction} stored baseline by {Math.Abs(bestDelta):0.000}s.");
        }

        if (performance.BiggestTimeLoss is { } loss && loss.Behaviors.Count > 0)
        {
            parts.Add($"Biggest gap versus best is in {loss.Zone.Label}: {string.Join(", ", loss.Behaviors)}.");
        }

        return parts.Count > 0
            ? string.Join(" ", parts)
            : "No clear lap comparison pattern yet; keep building clean laps.";
    }

    private static string ShortBehavior(IReadOnlyList<string> behaviors)
    {
        var behavior = behaviors.FirstOrDefault(item => item.Contains("throttle", StringComparison.OrdinalIgnoreCase))
            ?? behaviors.FirstOrDefault()
            ?? "driving issue";
        return behavior.Split('(')[0].Trim().ToLowerInvariant();
    }

    private static double? CompareThrottlePickupProgress(
        IReadOnlyList<TelemetrySnapshot> bestSnapshots,
        IReadOnlyList<TelemetrySnapshot> selectedSnapshots)
    {
        var bestPickup = FindThrottlePickupProgress(bestSnapshots);
        var selectedPickup = FindThrottlePickupProgress(selectedSnapshots);
        if (!bestPickup.HasValue || !selectedPickup.HasValue)
        {
            return null;
        }

        return Round(selectedPickup.Value - bestPickup.Value);
    }

    private static double? FindThrottlePickupProgress(IReadOnlyList<TelemetrySnapshot> snapshots)
    {
        var brakeSeen = false;
        foreach (var snapshot in snapshots.OrderBy(item => item.Lap.LapProgress))
        {
            var brake = snapshot.Inputs.Brake ?? 0;
            var throttle = snapshot.Inputs.Throttle ?? 0;
            if (brake >= BrakeOnThreshold)
            {
                brakeSeen = true;
            }

            if (brakeSeen && brake <= BrakeOffThreshold && throttle >= ThrottlePickupThreshold)
            {
                return snapshot.Lap.LapProgress;
            }
        }

        return null;
    }

    private static int CountEventsInZone(IReadOnlyList<TelemetryEvent> events, PerformanceZone zone, EventType type) =>
        events.Count(item =>
            item.Type == type
            && item.LapProgress is { } progress
            && progress >= zone.ProgressStart
            && progress <= zone.ProgressEnd);

    private static int CountSteeringCorrections(IReadOnlyList<TelemetrySnapshot> snapshots)
    {
        var ordered = snapshots.Where(item => item.Lap.LapProgress.HasValue).OrderBy(item => item.Lap.LapProgress).ToArray();
        var corrections = 0;
        for (var index = 2; index < ordered.Length; index++)
        {
            var first = ordered[index - 2].Inputs.Steering ?? 0;
            var second = ordered[index - 1].Inputs.Steering ?? 0;
            var third = ordered[index].Inputs.Steering ?? 0;
            if (Math.Sign(first) != Math.Sign(second) && Math.Sign(second) != Math.Sign(third)
                && Math.Abs(second) >= 0.12)
            {
                corrections++;
            }
        }

        return corrections;
    }

    private static double? EntryStabilityScore(IReadOnlyList<TelemetrySnapshot> snapshots)
    {
        var entry = snapshots
            .OrderBy(item => item.Lap.LapProgress)
            .Take(Math.Max(2, snapshots.Count / 3))
            .Select(item => Math.Abs(item.Inputs.Steering ?? 0))
            .ToArray();
        if (entry.Length == 0)
        {
            return null;
        }

        return Clamp(100 - (entry.Average() * 180), 0, 100);
    }

    private static double? ExitStabilityScore(IReadOnlyList<TelemetrySnapshot> snapshots)
    {
        var exit = snapshots
            .OrderBy(item => item.Lap.LapProgress)
            .TakeLast(Math.Max(2, snapshots.Count / 3))
            .Select(item => Math.Abs(item.Inputs.Steering ?? 0))
            .ToArray();
        if (exit.Length == 0)
        {
            return null;
        }

        return Clamp(100 - (exit.Average() * 180), 0, 100);
    }

    private static double? EstimateLossFromBehaviors(IReadOnlyList<string> behaviors)
    {
        if (behaviors.Count == 0)
        {
            return null;
        }

        var score = behaviors.Sum(behavior => behavior switch
        {
            _ when behavior.Contains("delayed throttle", StringComparison.OrdinalIgnoreCase) => 0.080,
            _ when behavior.Contains("abrupt brake release", StringComparison.OrdinalIgnoreCase) => 0.060,
            _ when behavior.Contains("unstable braking", StringComparison.OrdinalIgnoreCase) => 0.050,
            _ when behavior.Contains("throttle hesitation", StringComparison.OrdinalIgnoreCase) => 0.045,
            _ when behavior.Contains("steering corrections", StringComparison.OrdinalIgnoreCase) => 0.040,
            _ when behavior.Contains("instability", StringComparison.OrdinalIgnoreCase) => 0.035,
            _ => 0.025
        });

        return Round(score);
    }

    private static IReadOnlyList<TelemetrySnapshot> SnapshotsInZone(
        IReadOnlyList<TelemetrySnapshot> snapshots,
        PerformanceZone zone) =>
        snapshots
            .Where(item => item.Lap.LapProgress is { } progress
                && progress >= zone.ProgressStart - 0.005
                && progress <= zone.ProgressEnd + 0.005)
            .ToArray();

    private static Dictionary<int, IReadOnlyList<TelemetrySnapshot>> GroupSnapshotsByLap(
        IReadOnlyList<TelemetrySnapshot> snapshots,
        IReadOnlyList<CompletedLap> validLaps)
    {
        var result = new Dictionary<int, List<TelemetrySnapshot>>();
        foreach (var lap in validLaps)
        {
            var lapSnapshots = snapshots
                .Where(item => item.Lap.LapNumber == lap.LapNumber)
                .ToList();
            if (lapSnapshots.Count == 0)
            {
                lapSnapshots = snapshots
                    .Where(item => item.Timestamp >= lap.StartedAt && item.Timestamp <= lap.EndedAt)
                    .ToList();
            }

            if (lapSnapshots.Count > 0)
            {
                result[lap.LapNumber] = lapSnapshots;
            }
        }

        return result.ToDictionary(item => item.Key, item => (IReadOnlyList<TelemetrySnapshot>)item.Value);
    }

    private static int SectorIndexForProgress(double progress, int sectorCount = SectorCount)
    {
        var index = (int)Math.Floor(progress * sectorCount);
        return Math.Clamp(index + 1, 1, sectorCount);
    }

    private static double Round(double value) => Math.Round(value, 3);

    private static double Clamp(double value, double min, double max) => Math.Max(min, Math.Min(max, value));
}
