using RaceEngineer.Core.Session;
using RaceEngineer.Core.SessionContext;

namespace RaceEngineer.Core.Coaching.Ai;

public static class EngineerAiContextBuilder
{
    private const int MaxFacts = 10;

    private static readonly string[] Guardrails =
    [
        "Use only the provided facts. Do not invent telemetry, lap times, fuel, or strategy.",
        "If the facts do not support an answer, say the data is unavailable.",
        "Do not make pit or strategy calls unless strategy callouts are allowed and confidence is sufficient.",
        "Keep the answer short, race-safe, and prioritize safety and uncertainty."
    ];

    public static EngineerAiContext Build(
        SessionState session,
        string question,
        CoachContext? context,
        CoachEvidenceBundle evidence)
    {
        var topics = DetectTopics(question);
        var packets = SelectPackets(evidence, topics, context?.SessionContext);
        var facts = packets
            .Select(ToFact)
            .Take(MaxFacts)
            .ToArray();

        var latest = session.LatestSnapshot;
        var sessionContext = context?.SessionContext ?? SessionContextAssessment.InitialLive;

        return new EngineerAiContext(
            question.Trim(),
            sessionContext.SessionModeLabel,
            sessionContext.StrategyConfidenceLabel,
            sessionContext.AllowPitStrategyCallouts && sessionContext.StrategyConfidence != StrategyConfidenceLevel.Low,
            latest?.Lap.LapNumber ?? session.LastLap?.LapNumber,
            latest?.Condition.Fuel,
            facts,
            Guardrails);
    }

    private static IReadOnlyList<CoachEvidencePacket> SelectPackets(
        CoachEvidenceBundle evidence,
        IReadOnlyList<CoachEvidenceTopic> topics,
        SessionContextAssessment? sessionContext)
    {
        if (topics.Count == 0)
        {
            return evidence.Packets
                .OrderByDescending(packet => packet.Confidence)
                .ThenBy(packet => packet.Summary, StringComparer.Ordinal)
                .Take(MaxFacts)
                .ToArray();
        }

        var selected = new List<CoachEvidencePacket>();
        foreach (var topic in topics)
        {
            if (topic == CoachEvidenceTopic.Strategy
                && sessionContext is { AllowPitStrategyCallouts: false })
            {
                continue;
            }

            selected.AddRange(evidence.Select(topic));
        }

        return selected
            .DistinctBy(packet => $"{packet.Category}|{packet.Summary}|{packet.Explanation}")
            .OrderByDescending(packet => packet.Confidence)
            .ThenBy(packet => packet.Summary, StringComparer.Ordinal)
            .Take(MaxFacts)
            .ToArray();
    }

    private static EngineerAiFact ToFact(CoachEvidencePacket packet) =>
        new(
            packet.Category,
            packet.Summary,
            packet.Explanation,
            packet.Confidence,
            packet.SourceType.ToString(),
            packet.RelatedLapNumber);

    private static IReadOnlyList<CoachEvidenceTopic> DetectTopics(string question)
    {
        var text = question.ToLowerInvariant();
        var topics = new List<CoachEvidenceTopic>();

        if (ContainsAny(text, CoachQueryPhrases.LosingTime))
        {
            topics.Add(CoachEvidenceTopic.LosingTime);
        }

        if (ContainsAny(text, CoachQueryPhrases.Braking))
        {
            topics.Add(CoachEvidenceTopic.Braking);
        }

        if (ContainsAny(text, CoachQueryPhrases.Throttle))
        {
            topics.Add(CoachEvidenceTopic.Throttle);
        }

        if (ContainsAny(text, CoachQueryPhrases.Improvement))
        {
            topics.Add(CoachEvidenceTopic.Improvement);
        }

        if (ContainsAny(text, CoachQueryPhrases.RacePace))
        {
            topics.Add(CoachEvidenceTopic.RacePace);
        }

        if (ContainsAny(text, CoachQueryPhrases.Incidents))
        {
            topics.Add(CoachEvidenceTopic.Incidents);
        }

        if (ContainsAny(text, CoachQueryPhrases.Fuel) || text.Contains("goriv", StringComparison.Ordinal))
        {
            topics.Add(CoachEvidenceTopic.Fuel);
        }

        if (ContainsAny(text, CoachQueryPhrases.Strategy) || ContainsAny(text, CoachQueryPhrases.Pit))
        {
            topics.Add(CoachEvidenceTopic.Strategy);
        }

        if (ContainsAny(text, "compare my laps", "compare laps", "lap comparison"))
        {
            topics.Add(CoachEvidenceTopic.LapComparison);
        }

        return topics;
    }

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
