using RaceEngineer.Core.Events;
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
        var primaryTopic = CoachQueryTopicClassifier.ClassifyPrimary(question);
        var latest = session.LatestSnapshot;
        var sessionContext = context?.SessionContext ?? SessionContextAssessment.InitialLive;

        if (!DrivingTechniqueGate.Evaluate(primaryTopic, session, sessionContext).Allowed)
        {
            return new EngineerAiContext(
                question.Trim(),
                sessionContext.SessionModeLabel,
                sessionContext.StrategyConfidenceLabel,
                sessionContext.AllowPitStrategyCallouts && sessionContext.StrategyConfidence != StrategyConfidenceLevel.Low,
                latest?.Lap.LapNumber ?? session.LastLap?.LapNumber,
                primaryTopic is CoachQueryTopic.FuelAmount or CoachQueryTopic.FuelStrategy or CoachQueryTopic.Strategy or CoachQueryTopic.Pit
                    ? latest?.Condition.Fuel
                    : null,
                [],
                Guardrails);
        }

        var evidenceTopic = CoachQueryTopicClassifier.ToEvidenceTopic(primaryTopic);
        var packets = SelectPackets(evidence, primaryTopic, evidenceTopic, sessionContext);
        var facts = packets
            .Select(ToFact)
            .Take(MaxFacts)
            .ToArray();

        return new EngineerAiContext(
            question.Trim(),
            sessionContext.SessionModeLabel,
            sessionContext.StrategyConfidenceLabel,
            sessionContext.AllowPitStrategyCallouts && sessionContext.StrategyConfidence != StrategyConfidenceLevel.Low,
            latest?.Lap.LapNumber ?? session.LastLap?.LapNumber,
            primaryTopic is CoachQueryTopic.FuelAmount or CoachQueryTopic.FuelStrategy or CoachQueryTopic.Strategy or CoachQueryTopic.Pit
                ? latest?.Condition.Fuel
                : null,
            facts,
            Guardrails);
    }

    private static IReadOnlyList<CoachEvidencePacket> SelectPackets(
        CoachEvidenceBundle evidence,
        CoachQueryTopic primaryTopic,
        CoachEvidenceTopic? evidenceTopic,
        SessionContextAssessment? sessionContext)
    {
        if (primaryTopic is CoachQueryTopic.Position
            or CoachQueryTopic.TrackIdentity
            or CoachQueryTopic.CarIdentity
            or CoachQueryTopic.Tyre
            or CoachQueryTopic.PushConfidence
            or CoachQueryTopic.LapTime
            or CoachQueryTopic.FuelStrategy
            or CoachQueryTopic.RaceAwareness
            or CoachQueryTopic.TrackMemory
            or CoachQueryTopic.TrackGuide)
        {
            return FilterByPrimaryTopic(evidence.Packets, primaryTopic);
        }

        if (evidenceTopic is null)
        {
            return evidence.Packets
                .Where(packet => !LooksLikeFuelPacket(packet))
                .OrderByDescending(packet => packet.Confidence)
                .ThenBy(packet => packet.Summary, StringComparer.Ordinal)
                .Take(MaxFacts)
                .ToArray();
        }

        if (evidenceTopic == CoachEvidenceTopic.Strategy
            && sessionContext is { AllowPitStrategyCallouts: false })
        {
            return FilterByPrimaryTopic(evidence.Packets, CoachQueryTopic.FuelAmount);
        }

        var selected = evidence.Select(evidenceTopic.Value).ToArray();
        if (selected.Length > 0)
        {
            return PrioritizeStoredSessionMemory(selected, evidence.Packets, primaryTopic);
        }

        return PrioritizeStoredSessionMemory(
            FilterByPrimaryTopic(evidence.Packets, primaryTopic),
            evidence.Packets,
            primaryTopic);
    }

    private static IReadOnlyList<CoachEvidencePacket> PrioritizeStoredSessionMemory(
        IReadOnlyList<CoachEvidencePacket> selected,
        IReadOnlyList<CoachEvidencePacket> allPackets,
        CoachQueryTopic primaryTopic)
    {
        if (primaryTopic is not (CoachQueryTopic.Improvement
            or CoachQueryTopic.LapComparison
            or CoachQueryTopic.TrackMemory
            or CoachQueryTopic.TrackGuide
            or CoachQueryTopic.LosingTime))
        {
            return selected.Take(MaxFacts).ToArray();
        }

        var stored = allPackets.Where(packet => packet.Category == "TrackMemory").ToArray();
        if (stored.Length == 0)
        {
            return selected.Take(MaxFacts).ToArray();
        }

        return stored
            .Concat(selected.Where(packet => packet.Category != "TrackMemory"))
            .Take(MaxFacts)
            .ToArray();
    }

    private static IReadOnlyList<CoachEvidencePacket> FilterByPrimaryTopic(
        IReadOnlyList<CoachEvidencePacket> packets,
        CoachQueryTopic topic)
    {
        IEnumerable<CoachEvidencePacket> filtered = topic switch
        {
            CoachQueryTopic.Tyre => packets.Where(packet =>
                LooksLikeTyrePacket(packet) || packet.Category.Contains("TyreIntelligence", StringComparison.Ordinal)),
            CoachQueryTopic.PushConfidence => packets.Where(packet =>
                LooksLikeTyrePacket(packet)
                    || packet.Category is "Braking" or "Throttle" or "Pace" or "Incident"
                    || packet.Category.Contains("TyreIntelligence", StringComparison.Ordinal)),
            CoachQueryTopic.FuelStrategy => packets.Where(packet =>
                LooksLikeFuelStrategyPacket(packet) || packet.Category == "StrategyKnowledge"),
            CoachQueryTopic.TrackMemory => packets.Where(packet => packet.Category == "TrackMemory"),
            CoachQueryTopic.TrackGuide => packets.Where(packet =>
                packet.Category == "TrackGuide"
                    || (packet.Category == "Knowledge" && packet.Summary == "Cached track guide")),
            CoachQueryTopic.TrackIdentity => packets.Where(packet =>
                packet.Category == "RaceAwareness"
                    && packet.Summary.Equals("Track identity", StringComparison.Ordinal)),
            CoachQueryTopic.CarIdentity => packets.Where(packet =>
                packet.Category == "RaceAwareness"
                    && packet.Summary.Equals("Car identity", StringComparison.Ordinal)),
            CoachQueryTopic.RaceAwareness => packets.Where(packet =>
                packet.Category is "RaceAwareness" or "OpponentIntelligence"),
            CoachQueryTopic.Position => packets.Where(packet =>
                packet.Category == "RaceAwareness"
                    && packet.Summary.Equals("Race position", StringComparison.Ordinal)),
            CoachQueryTopic.LapTime => packets.Where(LooksLikeLapTimePacket),
            CoachQueryTopic.FuelAmount => packets.Where(LooksLikeFuelLevelPacket),
            CoachQueryTopic.FuelConsumption => packets.Where(LooksLikeFuelConsumptionPacket),
            CoachQueryTopic.Strategy or CoachQueryTopic.Pit => packets.Where(p =>
                LooksLikeStrategyPacket(p) || LooksLikeFuelPacket(p) || p.Category == "StrategyKnowledge"),
            CoachQueryTopic.Braking => packets.Where(LooksLikeBrakingPacket),
            CoachQueryTopic.Throttle => packets.Where(LooksLikeThrottlePacket),
            CoachQueryTopic.RacePace => packets.Where(LooksLikePacePacket),
            CoachQueryTopic.LosingTime or CoachQueryTopic.LapComparison => packets.Where(LooksLikeLapComparisonPacket),
            CoachQueryTopic.Improvement => packets.Where(packet =>
                LooksLikeImprovementPacket(packet) || packet.Category == "TrackMemory"),
            CoachQueryTopic.Incidents => packets.Where(LooksLikeIncidentPacket),
            _ => packets.Where(packet => !LooksLikeFuelPacket(packet))
        };

        return filtered
            .Where(packet => !IsInvalidLapOrFlagsPacket(packet))
            .OrderByDescending(packet => packet.Confidence)
            .ThenBy(packet => packet.Summary, StringComparer.Ordinal)
            .Take(MaxFacts)
            .ToArray();
    }

    private static bool IsInvalidLapOrFlagsPacket(CoachEvidencePacket packet) =>
        packet.Summary == EventType.InvalidLapOrFlags.ToString();

    private static EngineerAiFact ToFact(CoachEvidencePacket packet) =>
        new(
            packet.Category,
            packet.Summary,
            packet.Explanation,
            packet.Confidence,
            packet.SourceType.ToString(),
            packet.RelatedLapNumber);

    private static bool LooksLikeFuelLevelPacket(CoachEvidencePacket packet)
    {
        var key = $"{packet.Category} {packet.Summary} {packet.Explanation}".ToLowerInvariant();
        return key.Contains("latest fuel") || key.Contains("fuel level");
    }

    private static bool LooksLikeFuelConsumptionPacket(CoachEvidencePacket packet)
    {
        var key = $"{packet.Category} {packet.Summary} {packet.Explanation}".ToLowerInvariant();
        return key.Contains("per lap") || key.Contains("consumption") || key.Contains("potro");
    }

    private static bool LooksLikeFuelStrategyPacket(CoachEvidencePacket packet)
    {
        var key = $"{packet.Category} {packet.Summary} {packet.Explanation}".ToLowerInvariant();
        return key.Contains("laps remaining") || key.Contains("fuel risk") || key.Contains("finish");
    }

    private static bool LooksLikeFuelPacket(CoachEvidencePacket packet)
    {
        var key = $"{packet.Category} {packet.Summary} {packet.Explanation}".ToLowerInvariant();
        return key.Contains("fuel") || key.Contains("goriv");
    }

    private static bool LooksLikeStrategyPacket(CoachEvidencePacket packet)
    {
        var key = $"{packet.Category} {packet.Summary}".ToLowerInvariant();
        return key.Contains("strategy") || key.Contains("pit");
    }

    private static bool LooksLikeTyrePacket(CoachEvidencePacket packet)
    {
        var key = $"{packet.Category} {packet.Summary} {packet.Explanation}".ToLowerInvariant();
        return key.Contains("tyre") || key.Contains("tire") || key.Contains("gum");
    }

    private static bool LooksLikeLapTimePacket(CoachEvidencePacket packet)
    {
        var key = $"{packet.Category} {packet.Summary} {packet.Explanation}".ToLowerInvariant();
        return key.Contains("lap") || key.Contains("time:");
    }

    private static bool LooksLikeBrakingPacket(CoachEvidencePacket packet)
    {
        if (IsInvalidLapOrFlagsPacket(packet))
        {
            return false;
        }

        var key = $"{packet.Category} {packet.Summary} {packet.Explanation}".ToLowerInvariant();
        return key.Contains("brak");
    }

    private static bool LooksLikeThrottlePacket(CoachEvidencePacket packet)
    {
        if (IsInvalidLapOrFlagsPacket(packet))
        {
            return false;
        }

        var key = $"{packet.Category} {packet.Summary} {packet.Explanation}".ToLowerInvariant();
        return key.Contains("throttle") || key.Contains("gas");
    }

    private static bool LooksLikePacePacket(CoachEvidencePacket packet)
    {
        if (IsInvalidLapOrFlagsPacket(packet))
        {
            return false;
        }

        var key = $"{packet.Category} {packet.Summary}".ToLowerInvariant();
        return key.Contains("pace") || key.Contains("tempo");
    }

    private static bool LooksLikeLapComparisonPacket(CoachEvidencePacket packet)
    {
        if (IsInvalidLapOrFlagsPacket(packet))
        {
            return false;
        }

        var key = $"{packet.Category} {packet.Summary}".ToLowerInvariant();
        return key.Contains("sector") || key.Contains("delta") || key.Contains("lap");
    }

    private static bool LooksLikeImprovementPacket(CoachEvidencePacket packet)
    {
        if (IsInvalidLapOrFlagsPacket(packet))
        {
            return false;
        }

        var key = $"{packet.Category} {packet.Summary}".ToLowerInvariant();
        return key.Contains("improvement") || key.Contains("weakness");
    }

    private static bool LooksLikeIncidentPacket(CoachEvidencePacket packet)
    {
        var key = $"{packet.Category} {packet.Summary}".ToLowerInvariant();
        return key.Contains("incident") || key.Contains("event");
    }
}
