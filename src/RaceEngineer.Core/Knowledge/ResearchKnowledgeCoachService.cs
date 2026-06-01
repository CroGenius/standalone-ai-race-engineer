using RaceEngineer.Core.Coaching.Ai;
using RaceEngineer.Core.Events;
using RaceEngineer.Core.Session;

namespace RaceEngineer.Core.Knowledge;

public static class ResearchKnowledgeCoachService
{
    public static IReadOnlyList<ResearchKnowledgeItem> SelectRelevantItems(
        WebResearchBundle? bundle,
        string question)
    {
        if (bundle is not { HasResearch: true })
        {
            return [];
        }

        var matches = bundle.Items
            .Where(item => ResearchTopics.MatchesQuestion(item.Topic, question))
            .ToArray();
        return matches.Length > 0 ? matches : bundle.Items.Take(3).ToArray();
    }

    public static string? BuildResearchLine(WebResearchBundle? bundle, string question)
    {
        var item = SelectRelevantItems(bundle, question).FirstOrDefault();
        return item is null ? null : $"Cached research: {item.Summary}";
    }

    public static string? BuildTelemetryLine(SessionState session, IReadOnlyList<TelemetryEvent> events)
    {
        var recent = events
            .Where(item => item.Type is EventType.ThrottleHesitation or EventType.UnstableBraking or EventType.AbruptBrakeRelease or EventType.TractionLoss)
            .TakeLast(2)
            .ToArray();
        if (recent.Length > 0)
        {
            return $"Live telemetry: recent {recent[^1].Type} on lap {recent[^1].LapNumber?.ToString() ?? "unknown"}.";
        }

        if (session.FuelUsedPerLap is { } fuel)
        {
            return $"Live telemetry: current fuel burn about {fuel:0.00} L/lap.";
        }

        return null;
    }

    public static string BuildCombinedAnswer(
        string question,
        WebResearchBundle? bundle,
        SessionState session,
        IReadOnlyList<TelemetryEvent> events,
        TrackCarKnowledgeRecommendation? catalog = null)
    {
        var parts = new List<string>();
        var researchLine = BuildResearchLine(bundle, question);
        if (!string.IsNullOrWhiteSpace(researchLine))
        {
            parts.Add(researchLine);
        }
        else if (catalog is { IsAvailable: true })
        {
            parts.Add($"Built-in catalog: {Shorten(catalog.KnowledgeSummary)}");
        }

        var telemetryLine = BuildTelemetryLine(session, events);
        if (!string.IsNullOrWhiteSpace(telemetryLine))
        {
            parts.Add(telemetryLine);
        }

        if (parts.Count == 0)
        {
            return "Research and telemetry are unavailable for this question.";
        }

        return string.Join(" ", parts);
    }

    public static IReadOnlyList<EngineerAiFact> BuildAiFacts(
        WebResearchBundle? bundle,
        string question)
    {
        if (bundle is not { HasResearch: true })
        {
            return [];
        }

        return SelectRelevantItems(bundle, question)
            .Take(4)
            .Select(item => new EngineerAiFact(
                "WebResearch",
                item.Topic.Replace('_', ' '),
                string.IsNullOrWhiteSpace(item.KeyFacts.FirstOrDefault())
                    ? item.Summary
                    : $"{item.Summary} Key fact: {item.KeyFacts[0]}",
                0.82,
                "cached research",
                null))
            .ToArray();
    }

    public static string BuildEvidenceSummary(ResearchKnowledgeItem item) =>
        $"Cached research ({item.ProviderName}, confidence {item.ConfidenceLabel}): {Shorten(item.Summary)}";

    private static string Shorten(string value) =>
        value.Length <= 160 ? value : value[..157] + "...";
}
