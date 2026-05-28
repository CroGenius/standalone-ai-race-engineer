using RaceEngineer.Core.Coaching;
using RaceEngineer.Core.Events;
using RaceEngineer.Core.Session;
using RaceEngineer.Core.SessionContext;

using RaceEngineer.Core.Strategy;

namespace RaceEngineer.Core.Analytics;

public sealed record PushConfidenceAssessment(
    bool HasReliableData,
    string CoachingMessage,
    string SpokenSummary,
    GripConfidenceLevel GripConfidence,
    bool CanPushHarder,
    IReadOnlyList<string> EvidenceLines);

public sealed record PushConfidenceInput(
    SessionState Session,
    SessionTyreIntelligence? TyreIntelligence = null,
    SessionTelemetryAnalytics? Analytics = null,
    IReadOnlyList<TelemetryEvent>? Events = null,
    SessionContextAssessment? Context = null,
    SessionStrategy? Strategy = null);

public static class PushConfidenceAnalyzer
{
    public static PushConfidenceAssessment Analyze(PushConfidenceInput input)
    {
        var events = input.Events ?? input.Session.RecentEvents;
        var intelligence = input.TyreIntelligence
            ?? new TyreIntelligenceService().Analyze(new TyreIntelligenceInput(
                input.Session,
                null,
                events,
                input.Context?.Activity ?? VehicleActivity.Unknown,
                input.Context?.Phase ?? SessionPhase.Unknown,
                input.Analytics,
                null));

        if (!intelligence.HasReliableData)
        {
            return new PushConfidenceAssessment(
                false,
                "Grip confidence is unknown until tyre telemetry is available.",
                "Wait for tyre telemetry before pushing.",
                GripConfidenceLevel.Low,
                false,
                [intelligence.Availability]);
        }

        var tractionLoss = events.Count(item => item.Type == EventType.TractionLoss);
        var slides = events.Count(item => item.Type is EventType.TractionLoss or EventType.InvalidLapOrFlags);
        var heavyBraking = events.Count(item => item.Type is EventType.HeavyBraking or EventType.UnstableBraking);
        var throttleHesitation = events.Count(item => item.Type == EventType.ThrottleHesitation);
        var brakeScore = input.Analytics?.BrakeStability.Score0To100;
        var throttleScore = input.Analytics?.ThrottleSmoothness.Score0To100;
        var paceTrend = input.Analytics?.PaceTrend.TrendLabel;
        var paceStable = paceTrend is null
            || paceTrend.Contains("stable", StringComparison.OrdinalIgnoreCase)
            || paceTrend.Contains("improving", StringComparison.OrdinalIgnoreCase);
        var incidentsPerLap = input.Analytics?.Incidents.IncidentsPerLap ?? 0;
        var onOutLap = input.Context?.Activity == VehicleActivity.OutLap;
        var inRaceStint = input.Context?.Phase == SessionPhase.Race && input.Context.Activity == VehicleActivity.OnTrack;

        var factors = new List<string>
        {
            $"grip confidence: {intelligence.GripConfidence}",
            $"tyre readiness: {intelligence.Readiness}",
            $"overheating risk: {intelligence.OverheatingRisk}",
            $"pace stable: {paceStable}",
            $"traction loss events: {tractionLoss}",
            $"recent slides/incidents: {slides}",
            $"brake stability score: {FormatScore(brakeScore)}",
            $"throttle smoothness score: {FormatScore(throttleScore)}",
            $"throttle hesitation events: {throttleHesitation}",
            $"session phase: {DescribePhase(input.Context, onOutLap, inRaceStint)}"
        };

        var rearUnstable = intelligence.RearDataUnavailable == false
            && intelligence.Axles.FirstOrDefault(item => item.Label == "Rear") is { WarmupState: TyreWarmupState.Overheating or TyreWarmupState.Fading }
            || (tractionLoss >= 1 && throttleHesitation >= 1);

        if (intelligence.Readiness is TyreReadiness.Overheated || intelligence.OverheatingRisk == "High")
        {
            return Build(
                input.Session.SessionId,
                intelligence.GripConfidence,
                false,
                [
                    "Tyres are overheating. Ease pace and protect grip before pushing harder.",
                    "Heat is building in the tyres. Back off sliding and protect grip for now.",
                    "Overheating risk is high. Protect the tyres before you push harder."
                ],
                [
                    "Tyres overheating. Ease pace.",
                    "Heat is high. Protect grip.",
                    "Overheat risk. Back off."
                ],
                factors);
        }

        if (rearUnstable || (throttleScore is < 55 && tractionLoss > 0))
        {
            return Build(
                input.Session.SessionId,
                GripConfidenceLevel.Medium,
                false,
                [
                    "Rear grip is inconsistent under throttle. Avoid full exits for now.",
                    "Rear traction is unsettled on exit. Build throttle progressively.",
                    "Exit grip is inconsistent. Wait for cleaner throttle before full attack."
                ],
                [
                    "Rear grip inconsistent. Avoid full exits.",
                    "Exit traction unsettled. Build throttle.",
                    "Rear unstable on exit. Wait."
                ],
                factors);
        }

        if (intelligence.GripConfidence == GripConfidenceLevel.Low
            || intelligence.Readiness is TyreReadiness.NotReady or TyreReadiness.Building
            || onOutLap
            || !paceStable
            || heavyBraking >= 2
            || incidentsPerLap >= 1.5)
        {
            var lapHint = input.Session.CompletedLaps.Count switch
            {
                0 => "another lap",
                1 => "another lap",
                _ => "another half lap"
            };

            return Build(
                input.Session.SessionId,
                GripConfidenceLevel.Low,
                false,
                [
                    $"Grip confidence is still unstable. Push progressively for {lapHint}.",
                    "The car is not fully settled yet. Build pace gradually for another lap.",
                    "Confidence is only partial. Increase pace in steps, not one big push."
                ],
                [
                    "Grip still unstable. Push progressively.",
                    "Car not settled. Build pace gradually.",
                    "Partial confidence. Increase pace in steps."
                ],
                factors);
        }

        if (intelligence.Readiness is TyreReadiness.PushNow or TyreReadiness.ReadyToPush
            && intelligence.GripConfidence == GripConfidenceLevel.High
            && paceStable
            && (brakeScore is null or >= 70)
            && (throttleScore is null or >= 65)
            && slides == 0)
        {
            return Build(
                input.Session.SessionId,
                GripConfidenceLevel.High,
                true,
                [
                    "Tyres are in window and the car is stable. You can push harder now.",
                    "Grip confidence is strong and inputs look stable. Safe to push harder.",
                    "Tyres and car balance look settled. You can increase attack now."
                ],
                [
                    "Tyres in window. Push harder now.",
                    "Grip strong. Safe to push.",
                    "Car stable. Increase attack."
                ],
                factors);
        }

        return Build(
            input.Session.SessionId,
            intelligence.GripConfidence,
            intelligence.PushSafe,
            [
                "Grip confidence is moderate. Push gradually and watch for overheating or slides.",
                "You can push a little more, but build attack gradually until the car stays stable.",
                "Tyres are usable, but confidence is not full yet. Increase pace in steps."
            ],
            [
                "Grip moderate. Push gradually.",
                "Build attack gradually.",
                "Usable grip. Increase pace in steps."
            ],
            factors);
    }

    private static PushConfidenceAssessment Build(
        Guid sessionId,
        GripConfidenceLevel grip,
        bool canPushHarder,
        IReadOnlyList<string> writtenOptions,
        IReadOnlyList<string> spokenOptions,
        IReadOnlyList<string> evidence)
    {
        var signature = $"{grip}|{canPushHarder}|{writtenOptions[0][..Math.Min(24, writtenOptions[0].Length)]}";
        var message = CoachAnswerVariation.Choose(sessionId, CoachQueryTopic.PushConfidence, signature, writtenOptions);
        var spoken = CoachAnswerVariation.Choose(sessionId, CoachQueryTopic.PushConfidence, $"{signature}|spoken", spokenOptions);
        return new PushConfidenceAssessment(true, message, spoken, grip, canPushHarder, evidence);
    }

    private static string FormatScore(double? score) =>
        score?.ToString("0") ?? "n/a";

    private static string DescribePhase(SessionContextAssessment? context, bool onOutLap, bool inRaceStint)
    {
        if (onOutLap)
        {
            return "out lap";
        }

        if (inRaceStint)
        {
            return "race stint";
        }

        return context?.SessionModeLabel ?? "on track";
    }
}
