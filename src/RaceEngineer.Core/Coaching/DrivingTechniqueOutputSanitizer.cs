using System.Reflection;
using System.Text.RegularExpressions;
using RaceEngineer.Core.Profile;
using RaceEngineer.Core.Session;
using RaceEngineer.Core.SessionContext;

namespace RaceEngineer.Core.Coaching;

public static class DrivingTechniqueOutputSanitizer
{
    private static readonly string[] BlockedTokens =
    [
        "InvalidLapOrFlags",
        "UnstableBraking",
        "AbruptBrakeRelease",
        "BrakeOverheating",
        "HeavyBraking",
        " from telemetry:",
        "Telemetry shows",
        "Event/",
        "CoachEvidence",
        "EventType."
    ];

    public static CoachQueryTopic ResolveTopic(string query)
    {
        var topic = CoachQueryTopicClassifier.ClassifyPrimary(query);
        if (topic == CoachQueryTopic.Unknown && query.Contains("brake", StringComparison.OrdinalIgnoreCase))
        {
            return CoachQueryTopic.Braking;
        }

        return topic;
    }

    public static CoachMessage EnforceFinalWritten(
        CoachQueryTopic topic,
        SessionState session,
        SessionContextAssessment? context,
        CoachMessage primaryAnswer,
        CoachMessage deterministicAnswer)
    {
        var gate = DrivingTechniqueGate.Evaluate(topic, session, context);
        if (!gate.Allowed)
        {
            return BuildGateMessage(gate);
        }

        if (MustSanitize(topic, primaryAnswer.Content))
        {
            var leakGate = new DrivingTechniqueGateResult(
                false,
                DrivingTechniqueGate.BuildUnavailableMessage(topic, session, context)
                    ?? "Not enough driving data yet. Drive a clean lap first.",
                "Blocked technique leak in answer.");
            return BuildGateMessage(leakGate);
        }

        return primaryAnswer;
    }

    public static string EnforceSpokenSummary(
        CoachQueryTopic topic,
        DrivingTechniqueGateResult gate,
        string? candidateSummary,
        CoachMessage finalWritten)
    {
        if (!gate.Allowed || MustSanitize(topic, candidateSummary) || MustSanitize(topic, finalWritten.Content))
        {
            return BuildSpokenGateMessage(topic, gate);
        }

        return string.IsNullOrWhiteSpace(candidateSummary)
            ? BuildSpokenGateMessage(topic, gate)
            : candidateSummary;
    }

    public static string EnforceTtsPayload(
        CoachQueryTopic topic,
        DrivingTechniqueGateResult gate,
        string payload,
        string spokenSummary)
    {
        if (!gate.Allowed || MustSanitize(topic, payload) || MustSanitize(topic, spokenSummary))
        {
            return BuildSpokenGateMessage(topic, gate);
        }

        return payload;
    }

    public static bool MustSanitize(CoachQueryTopic topic, string? content)
    {
        if (!DrivingTechniqueGate.IsDrivingTechniqueTopic(topic) || string.IsNullOrWhiteSpace(content))
        {
            return false;
        }

        if (DrivingTechniqueGate.ContainsBlockedTechniqueLeak(content))
        {
            return true;
        }

        foreach (var token in BlockedTokens)
        {
            if (content.Contains(token, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        if (LooksLikeEventIdentifier(content))
        {
            return true;
        }

        return false;
    }

    public static string ExtractActionText(string content)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return "";
        }

        var marker = content.IndexOf("Evidence:", StringComparison.OrdinalIgnoreCase);
        return marker >= 0 ? content[..marker].Trim() : content.Trim();
    }

    private static CoachMessage BuildGateMessage(DrivingTechniqueGateResult gate)
    {
        var message = gate.UnavailableMessage ?? "Not enough driving data yet. Drive a clean lap first.";
        return new CoachMessage(
            "coach",
            $"{message}{Environment.NewLine}{Environment.NewLine}Evidence:{Environment.NewLine}- {gate.EvidenceReason}",
            [],
            gate.EvidenceReason,
            []);
    }

    private static string BuildSpokenGateMessage(CoachQueryTopic topic, DrivingTechniqueGateResult gate) =>
        topic switch
        {
            CoachQueryTopic.Braking => "No braking data yet. Drive a clean lap first.",
            CoachQueryTopic.Throttle => "No throttle data yet. Drive a clean lap first.",
            CoachQueryTopic.RacePace => "No pace data yet. Drive a clean lap first.",
            CoachQueryTopic.LosingTime => "No sector data yet. Drive a clean lap first.",
            CoachQueryTopic.Improvement => "No improvement data yet. Drive a clean lap first.",
            CoachQueryTopic.LapComparison => "No lap comparison data yet. Drive a clean lap first.",
            _ => ExtractActionText(gate.UnavailableMessage ?? "Not enough driving data yet. Drive a clean lap first.")
        };

    private static bool LooksLikeEventIdentifier(string content) =>
        Regex.IsMatch(content, @"\b[A-Z][A-Za-z0-9]+(?:Braking|Release|Overheating|Flags|Incident)\b");
}
