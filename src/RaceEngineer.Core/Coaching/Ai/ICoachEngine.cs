using RaceEngineer.Core.Events;
using RaceEngineer.Core.Profile;
using RaceEngineer.Core.Session;
using RaceEngineer.Core.SessionContext;

namespace RaceEngineer.Core.Coaching.Ai;

public interface ICoachEngine
{
    CoachMessage Answer(
        SessionState session,
        string userMessage,
        CoachContext? context = null,
        CoachEvidenceBundle? evidence = null);

    CoachMessage? ChooseLiveCallout(
        IReadOnlyList<TelemetryEvent> events,
        SessionContextAssessment? context = null);

    string GeneratePostSessionReport(SessionState session, RacePrepPlan? plan = null);
}
