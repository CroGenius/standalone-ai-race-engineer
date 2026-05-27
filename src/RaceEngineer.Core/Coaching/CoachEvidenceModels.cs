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

        return packets
            .Where(packet => categories.Contains(packet.Category, StringComparer.Ordinal))
            .OrderBy(packet => packet.Category, StringComparer.Ordinal)
            .ThenBy(packet => packet.Summary, StringComparer.Ordinal)
            .Take(6)
            .ToArray();
    }
}
