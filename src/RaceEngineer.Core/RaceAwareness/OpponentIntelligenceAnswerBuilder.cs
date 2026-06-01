using System.Globalization;
using RaceEngineer.Core.Telemetry;

namespace RaceEngineer.Core.RaceAwareness;

public static class OpponentIntelligenceAnswerBuilder
{
    public const string UnavailableMessage = "Opponent data is unavailable from telemetry.";

    public static RaceAwarenessAnswer Build(
        RaceAwarenessSubtopic subtopic,
        OpponentIntelligenceRecommendation? intelligence,
        RaceAwarenessRoutingResult routing,
        TelemetryProviderCapabilities? telemetryProviderCapabilities = null,
        TelemetryProviderStatus? telemetryProviderStatus = null,
        string? telemetryProviderDiagnostics = null)
    {
        if (intelligence is null || !intelligence.HasOpponentData || intelligence.Current is null)
        {
            return new RaceAwarenessAnswer(
                TelemetryProviderCapabilityMessages.OpponentDataUnavailable(
                    telemetryProviderCapabilities,
                    telemetryProviderStatus,
                    telemetryProviderDiagnostics),
                [],
                routing with
                {
                    FallbackReason = TelemetryProviderCapabilityMessages.IsProviderUnavailable(telemetryProviderStatus)
                        ? "telemetry provider unavailable"
                        : telemetryProviderCapabilities is not null && !telemetryProviderCapabilities.OpponentGaps
                            ? "provider does not expose opponent gaps"
                            : "Opponent gap or position fields are missing from telemetry."
                });
        }

        return subtopic switch
        {
            RaceAwarenessSubtopic.CatchingAhead => BuildCatchingAhead(
                intelligence,
                routing,
                telemetryProviderCapabilities,
                telemetryProviderStatus,
                telemetryProviderDiagnostics),
            RaceAwarenessSubtopic.PullingAway => BuildPullingAway(
                intelligence,
                routing,
                telemetryProviderCapabilities,
                telemetryProviderStatus,
                telemetryProviderDiagnostics),
            RaceAwarenessSubtopic.AttackOpportunity => BuildAttack(intelligence, routing),
            RaceAwarenessSubtopic.DefendRecommendation => BuildDefend(intelligence, routing),
            RaceAwarenessSubtopic.RaceSituation => BuildRaceSituation(intelligence, routing),
            RaceAwarenessSubtopic.OpponentIdentity => BuildOpponentIdentity(
                intelligence,
                routing,
                telemetryProviderCapabilities,
                telemetryProviderStatus,
                telemetryProviderDiagnostics),
            _ => BuildRaceSituation(intelligence, routing)
        };
    }

    private static RaceAwarenessAnswer BuildCatchingAhead(
        OpponentIntelligenceRecommendation intelligence,
        RaceAwarenessRoutingResult routing,
        TelemetryProviderCapabilities? telemetryProviderCapabilities,
        TelemetryProviderStatus? telemetryProviderStatus,
        string? telemetryProviderDiagnostics)
    {
        var trend = intelligence.GapTrend;
        if (trend.AheadDirection == GapTrendDirection.Unavailable)
        {
            return new RaceAwarenessAnswer(
                TelemetryProviderCapabilityMessages.GapTrendAheadUnavailable(
                    telemetryProviderCapabilities,
                    telemetryProviderStatus,
                    telemetryProviderDiagnostics),
                [],
                routing with
                {
                    FallbackReason = TelemetryProviderCapabilityMessages.IsProviderUnavailable(telemetryProviderStatus)
                        ? "telemetry provider unavailable"
                        : telemetryProviderCapabilities is not null && !telemetryProviderCapabilities.OpponentGaps
                            ? "provider does not expose opponent gaps"
                            : "Not enough gap samples to estimate trend."
                });
        }

        var content = trend.AheadDirection switch
        {
            GapTrendDirection.Gaining =>
                trend.GapAheadChangePerLapSeconds is { } change
                    ? $"You are gaining {FormatSeconds(Math.Abs(change))} per lap on the car ahead ({trend.Confidence.ToString().ToLowerInvariant()} confidence)."
                    : $"You are closing on the car ahead ({trend.Confidence.ToString().ToLowerInvariant()} confidence).",
            GapTrendDirection.Losing =>
                trend.GapAheadChangePerLapSeconds is { } change
                    ? $"You are losing {FormatSeconds(Math.Abs(change))} per lap to the car ahead ({trend.Confidence.ToString().ToLowerInvariant()} confidence)."
                    : $"The car ahead is pulling away ({trend.Confidence.ToString().ToLowerInvariant()} confidence).",
            _ => $"Gap to the car ahead is stable ({trend.Confidence.ToString().ToLowerInvariant()} confidence)."
        };

        return new RaceAwarenessAnswer(content, [trend.Summary], routing with { FallbackReason = null });
    }

    private static RaceAwarenessAnswer BuildPullingAway(
        OpponentIntelligenceRecommendation intelligence,
        RaceAwarenessRoutingResult routing,
        TelemetryProviderCapabilities? telemetryProviderCapabilities,
        TelemetryProviderStatus? telemetryProviderStatus,
        string? telemetryProviderDiagnostics)
    {
        var trend = intelligence.GapTrend;
        if (trend.BehindDirection == GapTrendDirection.Unavailable)
        {
            return new RaceAwarenessAnswer(
                TelemetryProviderCapabilityMessages.GapTrendBehindUnavailable(
                    telemetryProviderCapabilities,
                    telemetryProviderStatus,
                    telemetryProviderDiagnostics),
                [],
                routing with
                {
                    FallbackReason = TelemetryProviderCapabilityMessages.IsProviderUnavailable(telemetryProviderStatus)
                        ? "telemetry provider unavailable"
                        : telemetryProviderCapabilities is not null && !telemetryProviderCapabilities.OpponentGaps
                            ? "provider does not expose opponent gaps"
                            : "Not enough gap samples to estimate trend."
                });
        }

        var content = trend.BehindDirection switch
        {
            GapTrendDirection.Gaining =>
                trend.GapBehindChangePerLapSeconds is { } change
                    ? $"You are pulling away by {FormatSeconds(Math.Abs(change))} per lap from the car behind ({trend.Confidence.ToString().ToLowerInvariant()} confidence)."
                    : $"You are pulling away from the car behind ({trend.Confidence.ToString().ToLowerInvariant()} confidence).",
            GapTrendDirection.Losing =>
                trend.GapBehindChangePerLapSeconds is { } change
                    ? $"The car behind is gaining {FormatSeconds(Math.Abs(change))} per lap ({trend.Confidence.ToString().ToLowerInvariant()} confidence)."
                    : $"The car behind is closing ({trend.Confidence.ToString().ToLowerInvariant()} confidence).",
            _ => $"Gap to the car behind is stable ({trend.Confidence.ToString().ToLowerInvariant()} confidence)."
        };

        return new RaceAwarenessAnswer(content, [trend.Summary], routing with { FallbackReason = null });
    }

    private static RaceAwarenessAnswer BuildAttack(
        OpponentIntelligenceRecommendation intelligence,
        RaceAwarenessRoutingResult routing)
    {
        var parts = new List<string>();
        if (intelligence.Battle.Type == RaceBattleType.AttackOpportunity)
        {
            parts.Add($"{intelligence.Battle.Summary} ({intelligence.Battle.Confidence.ToString().ToLowerInvariant()} confidence).");
        }
        else if (intelligence.GapTrend.AheadDirection == GapTrendDirection.Gaining)
        {
            parts.Add("You are closing, but no high-confidence attack window is confirmed yet.");
        }
        else
        {
            parts.Add("No clear overtake opportunity is confirmed from current gaps and trend.");
        }

        if (!string.IsNullOrWhiteSpace(intelligence.AttackZoneRecommendation))
        {
            parts.Add(intelligence.AttackZoneRecommendation);
        }

        if (intelligence.StoredOvertakeZones.Count > 0)
        {
            parts.Add($"Stored overtake zones for this track/car: {string.Join("; ", intelligence.StoredOvertakeZones.Take(2))}.");
        }

        if (!string.IsNullOrWhiteSpace(intelligence.StrategyInsight.UndercutOpportunity))
        {
            parts.Add(intelligence.StrategyInsight.UndercutOpportunity);
        }

        return new RaceAwarenessAnswer(
            string.Join(" ", parts),
            [intelligence.Battle.Summary],
            routing with { FallbackReason = null });
    }

    private static RaceAwarenessAnswer BuildDefend(
        OpponentIntelligenceRecommendation intelligence,
        RaceAwarenessRoutingResult routing)
    {
        var parts = new List<string>();
        if (intelligence.Battle.Type == RaceBattleType.DefensiveSituation)
        {
            parts.Add($"{intelligence.Battle.Summary} ({intelligence.Battle.Confidence.ToString().ToLowerInvariant()} confidence).");
        }
        else if (intelligence.GapTrend.BehindDirection == GapTrendDirection.Losing)
        {
            parts.Add("Pressure is building behind, but no high-confidence defend call is confirmed yet.");
        }
        else
        {
            parts.Add("No immediate defensive threat is confirmed from current gaps and trend.");
        }

        if (!string.IsNullOrWhiteSpace(intelligence.DefendZoneRecommendation))
        {
            parts.Add(intelligence.DefendZoneRecommendation);
        }

        if (intelligence.StoredDefensiveWeaknesses.Count > 0)
        {
            parts.Add($"Stored defensive notes: {string.Join("; ", intelligence.StoredDefensiveWeaknesses.Take(2))}.");
        }

        if (!string.IsNullOrWhiteSpace(intelligence.StrategyInsight.OvercutOpportunity))
        {
            parts.Add(intelligence.StrategyInsight.OvercutOpportunity);
        }

        return new RaceAwarenessAnswer(
            string.Join(" ", parts),
            [intelligence.Battle.Summary],
            routing with { FallbackReason = null });
    }

    private static RaceAwarenessAnswer BuildRaceSituation(
        OpponentIntelligenceRecommendation intelligence,
        RaceAwarenessRoutingResult routing)
    {
        var parts = new List<string> { intelligence.Summary };
        if (intelligence.StoredBattleOutcomes.Count > 0)
        {
            parts.Add($"Stored battle history: {string.Join("; ", intelligence.StoredBattleOutcomes.Take(2))}.");
        }

        return new RaceAwarenessAnswer(
            string.Join(" ", parts),
            [intelligence.Battle.StatusLabel],
            routing with { FallbackReason = null });
    }

    private static RaceAwarenessAnswer BuildOpponentIdentity(
        OpponentIntelligenceRecommendation intelligence,
        RaceAwarenessRoutingResult routing,
        TelemetryProviderCapabilities? telemetryProviderCapabilities,
        TelemetryProviderStatus? telemetryProviderStatus,
        string? telemetryProviderDiagnostics)
    {
        var current = intelligence.Current!;
        var parts = new List<string>();
        if (current.GapAheadSeconds is { } gapAhead)
        {
            parts.Add(!string.IsNullOrWhiteSpace(current.CarAhead)
                ? $"Car ahead: {current.CarAhead} ({FormatSeconds(gapAhead)})."
                : $"Gap ahead: {FormatSeconds(gapAhead)}.");
        }
        else if (!string.IsNullOrWhiteSpace(current.CarAhead))
        {
            parts.Add($"Car ahead: {current.CarAhead}.");
        }

        if (current.GapBehindSeconds is { } gapBehind)
        {
            parts.Add(!string.IsNullOrWhiteSpace(current.CarBehind)
                ? $"Car behind: {current.CarBehind} ({FormatSeconds(gapBehind)})."
                : $"Gap behind: {FormatSeconds(gapBehind)}.");
        }
        else if (!string.IsNullOrWhiteSpace(current.CarBehind))
        {
            parts.Add($"Car behind: {current.CarBehind}.");
        }

        if (parts.Count == 0)
        {
            return new RaceAwarenessAnswer(
                TelemetryProviderCapabilityMessages.OpponentDataUnavailable(
                    telemetryProviderCapabilities,
                    telemetryProviderStatus,
                    telemetryProviderDiagnostics),
                [],
                routing with
                {
                    FallbackReason = TelemetryProviderCapabilityMessages.IsProviderUnavailable(telemetryProviderStatus)
                        ? "telemetry provider unavailable"
                        : telemetryProviderCapabilities is not null && !telemetryProviderCapabilities.OpponentGaps
                            ? "provider does not expose opponent gaps"
                            : "car_ahead/car_behind missing from telemetry."
                });
        }

        parts.Add(intelligence.Battle.Summary);
        return new RaceAwarenessAnswer(
            string.Join(" ", parts),
            parts.ToArray(),
            routing with { FallbackReason = null });
    }

    private static string FormatSeconds(double value) =>
        $"{value.ToString("0.0", CultureInfo.InvariantCulture)}s";
}
