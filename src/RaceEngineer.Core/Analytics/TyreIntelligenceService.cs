using RaceEngineer.Core.Coaching;
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
        var tempTrend = InferTempTrend(recentSnapshots);
        var completedLaps = input.Session.CompletedLaps.Count;
        var coachingMessage = BuildCoachingMessage(
            input.Session.SessionId,
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
            inRaceStint,
            tractionLoss,
            avoidHeavyInputs,
            pushSafe,
            tempTrend,
            completedLaps);
        var spokenCoachingSummary = BuildSpokenCoachingSummary(
            input.Session.SessionId,
            corners,
            rearDataUnavailable,
            readiness,
            cornersUntilReady,
            pushSafe,
            tempTrend,
            completedLaps);
        var tyreConditionMessage = BuildTyreConditionMessage(
            input.Session.SessionId,
            corners,
            axles,
            rearDataUnavailable,
            front,
            rear,
            warmupState,
            tempTrend,
            onOutLap,
            inRaceStint,
            completedLaps,
            overheatingRisk);
        var tyreConditionSpokenSummary = BuildTyreConditionSpokenSummary(
            input.Session.SessionId,
            corners,
            axles,
            rearDataUnavailable,
            warmupState,
            tempTrend,
            completedLaps);
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
            tyreConditionMessage,
            tyreConditionSpokenSummary,
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

    private static string InferTempTrend(IReadOnlyList<TelemetrySnapshot> snapshots)
    {
        if (snapshots.Count < 4)
        {
            return "stable";
        }

        var averages = snapshots
            .Select(snapshot => snapshot.Condition.TyreTempC?.Where(value => value.HasValue).Select(value => value!.Value).DefaultIfEmpty().Average() ?? 0)
            .Where(value => value > 0)
            .ToArray();
        if (averages.Length < 4)
        {
            return "stable";
        }

        var first = averages.Take(averages.Length / 2).Average();
        var second = averages.Skip(averages.Length / 2).Average();
        var delta = second - first;
        if (delta >= 2.5)
        {
            return "rising";
        }

        if (delta <= -2.5)
        {
            return "falling";
        }

        return "stable";
    }

    private static string BuildCoachingMessage(
        Guid sessionId,
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
        bool inRaceStint,
        int tractionLoss,
        bool avoidHeavyInputs,
        bool pushSafe,
        string tempTrend,
        int completedLaps)
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
            sessionId,
            front,
            rear,
            warmupState,
            readiness,
            gripConfidence,
            cornersUntilReady,
            onOutLap,
            inRaceStint,
            tractionLoss,
            avoidHeavyInputs,
            pushSafe,
            tempTrend,
            completedLaps));

        return string.Join(" ", messageParts.Where(part => !string.IsNullOrWhiteSpace(part)));
    }

    private static string BuildTyreConditionMessage(
        Guid sessionId,
        IReadOnlyList<TyreCornerAssessment> corners,
        IReadOnlyList<TyreAxleAssessment> axles,
        bool rearDataUnavailable,
        TyreAxleAssessment? front,
        TyreAxleAssessment? rear,
        TyreWarmupState warmupState,
        string tempTrend,
        bool onOutLap,
        bool inRaceStint,
        int completedLaps,
        string overheatingRisk)
    {
        var cornerSummary = string.Join(
            ", ",
            corners.Where(corner => corner.HasData).Select(corner => $"{corner.Label} {FormatReadinessState(corner.WarmupState).ToLowerInvariant()} {corner.TempC:0.0}C"));
        var trendText = tempTrend switch
        {
            "rising" => "Temperatures are rising through recent corners.",
            "falling" => "Temperatures are cooling slightly through recent corners.",
            _ => "Temperatures are holding steady through recent corners."
        };
        var phaseText = onOutLap
            ? "Out-lap phase: fronts and rears are still settling."
            : inRaceStint
                ? "Race stint: focus on tyre evolution lap to lap."
                : completedLaps switch
                {
                    0 => "Opening laps: tyres are still building baseline temperature.",
                    1 => "First flying lap complete: compare front/rear balance.",
                    >= 3 => "Stint established: track how each axle is evolving.",
                    _ => "Early stint: keep monitoring front/rear balance."
                };

        var balanceText = front is not null && rear is not null
            ? $"Front axle {FormatReadinessState(front.WarmupState).ToLowerInvariant()} at {front.AverageTempC:0.0}C, rear {FormatReadinessState(rear.WarmupState).ToLowerInvariant()} at {rear.AverageTempC:0.0}C."
            : axles.FirstOrDefault()?.Summary ?? "Axle balance is still forming.";

        var riskText = overheatingRisk == "High"
            ? "Heat load is high on at least one axle."
            : warmupState == TyreWarmupState.Cold
                ? "Tyres are still below working temperature."
                : warmupState == TyreWarmupState.OptimalWindow
                    ? "Tyres are in the working window."
                    : "Tyres are building toward the working window.";

        var rearNote = rearDataUnavailable ? " Rear tyre data is unavailable." : "";
        var signature = $"{warmupState}|{tempTrend}|{completedLaps}|{overheatingRisk}";
        return CoachAnswerVariation.Choose(
            sessionId,
            CoachQueryTopic.Tyre,
            signature,
            [
                $"{cornerSummary}. {balanceText} {trendText} {phaseText}{rearNote}",
                $"{phaseText} {balanceText} {trendText} Current corners: {cornerSummary}.{rearNote}",
                $"{riskText} {balanceText} {trendText} Corner snapshot: {cornerSummary}.{rearNote}"
            ]);
    }

    private static string BuildTyreConditionSpokenSummary(
        Guid sessionId,
        IReadOnlyList<TyreCornerAssessment> corners,
        IReadOnlyList<TyreAxleAssessment> axles,
        bool rearDataUnavailable,
        TyreWarmupState warmupState,
        string tempTrend,
        int completedLaps)
    {
        var front = axles.FirstOrDefault(item => item.Label == "Front");
        var rear = axles.FirstOrDefault(item => item.Label == "Rear");
        var summary = front is not null && rear is not null
            ? $"Front {FormatReadinessState(front.WarmupState).ToLowerInvariant()}, rear {FormatReadinessState(rear.WarmupState).ToLowerInvariant()}."
            : string.Join(
                ", ",
                corners.Where(corner => corner.HasData).Select(corner => $"{corner.ShortLabel} {FormatReadinessState(corner.WarmupState).ToLowerInvariant()}").Take(2));
        var trend = tempTrend == "rising"
            ? "Temps rising."
            : tempTrend == "falling"
                ? "Temps cooling."
                : "Temps stable.";
        var phase = completedLaps switch
        {
            0 => "Opening lap.",
            1 => "First lap done.",
            >= 3 => "Stint established.",
            _ => "Early stint."
        };
        var rearNote = rearDataUnavailable ? " Rear unavailable." : "";
        var signature = $"{warmupState}|{tempTrend}|spoken|{completedLaps}";
        return CoachAnswerVariation.Choose(
            sessionId,
            CoachQueryTopic.Tyre,
            signature,
            [
                $"{summary} {trend} {phase}{rearNote}".Trim(),
                $"{phase} {summary} {trend}{rearNote}".Trim(),
                $"{trend} {summary}{rearNote}".Trim()
            ]);
    }

    private static string BuildActionMessage(
        Guid sessionId,
        TyreAxleAssessment? front,
        TyreAxleAssessment? rear,
        TyreWarmupState warmupState,
        TyreReadiness readiness,
        GripConfidenceLevel gripConfidence,
        int? cornersUntilReady,
        bool onOutLap,
        bool inRaceStint,
        int tractionLoss,
        bool avoidHeavyInputs,
        bool pushSafe,
        string tempTrend,
        int completedLaps)
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
            if (completedLaps >= 2 && warmupState == TyreWarmupState.OptimalWindow)
            {
                return CoachAnswerVariation.Choose(
                    sessionId,
                    CoachQueryTopic.PushConfidence,
                    "warm-optimal",
                    [
                        "Tyres are in window but the car still needs one clean lap to settle.",
                        "Temperature is good, but build attack gradually for one more lap."
                    ]);
            }

            string[] waitOptions = cornersUntilReady is > 0 and <= 3
                ?
                [
                    $"Fronts still building. Give it {cornersUntilReady} more corners before full attack.",
                    $"Rears are closer than fronts. Build pace over the next {cornersUntilReady} corners.",
                    $"Temperature trend is {tempTrend}. Use the next {cornersUntilReady} corners to build grip."
                ]
                : completedLaps switch
                {
                    0 =>
                    [
                        "Out lap: build temperature smoothly before asking for peak grip.",
                        "Opening lap: focus on clean inputs while the tyres wake up."
                    ],
                    1 =>
                    [
                        "First flying lap: fronts and rears are still balancing.",
                        "After lap one: keep building temperature before maximum attack."
                    ],
                    _ =>
                    [
                        "Tyres are usable but still stabilizing through this stint.",
                        "Keep building temperature lap by lap before maximum attack."
                    ]
                };

            return CoachAnswerVariation.Choose(
                sessionId,
                CoachQueryTopic.PushConfidence,
                $"wait|{completedLaps}|{cornersUntilReady}|{tempTrend}",
                waitOptions);
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
        Guid sessionId,
        IReadOnlyList<TyreCornerAssessment> corners,
        bool rearDataUnavailable,
        TyreReadiness readiness,
        int? cornersUntilReady,
        bool pushSafe,
        string tempTrend,
        int completedLaps)
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
            return $"{countText} {state}.{rearNote} {BuildSpokenAction(sessionId, readiness, cornersUntilReady, pushSafe, tempTrend, completedLaps)}".Trim();
        }

        var cornerText = string.Join(
            ", ",
            available.Select(corner => $"{corner.ShortLabel} {FormatReadinessState(corner.WarmupState).ToLowerInvariant()}"));
        var unavailableNote = rearDataUnavailable ? " Rear data unavailable." : "";
        return $"{cornerText}.{unavailableNote} {BuildSpokenAction(sessionId, readiness, cornersUntilReady, pushSafe, tempTrend, completedLaps)}".Trim();
    }

    private static string BuildSpokenAction(
        Guid sessionId,
        TyreReadiness readiness,
        int? cornersUntilReady,
        bool pushSafe,
        string tempTrend,
        int completedLaps)
    {
        string[] options = readiness switch
        {
            TyreReadiness.NotReady or TyreReadiness.Building when cornersUntilReady is > 0 and <= 3 =>
            [
                $"Build over {cornersUntilReady} corners.",
                $"Next {cornersUntilReady} corners to settle.",
                $"Temps {tempTrend}. Build over {cornersUntilReady} corners."
            ],
            TyreReadiness.NotReady or TyreReadiness.Building when completedLaps <= 1 =>
            [
                "Build on this lap.",
                "Wake tyres smoothly.",
                "Early stint. Build pace."
            ],
            TyreReadiness.NotReady or TyreReadiness.Building =>
            [
                "Build pace gradually.",
                "Still stabilizing.",
                "Increase attack in steps."
            ],
            TyreReadiness.Overheated => ["Protect grip.", "Ease sliding.", "Manage heat."],
            TyreReadiness.Fading => ["Protect pace.", "Grip dropping.", "Manage stint."],
            TyreReadiness.PushNow when pushSafe => ["Safe to push.", "Attack now.", "Grip ready."],
            TyreReadiness.ReadyToPush when pushSafe => ["Push gradually.", "Build attack.", "Grip usable."],
            _ => ["Push gradually.", "Build attack.", "Increase in steps."]
        };

        return CoachAnswerVariation.Choose(sessionId, CoachQueryTopic.PushConfidence, $"spoken|{readiness}|{completedLaps}", options);
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
