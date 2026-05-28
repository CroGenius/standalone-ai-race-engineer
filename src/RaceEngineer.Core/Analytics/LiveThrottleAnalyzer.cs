using System.Globalization;
using RaceEngineer.Core.Coaching;
using RaceEngineer.Core.Events;
using RaceEngineer.Core.Session;
using RaceEngineer.Core.SessionContext;
using RaceEngineer.Core.Telemetry;

namespace RaceEngineer.Core.Analytics;

public sealed record LiveThrottleAssessment(
    bool HasData,
    string CoachingMessage,
    string SpokenSummary,
    IReadOnlyList<string> EvidenceLines);

public sealed record LiveThrottleInput(
    SessionState Session,
    IReadOnlyList<TelemetryEvent>? Events = null,
    IReadOnlyList<TelemetrySnapshot>? Snapshots = null,
    SessionTelemetryAnalytics? Analytics = null,
    CoachEvidenceBundle? Evidence = null,
    SessionContextAssessment? Context = null);

public static class LiveThrottleAnalyzer
{
    public static LiveThrottleAssessment Analyze(LiveThrottleInput input)
    {
        var events = (input.Events ?? input.Session.RecentEvents).TakeLast(20).ToArray();
        var snapshots = (input.Snapshots ?? []).TakeLast(40).ToArray();
        var evidenceLines = new List<string>();

        var hesitationEvents = events.Where(item => item.Type == EventType.ThrottleHesitation).ToArray();
        var earlyThrottleEvents = events.Where(item => item.Type == EventType.EarlyThrottleWithSteering).ToArray();
        var tractionLoss = events.Count(item => item.Type == EventType.TractionLoss);

        if (hesitationEvents.Length > 0)
        {
            evidenceLines.Add($"throttle hesitation events: {hesitationEvents.Length}");
        }

        if (earlyThrottleEvents.Length > 0)
        {
            evidenceLines.Add($"early throttle with steering events: {earlyThrottleEvents.Length}");
        }

        if (tractionLoss > 0)
        {
            evidenceLines.Add($"traction loss events: {tractionLoss}");
        }

        var oscillation = AnalyzeOscillation(snapshots, evidenceLines);
        var pickup = AnalyzePickupTiming(snapshots, evidenceLines);
        var analyticsScore = input.Analytics?.ThrottleSmoothness.Score0To100
            ?? input.Evidence?.Select(CoachEvidenceTopic.Throttle).FirstOrDefault(packet => packet.Summary == "Throttle smoothness score")?.MetricValue;

        if (analyticsScore is { } score)
        {
            evidenceLines.Add($"throttle smoothness score: {score.ToString("0", CultureInfo.InvariantCulture)}/100");
        }

        if (evidenceLines.Count == 0 && snapshots.Length < 3 && input.Session.LatestSnapshot?.Inputs.Throttle is null)
        {
            return new LiveThrottleAssessment(
                false,
                "No throttle trace data is available yet.",
                "No throttle data yet.",
                ["No live throttle samples or events recorded."]);
        }

        if (hesitationEvents.Length >= 2)
        {
            return Build(
                input.Session.SessionId,
                "Throttle hesitation is showing in recent corners. Commit earlier once the car is pointed.",
                "Throttle hesitation in recent corners. Commit earlier.",
                evidenceLines);
        }

        if (oscillation >= 0.22)
        {
            return Build(
                input.Session.SessionId,
                "Throttle trace is oscillating on exit. Smooth the pickup and avoid stabbing the pedal.",
                "Throttle oscillating on exit. Smooth the pickup.",
                evidenceLines);
        }

        if (earlyThrottleEvents.Length >= 1)
        {
            return Build(
                input.Session.SessionId,
                "You are picking up throttle early with steering still loaded. Wait for the car to settle before full throttle.",
                "Early throttle with steering loaded. Wait for settle.",
                evidenceLines);
        }

        if (pickup?.Delayed == true)
        {
            return Build(
                input.Session.SessionId,
                "Throttle pickup is delayed after apex. You can gain time by committing earlier on exit.",
                "Throttle pickup delayed. Commit earlier on exit.",
                evidenceLines);
        }

        if (analyticsScore is >= 75 || (oscillation <= 0.12 && hesitationEvents.Length == 0))
        {
            return Build(
                input.Session.SessionId,
                "Throttle application looks smooth over recent corners with stable pickup timing.",
                "Throttle looks smooth over recent corners.",
                evidenceLines);
        }

        if (tractionLoss > 0)
        {
            return Build(
                input.Session.SessionId,
                "Throttle is reasonable, but recent traction loss suggests you are asking too much on exit.",
                "Traction loss on exit. Ask for less throttle.",
                evidenceLines);
        }

        return Build(
            input.Session.SessionId,
            "Throttle traces are usable but not fully settled yet. Focus on smoother exit application.",
            "Throttle usable but not fully settled.",
            evidenceLines);
    }

    private static LiveThrottleAssessment Build(
        Guid sessionId,
        string message,
        string spoken,
        IReadOnlyList<string> evidence)
    {
        var signature = message[..Math.Min(32, message.Length)];
        return new LiveThrottleAssessment(
            true,
            CoachAnswerVariation.Choose(sessionId, CoachQueryTopic.Throttle, signature, [message]),
            CoachAnswerVariation.Choose(sessionId, CoachQueryTopic.Throttle, $"{signature}|spoken", [spoken]),
            evidence);
    }

    private static double AnalyzeOscillation(IReadOnlyList<TelemetrySnapshot> snapshots, List<string> evidenceLines)
    {
        if (snapshots.Count < 4)
        {
            return 0;
        }

        var jerkyChanges = 0;
        var samples = 0;
        for (var index = 1; index < snapshots.Count; index++)
        {
            var previous = snapshots[index - 1].Inputs.Throttle;
            var current = snapshots[index].Inputs.Throttle;
            if (previous is { } prev && current is { } cur && cur >= 0.12)
            {
                samples++;
                if (Math.Abs(cur - prev) >= 0.30)
                {
                    jerkyChanges++;
                }
            }
        }

        if (samples == 0)
        {
            return 0;
        }

        var rate = jerkyChanges / (double)samples;
        evidenceLines.Add($"throttle oscillation rate: {rate.ToString("0.00", CultureInfo.InvariantCulture)}");
        return rate;
    }

    private sealed record PickupTimingAssessment(bool Delayed);

    private static PickupTimingAssessment? AnalyzePickupTiming(IReadOnlyList<TelemetrySnapshot> snapshots, List<string> evidenceLines)
    {
        if (snapshots.Count < 6)
        {
            return null;
        }

        var lowSteeringHighProgress = snapshots.Count(item =>
            item.Inputs.Steering is { } steering
            && Math.Abs(steering) <= 0.12
            && item.Lap.LapProgress is >= 0.35 and <= 0.75
            && item.Inputs.Throttle is < 0.35) >= 3;

        if (lowSteeringHighProgress)
        {
            evidenceLines.Add("delayed throttle pickup detected on recent trace samples");
            return new PickupTimingAssessment(true);
        }

        return new PickupTimingAssessment(false);
    }
}
