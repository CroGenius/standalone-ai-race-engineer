using RaceEngineer.Core.Coaching;
using RaceEngineer.Core.Session;

namespace RaceEngineer.Core.SessionContext;

public static class DrivingTechniqueGate
{
    public static bool HasValidDrivingTechniqueSamples(SessionState session) =>
        session.CompletedLaps.Any(lap => lap.IsValid && lap.Duration.HasValue);

    public static string? BuildUnavailableMessage(
        CoachQueryTopic topic,
        SessionState session,
        SessionContextAssessment? context)
    {
        if (HasValidDrivingTechniqueSamples(session))
        {
            return null;
        }

        return topic switch
        {
            CoachQueryTopic.Braking => "Not enough braking data yet. Drive a clean lap first.",
            CoachQueryTopic.Throttle => "Not enough throttle data yet. Drive a clean lap first.",
            CoachQueryTopic.RacePace => "No valid pace data yet. Drive a clean lap first.",
            CoachQueryTopic.LosingTime => "No valid sector data yet. Drive a clean lap first.",
            CoachQueryTopic.Improvement => "No valid improvement data yet. Drive a clean lap first.",
            CoachQueryTopic.LapComparison => "No valid lap comparison data yet. Drive a clean lap first.",
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
}
