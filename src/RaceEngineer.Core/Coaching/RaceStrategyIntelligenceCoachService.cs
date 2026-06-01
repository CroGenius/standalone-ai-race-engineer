using System.Globalization;
using RaceEngineer.Core.Session;
using RaceEngineer.Core.Strategy;

namespace RaceEngineer.Core.Coaching;

public static class RaceStrategyIntelligenceCoachService
{
    public static CoachMessage Answer(string query, SessionState session, CoachContext? context)
    {
        var intelligence = context?.RaceStrategyIntelligence
            ?? RaceStrategyIntelligenceService.FromCoachContext(session, context, query);
        var text = query.ToLowerInvariant();

        if (ContainsAny(text, "fuel for"))
        {
            return FormatFuelForLapsAnswer(intelligence, query);
        }

        if (ContainsAny(text, "how many laps left", "how many laps can i do", "how many laps remaining", "laps left"))
        {
            return FormatLapsLeftAnswer(intelligence);
        }

        if (ContainsAny(text, "can i finish", "imam li dovoljno goriva"))
        {
            return FormatCanFinishAnswer(intelligence);
        }

        if (ContainsAny(text, "save fuel", "should i save fuel", "fuel saving", "fuel target", "fuel margin"))
        {
            return FormatFuelSavingAnswer(intelligence, text);
        }

        if (ContainsAny(text, "tyre outlook", "tire outlook", "next 5 laps", "stint viability"))
        {
            return FormatTyreOutlookAnswer(intelligence);
        }

        if (ContainsAny(text, "tyre risk", "tire risk"))
        {
            return FormatTyreRiskAnswer(intelligence);
        }

        if (ContainsAny(text, "should i pit", "pit now", "pit window", "stay out or pit", "pit now or stay out", "stay out"))
        {
            return FormatPitAnswer(intelligence);
        }

        if (ContainsAny(text, "strategy recommendation", "what is my strategy", "strategija"))
        {
            return FormatStrategySummaryAnswer(intelligence);
        }

        return FormatStrategySummaryAnswer(intelligence);
    }

    private static CoachMessage FormatFuelForLapsAnswer(
        RaceStrategyIntelligenceRecommendation intelligence,
        string query)
    {
        if (!intelligence.Fuel.HasData)
        {
            return Unavailable(intelligence.Fuel.Availability);
        }

        var fuel = intelligence.Fuel;
        var action = $"{fuel.Recommendation} Live telemetry burn {RaceStrategyIntelligenceService.FormatBurnRate(fuel.ConservativeBurnRatePerLap)}.";
        var evidence = BuildEvidence(intelligence.Fuel.EvidenceLines);
        return Message(action, evidence, []);
    }

    private static CoachMessage FormatLapsLeftAnswer(RaceStrategyIntelligenceRecommendation intelligence)
    {
        if (!intelligence.Fuel.HasData || intelligence.Fuel.ProjectedLapsRemaining is not { } laps)
        {
            return Unavailable(intelligence.Fuel.Availability);
        }

        var action =
            $"About {laps.ToString("0.0", CultureInfo.InvariantCulture)} laps left on current fuel at {RaceStrategyIntelligenceService.FormatBurnRate(intelligence.Fuel.ConservativeBurnRatePerLap)}.";
        return Message(action, BuildEvidence(intelligence.Fuel.EvidenceLines), []);
    }

    private static CoachMessage FormatCanFinishAnswer(RaceStrategyIntelligenceRecommendation intelligence)
    {
        if (!intelligence.Fuel.HasData)
        {
            return Unavailable(intelligence.Fuel.Availability);
        }

        var fuel = intelligence.Fuel;
        var verdict = fuel.CanFinishSafely
            ? fuel.FuelSavingRequired
                ? "Maybe, but fuel is marginal. Save fuel or plan a stop."
                : "Yes, you can finish on current fuel."
            : "No, you likely cannot finish safely on current fuel.";
        var parts = new List<string> { verdict };
        if (fuel.ProjectedLapsRemaining is { } laps)
        {
            parts.Add($"About {laps.ToString("0.0", CultureInfo.InvariantCulture)} laps projected.");
        }

        parts.Add(fuel.Recommendation);
        return Message(string.Join(" ", parts), BuildEvidence(fuel.EvidenceLines), []);
    }

    private static CoachMessage FormatFuelSavingAnswer(
        RaceStrategyIntelligenceRecommendation intelligence,
        string query)
    {
        if (!intelligence.Fuel.HasData)
        {
            return Unavailable(intelligence.Fuel.Availability);
        }

        var fuel = intelligence.Fuel;
        var action = query.Contains("target", StringComparison.Ordinal) && fuel.FuelTargetPerLap is { } target
            ? $"Fuel target {RaceStrategyIntelligenceService.FormatBurnRate(target)}."
            : query.Contains("margin", StringComparison.Ordinal) && fuel.FuelMarginLiters is { } margin
                ? $"Fuel margin {RaceStrategyIntelligenceService.FormatLiters(margin)}."
                : fuel.FuelSavingRequired
                    ? $"Yes. {fuel.Recommendation}"
                    : $"No. {fuel.Recommendation}";

        return Message(action, BuildEvidence(fuel.EvidenceLines), []);
    }

    private static CoachMessage FormatTyreOutlookAnswer(RaceStrategyIntelligenceRecommendation intelligence)
    {
        if (!intelligence.Tyre.HasData)
        {
            return Unavailable(intelligence.Tyre.Availability);
        }

        return Message(intelligence.Tyre.OutlookSummary, BuildEvidence(intelligence.Tyre.EvidenceLines), []);
    }

    private static CoachMessage FormatTyreRiskAnswer(RaceStrategyIntelligenceRecommendation intelligence)
    {
        if (!intelligence.Tyre.HasData)
        {
            return Unavailable(intelligence.Tyre.Availability);
        }

        var tyre = intelligence.Tyre;
        var action = $"Tyre degradation risk is {tyre.DegradationRisk}. {tyre.OutlookSummary}";
        return Message(action, BuildEvidence(tyre.EvidenceLines), []);
    }

    private static CoachMessage FormatPitAnswer(RaceStrategyIntelligenceRecommendation intelligence)
    {
        if (!intelligence.Pit.HasData)
        {
            return Unavailable(intelligence.Pit.Availability);
        }

        var pit = intelligence.Pit;
        var action =
            $"{pit.RecommendationSummary} Window L{pit.EarliestSensibleStopLap}-L{pit.LatestSensibleStopLap}. Risk {pit.CurrentRisk}. {pit.ExpectedGainLoss}";
        return Message(action, BuildEvidence(pit.EvidenceLines), []);
    }

    private static CoachMessage FormatStrategySummaryAnswer(RaceStrategyIntelligenceRecommendation intelligence)
    {
        if (!intelligence.HasData)
        {
            return Unavailable(intelligence.Availability);
        }

        return Message(intelligence.StrategySummary, BuildEvidence(intelligence.EvidenceLines), []);
    }

    private static List<string> BuildEvidence(IReadOnlyList<string> lines) =>
        lines.Take(8).ToList();

    private static CoachMessage Message(string action, IReadOnlyList<string> evidence, IReadOnlyList<Guid> eventIds) =>
        new(
            "coach",
            evidence.Count == 0 ? action : $"{action}{Environment.NewLine}{Environment.NewLine}Evidence:{Environment.NewLine}{string.Join(Environment.NewLine, evidence.Select(item => $"- {item}"))}",
            eventIds,
            null,
            []);

    private static CoachMessage Unavailable(string reason) =>
        new("coach", reason, [], reason, []);

    private static bool ContainsAny(string text, params string[] phrases)
    {
        foreach (var phrase in phrases)
        {
            if (text.Contains(phrase, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }
}
