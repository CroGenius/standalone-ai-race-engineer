using RaceEngineer.Core.Coaching;
using RaceEngineer.Core.Session;

namespace RaceEngineer.Core.SessionContext;

public sealed record DrivingTechniqueGateResult(
    bool Allowed,
    string? UnavailableMessage,
    string EvidenceReason);

public static class DrivingTechniqueGate
{
    public static bool HasValidDrivingTechniqueSamples(SessionState session) =>
        session.CompletedLaps.Any(lap => lap.IsValid && lap.Duration.HasValue);

    public static bool MustBlockTechniqueCoaching(SessionState session, SessionContextAssessment? context)
    {
        if (HasValidDrivingTechniqueSamples(session))
        {
            return false;
        }

        if (session.CompletedLaps.Count == 0)
        {
            return true;
        }

        if (context is { Activity: VehicleActivity.Stationary or VehicleActivity.PitLane })
        {
            return true;
        }

        return true;
    }

    public static DrivingTechniqueGateResult Evaluate(
        CoachQueryTopic topic,
        SessionState session,
        SessionContextAssessment? context)
    {
        if (!IsDrivingTechniqueTopic(topic))
        {
            return new DrivingTechniqueGateResult(true, null, "");
        }

        if (!MustBlockTechniqueCoaching(session, context))
        {
            return new DrivingTechniqueGateResult(true, null, "");
        }

        var unavailable = BuildUnavailableMessage(topic, session, context)
            ?? "Not enough driving data yet. Drive a clean lap first.";
        return new DrivingTechniqueGateResult(false, unavailable, BuildEvidenceReason(topic, session, context));
    }

    public static string? BuildUnavailableMessage(
        CoachQueryTopic topic,
        SessionState session,
        SessionContextAssessment? context)
    {
        if (!IsDrivingTechniqueTopic(topic) || !MustBlockTechniqueCoaching(session, context))
        {
            return null;
        }

        return topic switch
        {
            CoachQueryTopic.Braking => "No braking data yet. Drive a clean lap first.",
            CoachQueryTopic.Throttle => "No throttle data yet. Drive a clean lap first.",
            CoachQueryTopic.RacePace => "No pace data yet. Drive a clean lap first.",
            CoachQueryTopic.LosingTime => "No sector data yet. Drive a clean lap first.",
            CoachQueryTopic.Improvement => "No improvement data yet. Drive a clean lap first.",
            CoachQueryTopic.LapComparison => "No lap comparison data yet. Drive a clean lap first.",
            _ => null
        };
    }

    public static string BuildEvidenceReason(
        CoachQueryTopic topic,
        SessionState session,
        SessionContextAssessment? context)
    {
        if (context is { Activity: VehicleActivity.Stationary or VehicleActivity.PitLane })
        {
            return "Stationary or pit lane — waiting for a completed on-track lap before technique coaching.";
        }

        if (session.CompletedLaps.Count == 0)
        {
            return "No completed laps recorded yet.";
        }

        return "No valid completed lap with timing data yet.";
    }

    public static bool IsDrivingTechniqueTopic(CoachQueryTopic topic) =>
        topic is CoachQueryTopic.Braking
            or CoachQueryTopic.Throttle
            or CoachQueryTopic.RacePace
            or CoachQueryTopic.LosingTime
            or CoachQueryTopic.Improvement
            or CoachQueryTopic.LapComparison;

    public static bool ContainsBlockedTechniqueLeak(string? content)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return false;
        }

        return content.Contains("InvalidLapOrFlags", StringComparison.OrdinalIgnoreCase)
            || content.Contains("Braking from telemetry:", StringComparison.OrdinalIgnoreCase)
            || content.Contains("Throttle from telemetry:", StringComparison.OrdinalIgnoreCase)
            || content.Contains("Race pace from telemetry:", StringComparison.OrdinalIgnoreCase)
            || content.Contains("Telemetry shows", StringComparison.OrdinalIgnoreCase);
    }
}
