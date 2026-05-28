using System.Globalization;
using RaceEngineer.Core.Events;
using RaceEngineer.Core.Session;
using RaceEngineer.Core.SessionContext;
using RaceEngineer.Core.Telemetry;

namespace RaceEngineer.Core.Analytics;

public sealed class TyreIntelligenceService
{
    private readonly TyreIntelligenceOptions options;

    public TyreIntelligenceService(TyreIntelligenceOptions? options = null)
    {
        this.options = options ?? new TyreIntelligenceOptions();
    }

    public SessionTyreIntelligence Analyze(TyreIntelligenceInput input)
    {
        var snapshot = input.Session.LatestSnapshot;
        var temps = snapshot?.Condition.TyreTempC?.Where(value => value.HasValue).Select(value => value!.Value).ToArray();
        if (temps is null or { Length: 0 })
        {
            return SessionTyreIntelligence.Unavailable("Latest snapshot has no tyre temperature values.");
        }

        var events = input.Events ?? input.Session.RecentEvents;
        var recentSnapshots = (input.RecentSnapshots ?? []).TakeLast(24).ToArray();
        if (recentSnapshots.Length == 0 && snapshot is not null)
        {
            recentSnapshots = [snapshot];
        }

        var axles = BuildAxleAssessments(temps);
        var front = axles.FirstOrDefault(item => item.Label == "Front");
        var rear = axles.FirstOrDefault(item => item.Label == "Rear");
        var avgTemp = temps.Average();
        var maxTemp = temps.Max();
        var overheatingEvents = events.Where(item => item.Type == EventType.TyreOverheating).TakeLast(5).ToArray();
        var tractionLoss = events.Count(item => item.Type == EventType.TractionLoss);
        var heavyBraking = events.Count(item => item.Type is EventType.HeavyBraking or EventType.UnstableBraking);
        var throttleHesitation = events.Count(item => item.Type == EventType.ThrottleHesitation);
        var paceStable = IsPaceStable(input);
        var onOutLap = input.Activity == VehicleActivity.OutLap;
        var inRaceStint = input.Phase == SessionPhase.Race && input.Activity == VehicleActivity.OnTrack;

        var warmupState = InferWarmupState(avgTemp, maxTemp, overheatingEvents.Length, input, paceStable);
        var gripConfidence = ScoreGripConfidence(
            warmupState,
            tractionLoss,
            heavyBraking,
            throttleHesitation,
            paceStable,
            onOutLap);
        var readiness = InferReadiness(warmupState, gripConfidence, onOutLap, inRaceStint, overheatingEvents.Length > 0);
        var overheatingRisk = ClassifyOverheatingRisk(maxTemp, overheatingEvents.Length, warmupState);
        var cornersUntilReady = EstimateCornersUntilReady(front?.AverageTempC ?? avgTemp, onOutLap || warmupState is TyreWarmupState.Cold or TyreWarmupState.Warming);
        var avoidHeavyInputs = readiness is TyreReadiness.NotReady or TyreReadiness.Building or TyreReadiness.Overheated;
        var pushSafe = readiness is TyreReadiness.ReadyToPush or TyreReadiness.PushNow;
        var coachingMessage = BuildCoachingMessage(
            front,
            rear,
            warmupState,
            readiness,
            gripConfidence,
            overheatingRisk,
            cornersUntilReady,
            onOutLap,
            tractionLoss,
            avoidHeavyInputs,
            pushSafe);
        var pushGuidance = BuildPushGuidance(readiness, cornersUntilReady, avoidHeavyInputs, pushSafe);

        var evidence = BuildEvidenceLines(
            axles,
            snapshot,
            overheatingEvents.Length,
            tractionLoss,
            heavyBraking,
            input.Session.CurrentLap,
            onOutLap,
            gripConfidence,
            readiness);

        return new SessionTyreIntelligence(
            true,
            "Available",
            warmupState,
            readiness,
            gripConfidence,
            gripConfidence.ToString(),
            overheatingRisk,
            coachingMessage,
            pushGuidance,
            cornersUntilReady,
            avoidHeavyInputs,
            pushSafe,
            axles,
            evidence);
    }

    private IReadOnlyList<TyreAxleAssessment> BuildAxleAssessments(IReadOnlyList<double> temps)
    {
        if (temps.Count >= 4)
        {
            return
            [
                BuildAxle("Front", temps.Take(2)),
                BuildAxle("Rear", temps.Skip(2).Take(2))
            ];
        }

        return [BuildAxle("All", temps)];
    }

    private TyreAxleAssessment BuildAxle(string label, IEnumerable<double> values)
    {
        var array = values.ToArray();
        var avg = array.Average();
        var min = array.Min();
        var max = array.Max();
        var state = ClassifyAxleWarmup(avg, max);
        return new TyreAxleAssessment(
            label,
            Round(avg),
            Round(min),
            Round(max),
            state,
            $"{label} tyres {FormatWarmupState(state).ToLowerInvariant()} ({Round(avg):0.0} C avg).");
    }

    private TyreWarmupState InferWarmupState(
        double avgTemp,
        double maxTemp,
        int overheatingEvents,
        TyreIntelligenceInput input,
        bool paceStable)
    {
        if (maxTemp >= options.OverheatTempThresholdC || overheatingEvents > 0)
        {
            return TyreWarmupState.Overheating;
        }

        if (input.Phase == SessionPhase.Race
            && input.Activity == VehicleActivity.OnTrack
            && !paceStable
            && input.Session.CompletedLaps.Count >= 3
            && input.LapIntelligence?.PaceDecay.TrendLabel.Contains("slower", StringComparison.OrdinalIgnoreCase) == true)
        {
            return TyreWarmupState.Fading;
        }

        if (avgTemp < options.ColdTempThresholdC)
        {
            return TyreWarmupState.Cold;
        }

        if (avgTemp < options.OptimalTempThresholdC)
        {
            return TyreWarmupState.Warming;
        }

        return TyreWarmupState.OptimalWindow;
    }

    private static GripConfidenceLevel ScoreGripConfidence(
        TyreWarmupState warmupState,
        int tractionLoss,
        int heavyBraking,
        int throttleHesitation,
        bool paceStable,
        bool onOutLap)
    {
        if (warmupState is TyreWarmupState.Cold or TyreWarmupState.Overheating or TyreWarmupState.Fading)
        {
            return GripConfidenceLevel.Low;
        }

        if (onOutLap || warmupState == TyreWarmupState.Warming || tractionLoss >= 2)
        {
            return GripConfidenceLevel.Medium;
        }

        if (heavyBraking >= 2 || throttleHesitation >= 2 || !paceStable)
        {
            return GripConfidenceLevel.Medium;
        }

        return warmupState == TyreWarmupState.OptimalWindow
            ? GripConfidenceLevel.High
            : GripConfidenceLevel.Medium;
    }

    private static TyreReadiness InferReadiness(
        TyreWarmupState warmupState,
        GripConfidenceLevel gripConfidence,
        bool onOutLap,
        bool inRaceStint,
        bool overheating)
    {
        if (overheating || warmupState == TyreWarmupState.Overheating)
        {
            return TyreReadiness.Overheated;
        }

        if (warmupState == TyreWarmupState.Fading)
        {
            return TyreReadiness.Fading;
        }

        if (warmupState == TyreWarmupState.Cold)
        {
            return TyreReadiness.NotReady;
        }

        if (warmupState == TyreWarmupState.Warming || onOutLap)
        {
            return gripConfidence == GripConfidenceLevel.High
                ? TyreReadiness.ReadyToPush
                : TyreReadiness.Building;
        }

        if (warmupState == TyreWarmupState.OptimalWindow && gripConfidence == GripConfidenceLevel.High && !onOutLap)
        {
            return inRaceStint ? TyreReadiness.PushNow : TyreReadiness.ReadyToPush;
        }

        return TyreReadiness.ReadyToPush;
    }

    private static string ClassifyOverheatingRisk(double maxTemp, int overheatingEvents, TyreWarmupState warmupState)
    {
        if (warmupState == TyreWarmupState.Overheating || overheatingEvents > 0 || maxTemp >= 105)
        {
            return "High";
        }

        if (maxTemp >= 98)
        {
            return "Medium";
        }

        return "Low";
    }

    private static int? EstimateCornersUntilReady(double avgTemp, bool needsWarmup)
    {
        if (!needsWarmup)
        {
            return 0;
        }

        var deficit = Math.Max(0, 85.0 - avgTemp);
        if (deficit <= 0)
        {
            return 0;
        }

        return Math.Clamp((int)Math.Ceiling(deficit / 8.0), 1, 6);
    }

    private static bool IsPaceStable(TyreIntelligenceInput input)
    {
        var consistency = input.Analytics?.LapConsistency.Score0To100;
        if (consistency is >= 70)
        {
            return true;
        }

        var trend = input.LapIntelligence?.PaceDecay.TrendLabel;
        return trend is null
            || trend.Contains("stable", StringComparison.OrdinalIgnoreCase)
            || trend.Contains("improving", StringComparison.OrdinalIgnoreCase);
    }

    private static string BuildCoachingMessage(
        TyreAxleAssessment? front,
        TyreAxleAssessment? rear,
        TyreWarmupState warmupState,
        TyreReadiness readiness,
        GripConfidenceLevel gripConfidence,
        string overheatingRisk,
        int? cornersUntilReady,
        bool onOutLap,
        int tractionLoss,
        bool avoidHeavyInputs,
        bool pushSafe)
    {
        if (warmupState == TyreWarmupState.Overheating || readiness == TyreReadiness.Overheated)
        {
            return rear?.WarmupState == TyreWarmupState.Overheating
                ? "Rear tyres are overheating under throttle. Ease exits and protect grip."
                : "Tyres are overheating. Back off sliding and protect entry speed.";
        }

        if (readiness == TyreReadiness.Fading)
        {
            return "Tyres look like they are fading. Protect pace and plan for a stop if grip keeps dropping.";
        }

        if (readiness == TyreReadiness.NotReady || front?.WarmupState == TyreWarmupState.Cold)
        {
            var waitText = cornersUntilReady is > 0 and <= 3
                ? $"Give them another {cornersUntilReady} corners before pushing."
                : "Give them another half lap before pushing.";
            return front is not null
                ? $"Front tyres are still cold. {waitText}"
                : $"Tyres are still cold. {waitText}";
        }

        if (readiness == TyreReadiness.Building || onOutLap)
        {
            return warmupState == TyreWarmupState.Warming
                ? "Tyres are warming. Build pace smoothly and avoid heavy braking or full throttle."
                : "Tyres are near operating window. You can start increasing pace.";
        }

        if (pushSafe && readiness == TyreReadiness.PushNow)
        {
            return "Tyres are in the window with good grip confidence. You can push now.";
        }

        if (tractionLoss > 0)
        {
            return "Tyres are warm but traction is unsettled. Wait for cleaner exits before a full attack.";
        }

        return gripConfidence == GripConfidenceLevel.High
            ? "Tyres are ready with stable grip. You can push."
            : "Tyres are usable, but grip confidence is only moderate. Build pace gradually.";
    }

    private static string BuildPushGuidance(
        TyreReadiness readiness,
        int? cornersUntilReady,
        bool avoidHeavyInputs,
        bool pushSafe)
    {
        if (avoidHeavyInputs)
        {
            return cornersUntilReady is > 0
                ? $"Avoid heavy braking and full throttle for about {cornersUntilReady} more corners."
                : "Avoid heavy braking and full throttle until tyre temperatures stabilize.";
        }

        return pushSafe
            ? "Grip confidence is safe for full attack."
            : "Increase pace gradually and watch for overheating or traction loss.";
    }

    private static IReadOnlyList<string> BuildEvidenceLines(
        IReadOnlyList<TyreAxleAssessment> axles,
        TelemetrySnapshot? snapshot,
        int overheatingEvents,
        int tractionLoss,
        int heavyBraking,
        int currentLap,
        bool onOutLap,
        GripConfidenceLevel gripConfidence,
        TyreReadiness readiness)
    {
        var lines = axles.Select(axle => axle.Summary).ToList();
        lines.Add($"grip confidence: {gripConfidence}");
        lines.Add($"tyre readiness: {readiness}");
        lines.Add($"current lap: {currentLap}");
        lines.Add(onOutLap ? "session state: out lap" : "session state: on track");
        lines.Add($"traction loss events: {tractionLoss}");
        lines.Add($"heavy braking events: {heavyBraking}");
        lines.Add($"tyre overheating events: {overheatingEvents}");

        var pressures = snapshot?.Condition.TyrePressure?.Where(value => value.HasValue).Select(value => value!.Value).ToArray();
        if (pressures is { Length: > 0 })
        {
            lines.Add($"pressure range: {pressures.Min():0.0}-{pressures.Max():0.0}");
        }

        return lines;
    }

    private TyreWarmupState ClassifyAxleWarmup(double avgTemp, double maxTemp)
    {
        if (maxTemp >= options.OverheatTempThresholdC)
        {
            return TyreWarmupState.Overheating;
        }

        if (avgTemp < options.ColdTempThresholdC)
        {
            return TyreWarmupState.Cold;
        }

        if (avgTemp < options.OptimalTempThresholdC)
        {
            return TyreWarmupState.Warming;
        }

        return TyreWarmupState.OptimalWindow;
    }

    private static string FormatWarmupState(TyreWarmupState state) =>
        state switch
        {
            TyreWarmupState.Cold => "Cold",
            TyreWarmupState.Warming => "Warming",
            TyreWarmupState.OptimalWindow => "In window",
            TyreWarmupState.Overheating => "Overheating",
            TyreWarmupState.Fading => "Fading",
            _ => "Unknown"
        };

    private static double Round(double value) => Math.Round(value, 1, MidpointRounding.AwayFromZero);
}
