using System.Globalization;
using RaceEngineer.Core.Analytics;
using RaceEngineer.Core.Events;
using RaceEngineer.Core.Knowledge;
using RaceEngineer.Core.Session;
using RaceEngineer.Core.Strategy;
using RaceEngineer.Core.Telemetry;
using RaceEngineer.Core.RaceAwareness;
using RaceEngineer.Core.TelemetryVisualization;

namespace RaceEngineer.Core.Coaching;

public sealed class CoachEvidenceBuilder
{
    private readonly TelemetryAnalyticsService analyticsService = new();
    private readonly LapIntelligenceService lapIntelligenceService = new();
    private readonly TyreIntelligenceService tyreIntelligenceService = new();
    private readonly DriverPerformanceIntelligenceService driverPerformanceService = new();
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
        var tyreIntelligence = input.TyreIntelligence ?? tyreIntelligenceService.Analyze(new TyreIntelligenceInput(
            input.Session,
            input.Snapshots,
            events));
        var packets = new List<CoachEvidencePacket>();
        var guide = TrackGuideZoneMapper.ResolveGuide(
            input.CachedTrackGuide,
            input.KnowledgeSources,
            TrackGuideZoneMapper.ResolveTrackName(
                input.RaceContext,
                null,
                input.Session,
                input.PreviousStoredSessionMemory,
                input.ReviewSessionTrack,
                input.Snapshots));
        var driverPerformance = TrackGuideZoneMapper.MapPerformance(
            input.DriverPerformance ?? driverPerformanceService.Analyze(new DriverPerformanceInput(
                input.Session,
                input.Snapshots,
                events,
                analytics,
                lapIntelligence,
                input.TrackMemory,
                input.TrackMemoryComparison,
                input.Session.LastLap?.LapNumber)),
            guide);
        var coaching = input.DriverCoaching is null
            ? null
            : TrackGuideZoneMapper.MapDriverCoachingRecommendation(input.DriverCoaching, guide);
        var timeline = input.Timeline ?? (input.Snapshots is { Count: >= 2 }
            ? traceBuilder.Build(new TelemetryTimelineInput(
                input.Session,
                input.Snapshots,
                events,
                input.Session.LastLap?.LapNumber))
            : TelemetryTimeline.Empty);
        AddSessionPackets(packets, input.Session);
        AddEventPackets(packets, events);
        AddAnalyticsPackets(packets, analytics);
        AddLapIntelligencePackets(packets, lapIntelligence);
        AddTyreIntelligencePackets(packets, tyreIntelligence);
        AddDriverPerformancePackets(packets, driverPerformance);
        var strategy = input.Strategy ?? new StrategyEngine().Analyze(new StrategyInput(
            input.Session,
            analytics,
            lapIntelligence,
            null,
            events));
        AddStrategyPackets(packets, strategy);
        AddStrategyKnowledgePackets(packets, input.StrategyKnowledge);
        AddTrackCarKnowledgePackets(packets, input.TrackCarKnowledge);
        AddRaceAwarenessPackets(packets, input.RaceContext);
        AddTrackMemoryPackets(packets, input.TrackMemory, input.TrackMemoryComparison);
        AddSessionMemorySummaryPackets(
            packets,
            input.PreviousStoredSessionMemory,
            input.RecentStoredSessionMemories,
            guide);
        AddTracePackets(packets, timeline);
        AddKnowledgePackets(packets, input.KnowledgeSources);
        AddTrackGuidePackets(packets, guide);
        AddOpponentIntelligencePackets(packets, input.OpponentIntelligence);
        AddDriverCoachingPackets(packets, coaching);

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

    private static void AddTyreIntelligencePackets(List<CoachEvidencePacket> packets, SessionTyreIntelligence intelligence)
    {
        if (!intelligence.HasReliableData)
        {
            return;
        }

        packets.Add(new CoachEvidencePacket(
            "TyreIntelligence",
            "Tyre readiness",
            intelligence.Readiness is TyreReadiness.NotReady or TyreReadiness.Overheated ? "Warning" : "Info",
            intelligence.GripConfidence == GripConfidenceLevel.High ? 0.90 : 0.75,
            CoachEvidenceSourceType.Analytics,
            null,
            [],
            null,
            intelligence.CoachingMessage));

        packets.Add(new CoachEvidencePacket(
            "TyreIntelligence",
            "Warmup state",
            intelligence.WarmupState is TyreWarmupState.Overheating or TyreWarmupState.Cold ? "Warning" : "Info",
            0.85,
            CoachEvidenceSourceType.Analytics,
            null,
            [],
            null,
            $"Warmup state is {intelligence.WarmupState}; grip confidence {intelligence.GripConfidenceLabel}."));

        packets.Add(new CoachEvidencePacket(
            "TyreIntelligence",
            "Overheating risk",
            intelligence.OverheatingRisk == "High" ? "Warning" : "Info",
            0.80,
            CoachEvidenceSourceType.Analytics,
            null,
            [],
            null,
            $"Overheating risk is {intelligence.OverheatingRisk}. {intelligence.PushGuidance}"));

        packets.Add(new CoachEvidencePacket(
            "TyreIntelligence",
            "Spoken tyre summary",
            "Info",
            0.90,
            CoachEvidenceSourceType.Analytics,
            null,
            [],
            null,
            intelligence.SpokenCoachingSummary));

        foreach (var corner in intelligence.Corners.Where(corner => corner.HasData))
        {
            packets.Add(new CoachEvidencePacket(
                "Tyre",
                corner.ShortLabel,
                corner.WarmupState is TyreWarmupState.Overheating or TyreWarmupState.Cold ? "Warning" : "Info",
                0.85,
                CoachEvidenceSourceType.Telemetry,
                null,
                [],
                corner.TempC,
                corner.Summary));
        }

        if (intelligence.RearDataUnavailable)
        {
            packets.Add(new CoachEvidencePacket(
                "TyreIntelligence",
                "Rear tyre data",
                "Warning",
                0.80,
                CoachEvidenceSourceType.Telemetry,
                null,
                [],
                null,
                "Rear tyre data is unavailable."));
        }

        foreach (var axle in intelligence.Axles)
        {
            packets.Add(new CoachEvidencePacket(
                "Tyre",
                axle.Label,
                axle.WarmupState is TyreWarmupState.Overheating or TyreWarmupState.Cold ? "Warning" : "Info",
                0.85,
                CoachEvidenceSourceType.Telemetry,
                null,
                [],
                axle.AverageTempC,
                axle.Summary));
        }
    }

    private static void AddDriverPerformancePackets(List<CoachEvidencePacket> packets, SessionDriverPerformance performance)
    {
        if (performance.Availability != "Available")
        {
            return;
        }

        if (performance.BiggestTimeLoss is { } loss)
        {
            packets.Add(new CoachEvidencePacket(
                "Performance",
                "Biggest time loss",
                "Warning",
                0.90,
                CoachEvidenceSourceType.Analytics,
                null,
                [],
                loss.EstimatedLossSeconds,
                $"Biggest loss in {loss.Zone.Label}: {string.Join(", ", loss.Behaviors)}."));
        }

        if (!string.IsNullOrWhiteSpace(performance.MainWeakness))
        {
            packets.Add(new CoachEvidencePacket(
                "Performance",
                "Main weakness",
                "Warning",
                0.88,
                CoachEvidenceSourceType.Analytics,
                null,
                [],
                null,
                performance.MainWeakness));
        }

        packets.Add(new CoachEvidencePacket(
            "Performance",
            "Braking quality",
            performance.BrakingQuality.Score0To100 is < 60 ? "Warning" : "Info",
            0.85,
            CoachEvidenceSourceType.Analytics,
            null,
            [],
            performance.BrakingQuality.Score0To100,
            performance.BrakingQuality.Detail));

        packets.Add(new CoachEvidencePacket(
            "Performance",
            "Throttle quality",
            performance.ThrottleQuality.Score0To100 is < 60 ? "Warning" : "Info",
            0.85,
            CoachEvidenceSourceType.Analytics,
            null,
            [],
            performance.ThrottleQuality.Score0To100,
            performance.ThrottleQuality.Detail));

        packets.Add(new CoachEvidencePacket(
            "Performance",
            "Consistency",
            performance.Consistency.Score0To100 is < 60 ? "Warning" : "Info",
            0.85,
            CoachEvidenceSourceType.Analytics,
            null,
            [],
            performance.Consistency.Score0To100,
            performance.Consistency.Detail));

        if (performance.CurrentVsBestDeltaSeconds is { } delta
            && LapDeltaSanity.IsReasonableLapDelta(delta))
        {
            packets.Add(new CoachEvidencePacket(
                "Performance",
                "Current vs best",
                delta > 0.050 ? "Warning" : "Info",
                0.85,
                CoachEvidenceSourceType.Analytics,
                null,
                [],
                delta,
                delta <= 0.010
                    ? "Selected lap matches session best."
                    : $"Selected lap is {delta.ToString("0.000", CultureInfo.InvariantCulture)}s slower than session best."));
        }

        if (!string.IsNullOrWhiteSpace(performance.StoredBaselineComparison))
        {
            packets.Add(new CoachEvidencePacket(
                "Performance",
                "Stored baseline",
                "Info",
                0.80,
                CoachEvidenceSourceType.Analytics,
                null,
                [],
                null,
                performance.StoredBaselineComparison));
        }

        foreach (var message in performance.CoachingMessages.Take(3))
        {
            packets.Add(new CoachEvidencePacket(
                "Performance",
                "Coaching insight",
                "Info",
                0.82,
                CoachEvidenceSourceType.Analytics,
                null,
                [],
                null,
                message));
        }
    }

    private static void AddTracePackets(List<CoachEvidencePacket> packets, TelemetryTimeline timeline)
    {
        var deltaRow = timeline.Rows.FirstOrDefault(row => row.Name == "Delta");
        if (deltaRow?.Series.FirstOrDefault()?.Points is { Count: > 0 } deltaPoints)
        {
            var worst = deltaPoints.OrderByDescending(point => point.Value).First();
            if (LapDeltaSanity.IsReasonableLapDelta(worst.Value))
            {
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
            else
            {
                packets.Add(new CoachEvidencePacket(
                    "DeltaTrace",
                    "Largest delta point",
                    "Info",
                    0.40,
                    CoachEvidenceSourceType.Trace,
                    timeline.SelectedLapNumber,
                    [],
                    null,
                    "Lap delta trace is unavailable because selected and best lap timestamps are not aligned."));
            }
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

    private static void AddTrackGuidePackets(List<CoachEvidencePacket> packets, TrackGuide? guide)
    {
        if (guide is null)
        {
            return;
        }

        packets.Add(new CoachEvidencePacket(
            "TrackGuide",
            "Track guide summary",
            "Info",
            0.90,
            CoachEvidenceSourceType.Knowledge,
            null,
            [],
            null,
            TrackGuideFormatter.BuildAiSummary(guide)));
        packets.Add(new CoachEvidencePacket(
            "TrackGuide",
            "Key corners",
            "Info",
            0.88,
            CoachEvidenceSourceType.Knowledge,
            null,
            [],
            null,
            TrackGuideFormatter.BuildKeyCornersSummary(guide)));
        packets.Add(new CoachEvidencePacket(
            "TrackGuide",
            "Tyre and fuel notes",
            "Info",
            0.86,
            CoachEvidenceSourceType.Knowledge,
            null,
            [],
            null,
            TrackGuideFormatter.BuildTyreFuelAiNotes(guide)));
        packets.Add(new CoachEvidencePacket(
            "TrackGuide",
            "Setup priorities",
            "Info",
            0.84,
            CoachEvidenceSourceType.Knowledge,
            null,
            [],
            null,
            TrackGuideFormatter.BuildSetupPrioritiesSummary(guide)));
    }

    private static void AddOpponentIntelligencePackets(
        List<CoachEvidencePacket> packets,
        OpponentIntelligenceRecommendation? intelligence)
    {
        if (intelligence is null || !intelligence.HasOpponentData)
        {
            return;
        }

        var confidence = MapConfidence(intelligence.Battle.Confidence);
        packets.Add(new CoachEvidencePacket(
            "OpponentIntelligence",
            "Race battle summary",
            "Info",
            confidence,
            CoachEvidenceSourceType.Session,
            intelligence.Current?.CurrentLap,
            [],
            null,
            intelligence.Summary));
        packets.Add(new CoachEvidencePacket(
            "OpponentIntelligence",
            "Gap trend",
            "Info",
            MapConfidence(intelligence.GapTrend.Confidence),
            CoachEvidenceSourceType.Session,
            intelligence.Current?.CurrentLap,
            [],
            intelligence.GapTrend.GapAheadChangePerLapSeconds ?? intelligence.GapTrend.GapBehindChangePerLapSeconds,
            intelligence.GapTrend.Summary));
        if (intelligence.Current is { } current)
        {
            if (!string.IsNullOrWhiteSpace(current.CarAhead) || current.GapAheadSeconds is not null)
            {
                packets.Add(new CoachEvidencePacket(
                    "OpponentIntelligence",
                    "Car ahead",
                    "Info",
                    0.90,
                    CoachEvidenceSourceType.Session,
                    current.CurrentLap,
                    [],
                    current.GapAheadSeconds,
                    !string.IsNullOrWhiteSpace(current.CarAhead)
                        ? $"Car ahead: {current.CarAhead}."
                        : "Car ahead gap is available."));
            }

            if (!string.IsNullOrWhiteSpace(current.CarBehind) || current.GapBehindSeconds is not null)
            {
                packets.Add(new CoachEvidencePacket(
                    "OpponentIntelligence",
                    "Car behind",
                    "Info",
                    0.90,
                    CoachEvidenceSourceType.Session,
                    current.CurrentLap,
                    [],
                    current.GapBehindSeconds,
                    !string.IsNullOrWhiteSpace(current.CarBehind)
                        ? $"Car behind: {current.CarBehind}."
                        : "Car behind gap is available."));
            }
        }

        if (!string.IsNullOrWhiteSpace(intelligence.AttackZoneRecommendation))
        {
            packets.Add(new CoachEvidencePacket(
                "OpponentIntelligence",
                "Overtake opportunity",
                "Info",
                confidence,
                CoachEvidenceSourceType.Knowledge,
                intelligence.Current?.CurrentLap,
                [],
                null,
                intelligence.AttackZoneRecommendation));
        }

        if (!string.IsNullOrWhiteSpace(intelligence.DefendZoneRecommendation))
        {
            packets.Add(new CoachEvidencePacket(
                "OpponentIntelligence",
                "Defensive opportunity",
                "Warning",
                confidence,
                CoachEvidenceSourceType.Knowledge,
                intelligence.Current?.CurrentLap,
                [],
                null,
                intelligence.DefendZoneRecommendation));
        }

        if (intelligence.StrategyInsight.HasData)
        {
            var strategyDetail = string.Join(
                " ",
                new[] { intelligence.StrategyInsight.UndercutOpportunity, intelligence.StrategyInsight.OvercutOpportunity, intelligence.StrategyInsight.RiskAssessment }
                    .Where(item => !string.IsNullOrWhiteSpace(item)));
            if (!string.IsNullOrWhiteSpace(strategyDetail))
            {
                packets.Add(new CoachEvidencePacket(
                    "OpponentIntelligence",
                    "Strategy battle insight",
                    "Info",
                    0.78,
                    CoachEvidenceSourceType.Analytics,
                    intelligence.Current?.CurrentLap,
                    [],
                    null,
                    strategyDetail));
            }
        }
    }

    private static double MapConfidence(BattleConfidence confidence) =>
        confidence switch
        {
            BattleConfidence.High => 0.92,
            BattleConfidence.Medium => 0.84,
            _ => 0.72
        };

    private static void AddKnowledgePackets(List<CoachEvidencePacket> packets, IReadOnlyList<KnowledgeSource>? sources)
    {
        if (sources is null)
        {
            return;
        }

        foreach (var source in sources
                     .Where(item =>
                         string.Equals(item.Category, "track", StringComparison.OrdinalIgnoreCase)
                         || string.Equals(item.Category, "setup", StringComparison.OrdinalIgnoreCase)
                         || item.Title.StartsWith(TrackGuideMapper.TitlePrefix, StringComparison.OrdinalIgnoreCase)
                         || item.Title.Contains("setup", StringComparison.OrdinalIgnoreCase))
                     .Take(5))
        {
            if (TrackGuideMapper.TryParse(source, out var guide))
            {
                packets.Add(new CoachEvidencePacket(
                    "Knowledge",
                    "Cached track guide",
                    "Info",
                    source.SourceType == KnowledgeSourceTypes.Web ? 0.88 : 0.82,
                    CoachEvidenceSourceType.Knowledge,
                    null,
                    [],
                    null,
                    TrackGuideFormatter.ToCoachSummary(guide)));
                continue;
            }

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

    private static void AddRaceAwarenessPackets(List<CoachEvidencePacket> packets, LiveRaceContext? raceContext)
    {
        if (raceContext is null)
        {
            return;
        }

        var track = FirstNonEmpty(raceContext.TrackName, raceContext.CircuitId);
        if (!string.IsNullOrWhiteSpace(track))
        {
            packets.Add(new CoachEvidencePacket(
                "RaceAwareness",
                "Track identity",
                "Info",
                0.96,
                CoachEvidenceSourceType.Session,
                raceContext.CurrentLap,
                [],
                null,
                $"Track is {track}."));
        }

        if (!string.IsNullOrWhiteSpace(raceContext.CarName))
        {
            packets.Add(new CoachEvidencePacket(
                "RaceAwareness",
                "Car identity",
                "Info",
                0.96,
                CoachEvidenceSourceType.Session,
                raceContext.CurrentLap,
                [],
                null,
                $"{raceContext.CarName}."));
        }

        if (raceContext.Position is { } position)
        {
            packets.Add(new CoachEvidencePacket(
                "RaceAwareness",
                "Race position",
                "Info",
                0.95,
                CoachEvidenceSourceType.Session,
                raceContext.CurrentLap,
                [],
                position,
                raceContext.TotalCars is { } total
                    ? $"Race position is P{position} of {total}."
                    : $"Race position is P{position}."));
        }

        if (raceContext.GapAheadSeconds is { } gapAhead)
        {
            packets.Add(new CoachEvidencePacket(
                "RaceAwareness",
                "Gap ahead",
                "Info",
                0.90,
                CoachEvidenceSourceType.Session,
                raceContext.CurrentLap,
                [],
                gapAhead,
                raceContext.CarAhead is { Length: > 0 } carAhead
                    ? $"Gap ahead to {carAhead} is {gapAhead.ToString("0.000", CultureInfo.InvariantCulture)}s."
                    : $"Gap ahead is {gapAhead.ToString("0.000", CultureInfo.InvariantCulture)}s."));
        }

        if (raceContext.GapBehindSeconds is { } gapBehind)
        {
            packets.Add(new CoachEvidencePacket(
                "RaceAwareness",
                "Gap behind",
                "Info",
                0.90,
                CoachEvidenceSourceType.Session,
                raceContext.CurrentLap,
                [],
                gapBehind,
                raceContext.CarBehind is { Length: > 0 } carBehind
                    ? $"Gap behind to {carBehind} is {gapBehind.ToString("0.000", CultureInfo.InvariantCulture)}s."
                    : $"Gap behind is {gapBehind.ToString("0.000", CultureInfo.InvariantCulture)}s."));
        }

        packets.Add(new CoachEvidencePacket(
            "RaceAwareness",
            "Race context confidence",
            raceContext.Confidence == RaceContextConfidence.Good ? "Info" : "Warning",
            0.80,
            CoachEvidenceSourceType.Session,
            raceContext.CurrentLap,
            [],
            null,
            $"Race context confidence is {raceContext.Confidence}. {raceContext.Diagnostics.Summary}"));
    }

    private static void AddTrackMemoryPackets(
        List<CoachEvidencePacket> packets,
        TrackMemoryRecord? memory,
        TrackMemoryComparison? comparison)
    {
        if (memory is { SessionCount: > 0 })
        {
            packets.Add(new CoachEvidencePacket(
                "TrackMemory",
                "Stored best lap",
                "Info",
                0.85,
                CoachEvidenceSourceType.Session,
                null,
                [],
                memory.BestLapSeconds,
                memory.BestLapSeconds is { } best
                    ? $"Stored session data for {memory.TrackName}: previous best lap {TrackMemoryService.FormatLapTime(best)}."
                    : $"Stored session data exists for {memory.TrackName}, but no best lap is recorded yet."));

            if (memory.AverageCleanLapSeconds is { } average)
            {
                packets.Add(new CoachEvidencePacket(
                    "TrackMemory",
                    "Stored average clean lap",
                    "Info",
                    0.85,
                    CoachEvidenceSourceType.Session,
                    null,
                    [],
                    average,
                    $"Stored session data for {memory.TrackName}: average clean lap {TrackMemoryService.FormatLapTime(average)}."));
            }

            if (memory.FuelUsedPerLap is { } fuelPerLap)
            {
                packets.Add(new CoachEvidencePacket(
                    "TrackMemory",
                    "Stored fuel use",
                    "Info",
                    0.80,
                    CoachEvidenceSourceType.Session,
                    null,
                    [],
                    fuelPerLap,
                    $"Stored session data for {memory.TrackName}: fuel use was about {fuelPerLap.ToString("0.00", CultureInfo.InvariantCulture)} L/lap."));
            }
        }

        if (comparison is not null && comparison.HasHistoricalData)
        {
            packets.Add(new CoachEvidencePacket(
                "TrackMemory",
                "Historical comparison",
                "Info",
                0.85,
                CoachEvidenceSourceType.Session,
                null,
                [],
                comparison.BestLapDeltaSeconds,
                comparison.Summary));
        }
    }

    private static void AddSessionMemorySummaryPackets(
        List<CoachEvidencePacket> packets,
        SessionMemorySummary? previousSummary,
        IReadOnlyList<SessionMemorySummary>? recentSummaries,
        TrackGuide? guide)
    {
        if (previousSummary is not null)
        {
            packets.Add(new CoachEvidencePacket(
                "TrackMemory",
                "Stored session summary",
                "Info",
                0.90,
                CoachEvidenceSourceType.Session,
                null,
                [],
                previousSummary.BestLapSeconds,
                TrackGuideZoneMapper.MapZoneReferences(previousSummary.OneLineSummary, guide)));

            if (previousSummary.ImprovementTargets.Count > 0)
            {
                packets.Add(new CoachEvidencePacket(
                    "TrackMemory",
                    "Stored improvement targets",
                    "Info",
                    0.88,
                    CoachEvidenceSourceType.Session,
                    null,
                    [],
                    null,
                    $"Stored session data for {previousSummary.TrackName}: focus on {string.Join("; ", TrackGuideZoneMapper.MapTextItems(previousSummary.ImprovementTargets.Take(3), guide))}."));
            }

            if (previousSummary.MainTimeLossZones.Count > 0)
            {
                packets.Add(new CoachEvidencePacket(
                    "TrackMemory",
                    "Stored time-loss zones",
                    "Info",
                    0.86,
                    CoachEvidenceSourceType.Session,
                    null,
                    [],
                    null,
                    $"Stored session data for {previousSummary.TrackName}: {string.Join("; ", TrackGuideZoneMapper.MapTextItems(previousSummary.MainTimeLossZones.Take(3), guide))}."));
            }
        }

        foreach (var summary in recentSummaries?.Take(3) ?? [])
        {
            if (previousSummary is not null && summary.SessionId == previousSummary.SessionId)
            {
                continue;
            }

            packets.Add(new CoachEvidencePacket(
                "TrackMemory",
                "Stored session history",
                "Info",
                0.82,
                CoachEvidenceSourceType.Session,
                null,
                [],
                summary.BestLapSeconds,
                summary.OneLineSummary));
        }
    }

    private static void AddStrategyKnowledgePackets(
        List<CoachEvidencePacket> packets,
        StrategyKnowledgeRecommendation? recommendation)
    {
        if (recommendation is null || recommendation.OverallSource == StrategyKnowledgeDataSource.Unavailable)
        {
            return;
        }

        packets.Add(new CoachEvidencePacket(
            "StrategyKnowledge",
            "Track-car strategy knowledge",
            "Info",
            recommendation.FuelPerLapSource == StrategyKnowledgeDataSource.LiveTelemetry ? 0.92 : 0.82,
            CoachEvidenceSourceType.Analytics,
            null,
            [],
            recommendation.ExpectedFuelPerLap,
            StrategyKnowledgeEvidenceFormatter.BuildSummary(recommendation)));

        if (recommendation.RecommendedStartingFuelLiters is { } startingFuel)
        {
            packets.Add(new CoachEvidencePacket(
                "StrategyKnowledge",
                "Recommended starting fuel",
                "Info",
                0.86,
                CoachEvidenceSourceType.Analytics,
                null,
                [],
                startingFuel,
                $"Recommended starting fuel {startingFuel.ToString("0.0", CultureInfo.InvariantCulture)} L ({recommendation.SourceLabel}, confidence {recommendation.ConfidenceLabel})."));
        }

        if (!string.IsNullOrWhiteSpace(recommendation.PitWindowEstimate))
        {
            packets.Add(new CoachEvidencePacket(
                "StrategyKnowledge",
                "Strategy pit window",
                "Info",
                0.84,
                CoachEvidenceSourceType.Analytics,
                null,
                [],
                null,
                recommendation.PitWindowEstimate));
        }
    }

    private static void AddTrackCarKnowledgePackets(
        List<CoachEvidencePacket> packets,
        TrackCarKnowledgeRecommendation? recommendation)
    {
        if (recommendation is null || !recommendation.IsAvailable)
        {
            return;
        }

        packets.Add(new CoachEvidencePacket(
            "TrackCarKnowledge",
            "Track-car knowledge summary",
            "Info",
            recommendation.FuelSource == TrackCarKnowledgeDataSource.LiveTelemetry ? 0.92 : 0.84,
            CoachEvidenceSourceType.Knowledge,
            null,
            [],
            recommendation.RecommendedFuelLiters,
            TrackCarKnowledgeEvidenceFormatter.BuildSummary(recommendation)));

        if (recommendation.RecommendedFuelLiters is { } fuelTotal)
        {
            packets.Add(new CoachEvidencePacket(
                "TrackCarKnowledge",
                "Fuel planning estimate",
                "Info",
                recommendation.FuelSource == TrackCarKnowledgeDataSource.LiveTelemetry ? 0.9 : 0.82,
                CoachEvidenceSourceType.Knowledge,
                null,
                [],
                fuelTotal,
                recommendation.LiveFuelNote ?? recommendation.KnowledgeFuelNote ?? recommendation.FuelUsageExpectation));
        }

        packets.Add(new CoachEvidencePacket(
            "TrackCarKnowledge",
            "Brake demand baseline",
            "Info",
            0.82,
            CoachEvidenceSourceType.Knowledge,
            null,
            [],
            null,
            recommendation.BrakeDemand));

        packets.Add(new CoachEvidencePacket(
            "TrackCarKnowledge",
            "Tyre baseline",
            "Info",
            0.82,
            CoachEvidenceSourceType.Knowledge,
            null,
            [],
            null,
            $"{recommendation.TyreWearExpectation} Warmup: {recommendation.TyreWarmupExpectation}"));
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

    private static void AddDriverCoachingPackets(
        List<CoachEvidencePacket> packets,
        DriverCoachingRecommendation? coaching)
    {
        if (coaching is not { HasData: true })
        {
            return;
        }

        packets.Add(new CoachEvidencePacket(
            "DriverCoaching",
            "Coaching summary",
            "Info",
            0.92,
            CoachEvidenceSourceType.Analytics,
            null,
            [],
            null,
            coaching.Summary));

        packets.Add(new CoachEvidencePacket(
            "DriverCoaching",
            "Progress trend",
            coaching.ProgressTrend == DriverProgressTrend.Declining ? "Warning" : "Info",
            0.88,
            CoachEvidenceSourceType.Analytics,
            null,
            [],
            null,
            coaching.ProgressTrendSummary));

        if (!string.IsNullOrWhiteSpace(coaching.PreviousSessionDeltaSummary))
        {
            packets.Add(new CoachEvidencePacket(
                "DriverCoaching",
                "Previous session comparison",
                "Info",
                0.86,
                CoachEvidenceSourceType.Analytics,
                null,
                [],
                null,
                coaching.PreviousSessionDeltaSummary));
        }

        foreach (var weakness in coaching.RepeatedWeaknesses.Take(3))
        {
            packets.Add(new CoachEvidencePacket(
                "DriverCoaching",
                "Repeated weakness",
                "Warning",
                0.84,
                CoachEvidenceSourceType.Analytics,
                null,
                [],
                null,
                weakness));
        }

        foreach (var target in coaching.TopCoachingTargets.Take(3))
        {
            packets.Add(new CoachEvidencePacket(
                "DriverCoaching",
                "Coaching target",
                "Info",
                0.90,
                CoachEvidenceSourceType.Analytics,
                null,
                [],
                null,
                target));
        }
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

    private static string? FirstNonEmpty(params string?[] values)
    {
        foreach (var value in values)
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value.Trim();
            }
        }

        return null;
    }
}
