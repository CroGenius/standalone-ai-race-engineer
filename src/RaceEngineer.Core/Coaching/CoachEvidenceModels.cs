using RaceEngineer.Core.Events;

namespace RaceEngineer.Core.Coaching;

public enum CoachEvidenceTopic
{
    LosingTime,
    Braking,
    Throttle,
    Improvement,
    LapComparison,
    RacePace,
    Fuel,
    Strategy,
    Tyres,
    Incidents,
    SetupNotes
}

public enum CoachEvidenceSourceType
{
    Telemetry,
    Event,
    Analytics,
    LapIntelligence,
    Trace,
    Knowledge,
    Session
}

public sealed record CoachEvidencePacket(
    string Category,
    string Summary,
    string Severity,
    double Confidence,
    CoachEvidenceSourceType SourceType,
    int? RelatedLapNumber,
    IReadOnlyList<Guid> RelatedEventIds,
    double? MetricValue,
    string Explanation);

public sealed record CoachEvidenceBundle(IReadOnlyList<CoachEvidencePacket> Packets)
{
    public static CoachEvidenceBundle Empty { get; } = new([]);

    public IReadOnlyList<CoachEvidencePacket> Select(CoachEvidenceTopic topic)
    {
        return CoachEvidenceSelector.Select(Packets, topic);
    }
}

public sealed record CoachEvidenceInput(
    Session.SessionState Session,
    IReadOnlyList<Telemetry.TelemetrySnapshot>? Snapshots = null,
    IReadOnlyList<Events.TelemetryEvent>? Events = null,
    Analytics.SessionTelemetryAnalytics? Analytics = null,
    Analytics.SessionLapIntelligence? LapIntelligence = null,
    Analytics.SessionTyreIntelligence? TyreIntelligence = null,
    Strategy.SessionStrategy? Strategy = null,
    TelemetryVisualization.TelemetryTimeline? Timeline = null,
    IReadOnlyList<Knowledge.KnowledgeSource>? KnowledgeSources = null);

public static class CoachEvidenceSelector
{
    private static readonly IReadOnlyDictionary<CoachEvidenceTopic, string[]> TopicCategories = new Dictionary<CoachEvidenceTopic, string[]>
    {
        [CoachEvidenceTopic.LosingTime] = ["Sector", "LapComparison", "DeltaTrace", "LosingTime"],
        [CoachEvidenceTopic.Braking] = ["Braking", "Event"],
        [CoachEvidenceTopic.Throttle] = ["Throttle", "Event"],
        [CoachEvidenceTopic.Improvement] = ["Improvement", "Analytics", "LapIntelligence"],
        [CoachEvidenceTopic.LapComparison] = ["LapComparison", "DeltaTrace", "Sector"],
        [CoachEvidenceTopic.RacePace] = ["Pace", "Analytics", "LapIntelligence"],
        [CoachEvidenceTopic.Fuel] = ["Fuel", "Analytics", "Session"],
        [CoachEvidenceTopic.Strategy] = ["Strategy"],
        [CoachEvidenceTopic.Tyres] = ["Tyre", "TyreIntelligence"],
        [CoachEvidenceTopic.Incidents] = ["Incident", "Event"],
        [CoachEvidenceTopic.SetupNotes] = ["Knowledge"]
    };

    public static IReadOnlyList<CoachEvidencePacket> Select(
        IReadOnlyList<CoachEvidencePacket> packets,
        CoachEvidenceTopic topic)
    {
        if (!TopicCategories.TryGetValue(topic, out var categories))
        {
            return [];
        }

        var filtered = packets
            .Where(packet => categories.Contains(packet.Category, StringComparer.Ordinal))
            .ToArray();

        if (topic == CoachEvidenceTopic.Strategy)
        {
            string[] priority =
            [
                "Pit recommendation",
                "Strategy summary",
                "Fuel risk",
                "Laps remaining",
                "Tyre risk",
                "Minimum fuel to finish",
                "Fuel used per lap",
                "Estimated finish fuel",
                "Pit window"
            ];

            return filtered
                .OrderBy(packet =>
                {
                    var index = Array.IndexOf(priority, packet.Summary);
                    return index >= 0 ? index : priority.Length;
                })
                .ThenBy(packet => packet.Summary, StringComparer.Ordinal)
                .Take(8)
                .ToArray();
        }

        return filtered
            .Where(packet => IsAllowedTechniquePacket(packet, topic))
            .OrderBy(packet => packet.Category, StringComparer.Ordinal)
            .ThenBy(packet => packet.Summary, StringComparer.Ordinal)
            .Take(6)
            .ToArray();
    }

    private static bool IsAllowedTechniquePacket(CoachEvidencePacket packet, CoachEvidenceTopic topic)
    {
        if (topic is not (CoachEvidenceTopic.Braking
            or CoachEvidenceTopic.Throttle
            or CoachEvidenceTopic.RacePace
            or CoachEvidenceTopic.LosingTime
            or CoachEvidenceTopic.Improvement
            or CoachEvidenceTopic.LapComparison))
        {
            return true;
        }

        if (!string.Equals(packet.Category, "Event", StringComparison.Ordinal))
        {
            return true;
        }

        return packet.Summary != EventType.InvalidLapOrFlags.ToString();
    }
}
