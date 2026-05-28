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
        var corners = BuildCornerAssessments(temps);
        var rearDataUnavailable = corners.Any(item => (item.Label is "Rear-left" or "Rear-right") && !item.HasData);
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
            corners,
            axles,
            rearDataUnavailable,
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
        var spokenCoachingSummary = BuildSpokenCoachingSummary(
            corners,
            rearDataUnavailable,
            readiness,
            cornersUntilReady,
            pushSafe);
        var pushGuidance = BuildPushGuidance(readiness, cornersUntilReady, avoidHeavyInputs, pushSafe);

        var evidence = BuildEvidenceLines(
            corners,
            axles,
            rearDataUnavailable,
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
            spokenCoachingSummary,
            pushGuidance,
            cornersUntilReady,
            avoidHeavyInputs,
            pushSafe,
            rearDataUnavailable,
            corners,
            axles,
            evidence);
    }

    private IReadOnlyList<TyreCornerAssessment> BuildCornerAssessments(IReadOnlyList<double> temps)
    {
        var definitions = new (string Label, string ShortLabel, int Index)[]
        {
            ("Front-left", "FL", 0),
            ("Front-right", "FR", 1),
            ("Rear-left", "RL", 2),
            ("Rear-right", "RR", 3)
        };

        return definitions
            .Select(definition =>
            {
                if (definition.Index >= temps.Count)
                {
                    return new TyreCornerAssessment(
                        definition.Label,
                        definition.ShortLabel,
                        null,
                        false,
                        TyreWarmupState.Unknown,
                        $"{definition.Label} data unavailable.");
                }

                var temp = temps[definition.Index];
                var state = ClassifyAxleWarmup(temp, temp);
                return new TyreCornerAssessment(
                    definition.Label,
                    definition.ShortLabel,
                    Round(temp),
                    true,
                    state,
                    $"{definition.Label} {FormatReadinessState(state).ToLowerInvariant()} ({Round(temp):0.0} C).");
            })
            .ToArray();
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
        IReadOnlyList<TyreCornerAssessment> corners,
        IReadOnlyList<TyreAxleAssessment> axles,
        bool rearDataUnavailable,
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
        var cornerLines = corners
            .Where(corner => corner.HasData)
            .Select(corner => corner.Summary.TrimEnd('.'))
            .ToArray();
        var messageParts = new List<string>();
        if (cornerLines.Length > 0)
        {
            messageParts.Add(string.Join(". ", cornerLines) + ".");
        }

        if (rearDataUnavailable)
        {
            messageParts.Add("Rear tyre data is unavailable.");
        }

        if (axles.Count > 0)
        {
            var axleSummary = string.Join(
                "; ",
                axles.Select(axle => $"{axle.Label} axle {FormatReadinessState(axle.WarmupState).ToLowerInvariant()}"));
            messageParts.Add($"{axleSummary}.");
        }

        messageParts.Add(BuildActionMessage(
            front,
            rear,
            warmupState,
            readiness,
            gripConfidence,
            cornersUntilReady,
            onOutLap,
            tractionLoss,
            avoidHeavyInputs,
            pushSafe));

        return string.Join(" ", messageParts.Where(part => !string.IsNullOrWhiteSpace(part)));
    }

    private static string BuildActionMessage(
        TyreAxleAssessment? front,
        TyreAxleAssessment? rear,
        TyreWarmupState warmupState,
        TyreReadiness readiness,
        GripConfidenceLevel gripConfidence,
        int? cornersUntilReady,
        bool onOutLap,
        int tractionLoss,
        bool avoidHeavyInputs,
        bool pushSafe)
    {
        if (warmupState == TyreWarmupState.Overheating || readiness == TyreReadiness.Overheated)
        {
            if (rear?.WarmupState == TyreWarmupState.Overheating)
            {
                return "Protect the rears under throttle and ease exits.";
            }

            if (front?.WarmupState == TyreWarmupState.Overheating)
            {
                return "Protect the fronts under braking and entry speed.";
            }

            return "Tyres are overheating. Back off sliding and protect grip.";
        }

        if (readiness == TyreReadiness.Fading)
        {
            return rear?.WarmupState == TyreWarmupState.Fading
                ? "Rears look like they are fading. Protect pace and plan for a stop if grip keeps dropping."
                : "Tyres look like they are fading. Protect pace and plan for a stop if grip keeps dropping.";
        }

        if (readiness is TyreReadiness.NotReady or TyreReadiness.Building || onOutLap || avoidHeavyInputs)
        {
            var waitText = cornersUntilReady is > 0 and <= 3
                ? $"Give them another {cornersUntilReady} corners before pushing."
                : "Give them another half lap before pushing.";
            return warmupState == TyreWarmupState.Warming || readiness == TyreReadiness.Building
                ? $"Build pace gradually and avoid heavy braking or full throttle. {waitText}"
                : waitText;
        }

        if (pushSafe && readiness == TyreReadiness.PushNow)
        {
            return "Tyres are in the window with good grip confidence. Safe to push now.";
        }

        if (tractionLoss > 0)
        {
            return "Tyres are warm but traction is unsettled. Push gradually and wait for cleaner exits.";
        }

        return gripConfidence == GripConfidenceLevel.High
            ? "Tyres are ready with stable grip. Safe to push."
            : "Tyres are usable, but grip confidence is only moderate. Push gradually.";
    }

    private static string BuildSpokenCoachingSummary(
        IReadOnlyList<TyreCornerAssessment> corners,
        bool rearDataUnavailable,
        TyreReadiness readiness,
        int? cornersUntilReady,
        bool pushSafe)
    {
        var available = corners.Where(corner => corner.HasData).ToArray();
        if (available.Length == 0)
        {
            return "Tyre data is not reliable yet.";
        }

        if (available.All(corner => corner.WarmupState == available[0].WarmupState))
        {
            var state = FormatReadinessState(available[0].WarmupState).ToLowerInvariant();
            var countText = available.Length == 4 ? "All four tyres" : "Tyres";
            var rearNote = rearDataUnavailable ? " Rear data unavailable." : "";
            return $"{countText} {state}.{rearNote} {BuildSpokenAction(readiness, cornersUntilReady, pushSafe)}".Trim();
        }

        var cornerText = string.Join(
            ", ",
            available.Select(corner => $"{corner.ShortLabel} {FormatReadinessState(corner.WarmupState).ToLowerInvariant()}"));
        var unavailableNote = rearDataUnavailable ? " Rear data unavailable." : "";
        return $"{cornerText}.{unavailableNote} {BuildSpokenAction(readiness, cornersUntilReady, pushSafe)}".Trim();
    }

    private static string BuildSpokenAction(TyreReadiness readiness, int? cornersUntilReady, bool pushSafe)
    {
        return readiness switch
        {
            TyreReadiness.NotReady or TyreReadiness.Building =>
                cornersUntilReady is > 0 and <= 3
                    ? $"Wait {cornersUntilReady} corners."
                    : "Wait half lap.",
            TyreReadiness.Overheated => "Protect grip.",
            TyreReadiness.Fading => "Protect pace.",
            TyreReadiness.PushNow when pushSafe => "Safe to push.",
            TyreReadiness.ReadyToPush when pushSafe => "Push gradually.",
            _ => "Push gradually."
        };
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
        IReadOnlyList<TyreCornerAssessment> corners,
        IReadOnlyList<TyreAxleAssessment> axles,
        bool rearDataUnavailable,
        TelemetrySnapshot? snapshot,
        int overheatingEvents,
        int tractionLoss,
        int heavyBraking,
        int currentLap,
        bool onOutLap,
        GripConfidenceLevel gripConfidence,
        TyreReadiness readiness)
    {
        var lines = corners.Where(corner => corner.HasData).Select(corner => corner.Summary).ToList();
        lines.AddRange(axles.Select(axle => axle.Summary));
        if (rearDataUnavailable)
        {
            lines.Add("rear tyre data: unavailable");
        }
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

    private static string FormatReadinessState(TyreWarmupState state) =>
        state switch
        {
            TyreWarmupState.Cold => "Cold",
            TyreWarmupState.Warming => "Warming",
            TyreWarmupState.OptimalWindow => "Ready",
            TyreWarmupState.Overheating => "Overheating",
            TyreWarmupState.Fading => "Fading",
            _ => "Unknown"
        };

    private static string FormatWarmupState(TyreWarmupState state) => FormatReadinessState(state);

    private static double Round(double value) => Math.Round(value, 1, MidpointRounding.AwayFromZero);
}
