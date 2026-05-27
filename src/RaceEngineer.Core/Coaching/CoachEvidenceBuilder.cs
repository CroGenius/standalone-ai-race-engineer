using System.Globalization;
using RaceEngineer.Core.Analytics;
using RaceEngineer.Core.Events;
using RaceEngineer.Core.Knowledge;
using RaceEngineer.Core.Session;
using RaceEngineer.Core.Strategy;
using RaceEngineer.Core.Telemetry;
using RaceEngineer.Core.TelemetryVisualization;

namespace RaceEngineer.Core.Coaching;

public sealed class CoachEvidenceBuilder
{
    private readonly TelemetryAnalyticsService analyticsService = new();
    private readonly LapIntelligenceService lapIntelligenceService = new();
    private readonly TelemetryTraceBuilder traceBuilder = new();

    public CoachEvidenceBundle Build(CoachEvidenceInput input)
    {
        var events = input.Events ?? input.Session.Events;
        var analytics = input.Analytics ?? analyticsService.Analyze(new SessionAnalyticsInput(
            input.Session,
            input.Snapshots));
        var lapIntelligence = input.LapIntelligence ?? lapIntelligenceService.Analyze(new LapIntelligenceInput(
            input.Session,
            input.Snapshots,
            input.Session.LastLap?.LapNumber));
        var timeline = input.Timeline ?? (input.Snapshots is { Count: >= 2 }
            ? traceBuilder.Build(new TelemetryTimelineInput(
                input.Session,
                input.Snapshots,
                events,
                input.Session.LastLap?.LapNumber))
            : TelemetryTimeline.Empty);

        var packets = new List<CoachEvidencePacket>();
        AddSessionPackets(packets, input.Session);
        AddEventPackets(packets, events);
        AddAnalyticsPackets(packets, analytics);
        AddLapIntelligencePackets(packets, lapIntelligence);
        var strategy = input.Strategy ?? new StrategyEngine().Analyze(new StrategyInput(
            input.Session,
            analytics,
            lapIntelligence,
            null,
            events));
        AddStrategyPackets(packets, strategy);
        AddTracePackets(packets, timeline);
        AddKnowledgePackets(packets, input.KnowledgeSources);

        return new CoachEvidenceBundle(packets
            .OrderBy(packet => packet.Category, StringComparer.Ordinal)
            .ThenBy(packet => packet.Summary, StringComparer.Ordinal)
            .ToArray());
    }

    private static void AddSessionPackets(List<CoachEvidencePacket> packets, SessionState session)
    {
        if (session.BestLap is { } bestLap)
        {
            packets.Add(new CoachEvidencePacket(
                "Session",
                "Best lap",
                "Info",
                0.95,
                CoachEvidenceSourceType.Session,
                bestLap.LapNumber,
                [],
                bestLap.Duration?.TotalSeconds,
                $"Best lap {bestLap.LapNumber} is {FormatDuration(bestLap.Duration)}."));
        }

        if (session.LastLap is { } lastLap)
        {
            packets.Add(new CoachEvidencePacket(
                "Session",
                "Last lap",
                "Info",
                0.90,
                CoachEvidenceSourceType.Session,
                lastLap.LapNumber,
                [],
                lastLap.Duration?.TotalSeconds,
                $"Last lap {lastLap.LapNumber} is {FormatDuration(lastLap.Duration)}."));
        }

        if (session.LatestFuelLevel is { } fuel)
        {
            packets.Add(new CoachEvidencePacket(
                "Fuel",
                "Latest fuel",
                "Info",
                0.90,
                CoachEvidenceSourceType.Telemetry,
                session.CurrentLap,
                [],
                fuel,
                $"Latest fuel level is {fuel.ToString("0.0", CultureInfo.InvariantCulture)}."));
        }
    }

    private static void AddEventPackets(List<CoachEvidencePacket> packets, IReadOnlyList<TelemetryEvent> events)
    {
        foreach (var group in events
                     .Where(item => item.Type is not EventType.LapStart and not EventType.LapEnd)
                     .GroupBy(item => item.Type)
                     .OrderByDescending(group => group.Count()))
        {
            var sample = group.First();
            packets.Add(new CoachEvidencePacket(
                "Event",
                group.Key.ToString(),
                sample.Severity.ToString(),
                sample.Confidence,
                CoachEvidenceSourceType.Event,
                sample.LapNumber,
                group.Select(item => item.Id).ToArray(),
                group.Count(),
                $"{group.Count()} recent {group.Key} event(s). {sample.SuggestedAction}"));
        }
    }

    private static void AddAnalyticsPackets(List<CoachEvidencePacket> packets, SessionTelemetryAnalytics analytics)
    {
        if (analytics.BrakeStability.Score0To100 is { } brakeScore)
        {
            packets.Add(new CoachEvidencePacket(
                "Braking",
                "Brake stability score",
                brakeScore >= 75 ? "Info" : "Warning",
                0.85,
                CoachEvidenceSourceType.Analytics,
                null,
                [],
                brakeScore,
                $"Brake stability is {brakeScore.ToString("0", CultureInfo.InvariantCulture)}/100 with {analytics.BrakeStability.UnstableBrakingCount} unstable and {analytics.BrakeStability.AbruptReleaseCount} abrupt events."));
        }

        if (analytics.ThrottleSmoothness.Score0To100 is { } throttleScore)
        {
            packets.Add(new CoachEvidencePacket(
                "Throttle",
                "Throttle smoothness score",
                throttleScore >= 75 ? "Info" : "Warning",
                0.85,
                CoachEvidenceSourceType.Analytics,
                null,
                [],
                throttleScore,
                $"Throttle smoothness is {throttleScore.ToString("0", CultureInfo.InvariantCulture)}/100 with {analytics.ThrottleSmoothness.HesitationCount} hesitation and {analytics.ThrottleSmoothness.EarlyThrottleCount} early-throttle events."));
        }

        if (analytics.PaceTrend.RecentAverageSeconds is { } recent && analytics.PaceTrend.PriorAverageSeconds is { } prior)
        {
            packets.Add(new CoachEvidencePacket(
                "Pace",
                "Recent pace trend",
                analytics.PaceTrend.TrendLabel == "Slowing" ? "Warning" : "Info",
                0.80,
                CoachEvidenceSourceType.Analytics,
                null,
                [],
                analytics.PaceTrend.DeltaSeconds,
                $"Recent pace is {analytics.PaceTrend.TrendLabel}: {prior.ToString("0.000", CultureInfo.InvariantCulture)}s to {recent.ToString("0.000", CultureInfo.InvariantCulture)}s."));
        }

        if (analytics.FuelTrend.FuelPerLap is { } fuelPerLap)
        {
            packets.Add(new CoachEvidencePacket(
                "Fuel",
                "Fuel per lap",
                "Info",
                0.85,
                CoachEvidenceSourceType.Analytics,
                null,
                [],
                fuelPerLap,
                $"Fuel usage is {fuelPerLap.ToString("0.00", CultureInfo.InvariantCulture)} per lap with {analytics.FuelTrend.TrendLabel} trend."));
        }

        if (analytics.Incidents.TotalIncidents > 0)
        {
            packets.Add(new CoachEvidencePacket(
                "Incident",
                "Incident frequency",
                analytics.Incidents.IncidentsPerLap >= 2 ? "Warning" : "Info",
                0.80,
                CoachEvidenceSourceType.Analytics,
                null,
                [],
                analytics.Incidents.IncidentsPerLap,
                $"{analytics.Incidents.TotalIncidents} incident(s) recorded at {analytics.Incidents.IncidentsPerLap.ToString("0.00", CultureInfo.InvariantCulture)} per lap."));
        }

        foreach (var weakness in analytics.DriverProfile.Weaknesses)
        {
            packets.Add(new CoachEvidencePacket(
                "Improvement",
                "Driver weakness",
                "Warning",
                0.75,
                CoachEvidenceSourceType.Analytics,
                null,
                [],
                null,
                weakness));
        }
    }

    private static void AddLapIntelligencePackets(List<CoachEvidencePacket> packets, SessionLapIntelligence intelligence)
    {
        if (intelligence.LapComparison.BestLapSeconds is { } best && intelligence.LapComparison.SelectedLapSeconds is { } selected)
        {
            packets.Add(new CoachEvidencePacket(
                "LapComparison",
                "Selected vs best lap",
                intelligence.LapComparison.DeltaSeconds is > 0.100 ? "Warning" : "Info",
                0.90,
                CoachEvidenceSourceType.LapIntelligence,
                intelligence.LapComparison.SelectedLapNumber,
                [],
                intelligence.LapComparison.DeltaSeconds,
                $"Selected lap {intelligence.LapComparison.SelectedLapNumber} is {selected.ToString("0.000", CultureInfo.InvariantCulture)}s versus best lap {intelligence.LapComparison.BestLapNumber} at {best.ToString("0.000", CultureInfo.InvariantCulture)}s."));
        }

        foreach (var sector in intelligence.SectorDeltas.Sectors.Where(item => item.DeltaSeconds is >= 0.050))
        {
            packets.Add(new CoachEvidencePacket(
                "Sector",
                sector.SectorName,
                "Warning",
                0.85,
                CoachEvidenceSourceType.LapIntelligence,
                intelligence.LapComparison.SelectedLapNumber,
                [],
                sector.DeltaSeconds,
                $"{sector.SectorName} loses {sector.DeltaSeconds!.Value.ToString("0.000", CultureInfo.InvariantCulture)}s versus best lap."));
        }

        if (intelligence.PaceDecay.FirstHalfAverageSeconds is { } firstHalf && intelligence.PaceDecay.SecondHalfAverageSeconds is { } secondHalf)
        {
            packets.Add(new CoachEvidencePacket(
                "Pace",
                "Stint pace decay",
                intelligence.PaceDecay.TrendLabel == "Decaying" ? "Warning" : "Info",
                0.80,
                CoachEvidenceSourceType.LapIntelligence,
                null,
                [],
                intelligence.PaceDecay.DeltaSeconds,
                $"Stint pace is {intelligence.PaceDecay.TrendLabel}: {firstHalf.ToString("0.000", CultureInfo.InvariantCulture)}s to {secondHalf.ToString("0.000", CultureInfo.InvariantCulture)}s."));
        }

        if (intelligence.ThrottleComparison.Delta is { } throttleDelta)
        {
            packets.Add(new CoachEvidencePacket(
                "Throttle",
                "Exit throttle delta",
                throttleDelta >= 0.08 ? "Warning" : "Info",
                0.80,
                CoachEvidenceSourceType.LapIntelligence,
                intelligence.LapComparison.SelectedLapNumber,
                [],
                throttleDelta,
                $"Exit throttle is {throttleDelta.ToString("0.00", CultureInfo.InvariantCulture)} versus best lap."));
        }

        foreach (var insight in intelligence.CoachingInsights.Take(4))
        {
            packets.Add(new CoachEvidencePacket(
                insight.Category == "strength" ? "Improvement" : insight.Category == "opportunity" ? "LosingTime" : "Improvement",
                insight.Category,
                insight.Category == "weakness" ? "Warning" : "Info",
                0.75,
                CoachEvidenceSourceType.LapIntelligence,
                intelligence.LapComparison.SelectedLapNumber,
                [],
                null,
                insight.Message));
        }
    }

    private static void AddTracePackets(List<CoachEvidencePacket> packets, TelemetryTimeline timeline)
    {
        var deltaRow = timeline.Rows.FirstOrDefault(row => row.Name == "Delta");
        if (deltaRow?.Series.FirstOrDefault()?.Points is { Count: > 0 } deltaPoints)
        {
            var worst = deltaPoints.OrderByDescending(point => point.Value).First();
            packets.Add(new CoachEvidencePacket(
                "DeltaTrace",
                "Largest delta point",
                worst.Value > 0 ? "Warning" : "Info",
                0.75,
                CoachEvidenceSourceType.Trace,
                timeline.SelectedLapNumber,
                [],
                worst.RawValue ?? worst.Value,
                $"Largest time delta versus best lap is {worst.Value.ToString("0.000", CultureInfo.InvariantCulture)}s at {worst.Progress.ToString("0.00", CultureInfo.InvariantCulture)} lap progress."));
        }

        foreach (var marker in timeline.Markers.Where(item => item.Category == "Event").Take(3))
        {
            packets.Add(new CoachEvidencePacket(
                "Incident",
                marker.Label,
                "Warning",
                0.70,
                CoachEvidenceSourceType.Trace,
                timeline.SelectedLapNumber,
                [],
                marker.Progress,
                $"Event marker {marker.Label} at {marker.Progress.ToString("0.00", CultureInfo.InvariantCulture)} lap progress."));
        }
    }

    private static void AddKnowledgePackets(List<CoachEvidencePacket> packets, IReadOnlyList<KnowledgeSource>? sources)
    {
        if (sources is null)
        {
            return;
        }

        foreach (var source in sources
                     .Where(item => string.Equals(item.Category, "setup", StringComparison.OrdinalIgnoreCase)
                         || item.Title.Contains("setup", StringComparison.OrdinalIgnoreCase))
                     .Take(3))
        {
            packets.Add(new CoachEvidencePacket(
                "Knowledge",
                source.Title,
                "Info",
                0.70,
                CoachEvidenceSourceType.Knowledge,
                null,
                [],
                null,
                TrimKnowledge(source.Content)));
        }
    }

    private static void AddStrategyPackets(List<CoachEvidencePacket> packets, SessionStrategy strategy)
    {
        if (strategy.Fuel.FuelUsedPerLap is { } fuelPerLap)
        {
            packets.Add(new CoachEvidencePacket(
                "Strategy",
                "Fuel used per lap",
                strategy.Fuel.RiskLevel >= FuelRiskLevel.High ? "Warning" : "Info",
                0.90,
                CoachEvidenceSourceType.Session,
                null,
                [],
                fuelPerLap,
                $"Fuel used per lap is {fuelPerLap.ToString("0.00", CultureInfo.InvariantCulture)}."));
        }

        if (strategy.Fuel.LapsRemaining is { } lapsRemaining)
        {
            packets.Add(new CoachEvidencePacket(
                "Strategy",
                "Laps remaining",
                strategy.Fuel.RiskLevel >= FuelRiskLevel.Moderate ? "Warning" : "Info",
                0.90,
                CoachEvidenceSourceType.Session,
                null,
                [],
                lapsRemaining,
                $"Estimated laps remaining on fuel is {lapsRemaining.ToString("0.0", CultureInfo.InvariantCulture)}."));
        }

        if (strategy.Fuel.EstimatedFinishFuel is { } finishFuel)
        {
            packets.Add(new CoachEvidencePacket(
                "Strategy",
                "Estimated finish fuel",
                finishFuel <= 0 ? "Warning" : "Info",
                0.85,
                CoachEvidenceSourceType.Session,
                null,
                [],
                finishFuel,
                $"Estimated fuel at target stint end is {finishFuel.ToString("0.0", CultureInfo.InvariantCulture)}."));
        }

        packets.Add(new CoachEvidencePacket(
            "Strategy",
            "Fuel risk",
            strategy.Fuel.RiskLevel >= FuelRiskLevel.High ? "Warning" : "Info",
            0.85,
            CoachEvidenceSourceType.Session,
            null,
            [],
            null,
            $"Fuel risk level is {strategy.Fuel.RiskLevel}."));

        if (strategy.Pit.MinimumFuelToFinish is { } minimumFuel)
        {
            packets.Add(new CoachEvidencePacket(
                "Strategy",
                "Minimum fuel to finish",
                minimumFuel > (strategy.Fuel.EstimatedFinishFuel ?? 0) ? "Warning" : "Info",
                0.85,
                CoachEvidenceSourceType.Session,
                null,
                [],
                minimumFuel,
                $"Minimum fuel to finish the stint is about {minimumFuel.ToString("0.0", CultureInfo.InvariantCulture)}."));
        }

        packets.Add(new CoachEvidencePacket(
            "Strategy",
            "Pit recommendation",
            strategy.Pit.Recommendation is PitRecommendation.PitNow or PitRecommendation.PrepareToPit ? "Warning" : "Info",
            0.90,
            CoachEvidenceSourceType.Session,
            null,
            [],
            null,
            strategy.Pit.RecommendationReason));

        if (strategy.Pit.EstimatedPitWindowStartLap is { } windowStart && strategy.Pit.EstimatedPitWindowEndLap is { } windowEnd)
        {
            packets.Add(new CoachEvidencePacket(
                "Strategy",
                "Pit window",
                "Info",
                0.85,
                CoachEvidenceSourceType.Session,
                null,
                [],
                windowStart,
                $"Estimated pit window is lap {windowStart} to lap {windowEnd}."));
        }

        packets.Add(new CoachEvidencePacket(
            "Strategy",
            "Tyre risk",
            strategy.TyreRisk.RiskLevel >= TyreRiskLevel.High ? "Warning" : "Info",
            0.80,
            CoachEvidenceSourceType.Analytics,
            null,
            [],
            strategy.TyreRisk.RiskScore0To100,
            strategy.TyreRisk.Factors.Count == 0
                ? $"Tyre risk level is {strategy.TyreRisk.RiskLevel}."
                : $"Tyre risk level is {strategy.TyreRisk.RiskLevel}: {string.Join(" ", strategy.TyreRisk.Factors)}"));

        packets.Add(new CoachEvidencePacket(
            "Strategy",
            "Strategy summary",
            "Info",
            0.90,
            CoachEvidenceSourceType.Session,
            null,
            [],
            null,
            strategy.Summary));
    }

    private static string FormatDuration(TimeSpan? duration)
    {
        if (!duration.HasValue)
        {
            return "unavailable";
        }

        return duration.Value.TotalHours >= 1
            ? duration.Value.ToString(@"h\:mm\:ss", CultureInfo.InvariantCulture)
            : duration.Value.ToString(@"m\:ss\.fff", CultureInfo.InvariantCulture);
    }

    private static string TrimKnowledge(string value)
    {
        const int maxLength = 180;
        var normalized = value.ReplaceLineEndings(" ").Trim();
        return normalized.Length <= maxLength ? normalized : normalized[..maxLength] + "...";
    }
}
