using System.Globalization;
using RaceEngineer.Core.Analytics;
using RaceEngineer.Core.Events;
using RaceEngineer.Core.Knowledge;
using RaceEngineer.Core.Profile;
using RaceEngineer.Core.Session;
using RaceEngineer.Core.Coaching.Ai;
using RaceEngineer.Core.SessionContext;
using RaceEngineer.Core.Telemetry;

namespace RaceEngineer.Core.Coaching;

public sealed record CoachMessage(
    string Role,
    string Content,
    IReadOnlyList<Guid> GroundedEventIds,
    string? Uncertainty,
    IReadOnlyList<CoachEvidencePacket> EvidencePackets);

public sealed record CoachContext(
    IReadOnlyList<string>? RacePrepNotes = null,
    string? LoadedPreviousSessionSummary = null,
    RacePrepPlan? RacePrepPlan = null,
    IReadOnlyList<KnowledgeSource>? KnowledgeSources = null,
    bool ExternalResearchAvailable = false,
    Profile.CoachPreferencesRecord? Preferences = null,
    SessionContext.SessionContextAssessment? SessionContext = null,
    Analytics.SessionTyreIntelligence? TyreIntelligence = null);

public sealed class CoachEngine : ICoachEngine
{
    public CoachMessage? ChooseLiveCallout(
        IReadOnlyList<TelemetryEvent> events,
        SessionContextAssessment? context = null)
    {
        if (events.Count == 0)
        {
            return null;
        }

        if (context is { AllowDrivingCallouts: false })
        {
            return null;
        }

        var chosen = events
            .Where(item => ShouldSurfaceLiveCallout(item, context))
            .OrderBy(Priority)
            .FirstOrDefault();
        return chosen is null
            ? null
            : new CoachMessage("coach", chosen.SuggestedAction, [chosen.Id], null, []);
    }

    public CoachMessage Answer(SessionState session, string userMessage, CoachContext? context = null, CoachEvidenceBundle? evidence = null)
    {
        return CoachResponseFormatter.ApplyPreferences(
            RouteAnswer(session, userMessage, context, evidence),
            context?.Preferences);
    }

    public CoachMessage BuildDeterministicAnswer(
        SessionState session,
        string userMessage,
        CoachContext? context = null,
        CoachEvidenceBundle? evidence = null) =>
        Answer(session, userMessage, context, evidence);

    private CoachMessage RouteAnswer(SessionState session, string userMessage, CoachContext? context, CoachEvidenceBundle? evidence)
    {
        var text = userMessage.ToLowerInvariant();
        var latest = session.LatestSnapshot;
        var recentEvents = session.RecentEvents.TakeLast(10).ToArray();
        var primaryTopic = CoachQueryTopicClassifier.ClassifyPrimary(userMessage);

        if (ContainsAny(text, "fuel plan", "fuel strategy"))
        {
            return PrepFieldAnswer("Fuel plan", context?.RacePrepPlan?.FuelPlan);
        }

        if (ContainsAny(text, "tyre plan", "tire plan"))
        {
            return PrepFieldAnswer("Tyre plan", context?.RacePrepPlan?.TyrePlan);
        }

        switch (primaryTopic)
        {
            case CoachQueryTopic.Position:
                return PositionAnswer(session);
            case CoachQueryTopic.Tyre:
                return TyreAnswer(session, context, recentEvents, evidence);
            case CoachQueryTopic.LapTime:
                return RouteLapTimeAnswer(session, text);
            case CoachQueryTopic.LosingTime:
                return AnswerDrivingTechniqueFromEvidence(userMessage, session, context, CoachQueryTopic.LosingTime, "Focus on the sector with the largest loss versus your best lap.", "No sector delta or delta trace evidence is available.", evidence, CoachEvidenceTopic.LosingTime);
            case CoachQueryTopic.Braking:
                return AnswerDrivingTechnique(userMessage, session, context, CoachQueryTopic.Braking, () => BrakeAnswer(latest, recentEvents), evidence, CoachEvidenceTopic.Braking);
            case CoachQueryTopic.Throttle:
                return AnswerDrivingTechniqueFromEvidence(userMessage, session, context, CoachQueryTopic.Throttle, "Work on smoother exit throttle and reduce hesitation.", "No throttle smoothness or trace evidence is available.", evidence, CoachEvidenceTopic.Throttle);
            case CoachQueryTopic.Improvement:
                return AnswerDrivingTechniqueFromEvidence(userMessage, session, context, CoachQueryTopic.Improvement, "Address the highest-priority weakness first.", "No improvement evidence is available.", evidence, CoachEvidenceTopic.Improvement);
            case CoachQueryTopic.LapComparison:
                return AnswerDrivingTechniqueFromEvidence(userMessage, session, context, CoachQueryTopic.LapComparison, "Use the best lap as the reference and close the largest gap.", "No lap comparison evidence is available.", evidence, CoachEvidenceTopic.LapComparison);
            case CoachQueryTopic.RacePace:
                return AnswerDrivingTechniqueFromEvidence(userMessage, session, context, CoachQueryTopic.RacePace, "Protect race pace by managing tyre, fuel, and repeat incidents.", "No race pace evidence is available.", evidence, CoachEvidenceTopic.RacePace);
            case CoachQueryTopic.Incidents:
                return AttachEvidence(RecentMistakesAnswer(recentEvents), evidence, CoachEvidenceTopic.Incidents);
            case CoachQueryTopic.Pit:
            case CoachQueryTopic.Strategy:
                return AttachEvidence(StrategyAnswer(evidence, context?.SessionContext), evidence, CoachEvidenceTopic.Strategy);
            case CoachQueryTopic.FuelAmount:
                return AttachEvidence(FuelAmountAnswer(session, recentEvents, context?.SessionContext), evidence, CoachEvidenceTopic.Fuel);
            case CoachQueryTopic.FuelConsumption:
                return AttachEvidence(FuelConsumptionAnswer(session, context?.SessionContext), evidence, CoachEvidenceTopic.Fuel);
            case CoachQueryTopic.FuelStrategy:
                return AttachEvidence(FuelStrategyAnswer(session, recentEvents, context?.SessionContext, evidence), evidence, CoachEvidenceTopic.Fuel);
        }

        if (text.Contains("brake", StringComparison.Ordinal))
        {
            return AnswerDrivingTechnique(userMessage, session, context, CoachQueryTopic.Braking, () => BrakeAnswer(latest, recentEvents), evidence, CoachEvidenceTopic.Braking);
        }

        if (ContainsAny(text, "mistake", "mistakes", "recent event", "recent events", "issues"))
        {
            return RecentMistakesAnswer(recentEvents);
        }

        if (ContainsAny(text, "previous session", "loaded session", "old session"))
        {
            return PreviousSessionAnswer(context);
        }

        if (ContainsAny(text, "combine my telemetry", "driving data say versus", "telemetry with track notes", "data versus the track guide"))
        {
            return CombinedTelemetryKnowledgeAnswer(session, recentEvents, context);
        }

        if (ContainsAny(text, "setup notes", "setup guide"))
        {
            return AttachEvidence(
                KnowledgeAnswer("Setup notes", context, source => MatchesKnowledge(source, context, "setup"), "No stored setup notes are loaded. External research is unavailable in offline mode."),
                evidence,
                CoachEvidenceTopic.SetupNotes);
        }

        if (ContainsAny(text, "strategy notes", "strategy guide"))
        {
            return KnowledgeAnswer("Strategy notes", context, source => MatchesKnowledge(source, context, "strategy"), "No stored strategy notes are loaded. External research is unavailable in offline mode.");
        }

        if (ContainsAny(text, "tell me about this track", "about this track", "watch for at", "track guide", "track notes"))
        {
            return KnowledgeAnswer("Track notes", context, source => MatchesKnowledge(source, context, "track") || !string.IsNullOrWhiteSpace(source.Track), "No stored track notes are loaded. External research is unavailable in offline mode.");
        }

        if (ContainsAny(text, "tell me about this car", "about this car", "car guide"))
        {
            return KnowledgeAnswer("Car notes", context, source => MatchesKnowledge(source, context, "car") || !string.IsNullOrWhiteSpace(source.Car), "No stored car notes are loaded. External research is unavailable in offline mode.");
        }

        if (ContainsAny(text, "summarize preparation", "preparation summary"))
        {
            return PrepSummaryAnswer(context?.RacePrepPlan);
        }

        if (ContainsAny(text, "summary", "session summary", "review"))
        {
            return SessionSummaryAnswer(session, recentEvents);
        }

        if (ContainsAny(text, "what should i focus on", "focus", "next lap", "work on"))
        {
            return recentEvents.Any(item => item.Type is not EventType.LapStart and not EventType.LapEnd and not EventType.HeavyBraking)
                ? NextLapFocusAnswer(recentEvents)
                : PrepFieldAnswer("Practice goal", context?.RacePrepPlan?.PracticeGoal);
        }

        if (ContainsAny(text, "reminders", "reminder"))
        {
            return PrepFieldAnswer("Driver reminders", context?.RacePrepPlan?.DriverReminders);
        }

        if (ContainsAny(text, "what is my plan", "prep", "notes", "plan"))
        {
            return PrepSummaryAnswer(context?.RacePrepPlan);
        }

        return recentEvents.Length > 0
            ? NextLapFocusAnswer(recentEvents)
            : Unavailable("I can answer once telemetry or stored session context is available.", "No latest snapshot, recent events, or stored notes matched the question.");
    }

    private static CoachMessage PositionAnswer(SessionState session)
    {
        var position = session.LatestSnapshot?.Race.Position;
        return position is null
            ? Unavailable("Position data is unavailable.", "Latest snapshot has no race position value.")
            : Message($"You are P{position}.", [$"position: {position}"], []);
    }

    private static CoachMessage RouteLapTimeAnswer(SessionState session, string text)
    {
        if (text.Contains("best lap", StringComparison.Ordinal))
        {
            return BestLapAnswer(session);
        }

        if (text.Contains("current lap", StringComparison.Ordinal))
        {
            return CurrentLapAnswer(session);
        }

        if (session.LastLap is { Duration: var lastDuration })
        {
            return Message(
                $"Last lap was {FormatDuration(lastDuration)}.",
                [$"lap: {session.LastLap.LapNumber}", $"time: {FormatDuration(lastDuration)}", $"valid: {session.LastLap.IsValid}"],
                []);
        }

        if (session.BestLap is { Duration: var bestDuration })
        {
            return Message(
                $"Best lap is {FormatDuration(bestDuration)}.",
                [$"lap: {session.BestLap.LapNumber}", $"time: {FormatDuration(bestDuration)}", $"valid: {session.BestLap.IsValid}"],
                []);
        }

        return Unavailable("No valid lap time yet.", "No completed lap with a valid duration has been recorded.");
    }

    private static CoachMessage TyreAnswer(
        SessionState session,
        CoachContext? context,
        IReadOnlyList<TelemetryEvent> events,
        CoachEvidenceBundle? evidence)
    {
        var intelligence = context?.TyreIntelligence
            ?? new TyreIntelligenceService().Analyze(new TyreIntelligenceInput(
                session,
                null,
                events,
                context?.SessionContext?.Activity ?? VehicleActivity.Unknown,
                context?.SessionContext?.Phase ?? SessionPhase.Unknown));

        if (!intelligence.HasReliableData)
        {
            return Unavailable(intelligence.CoachingMessage, intelligence.Availability);
        }

        var tyreEvents = events.Where(item => item.Type == EventType.TyreOverheating).ToArray();
        var resolvedEvidence = evidence ?? new CoachEvidenceBuilder().Build(new CoachEvidenceInput(
            session,
            null,
            events,
            null,
            null,
            intelligence));

        return AttachEvidence(
            new CoachMessage(
                "coach",
                intelligence.CoachingMessage,
                tyreEvents.Select(item => item.Id).ToArray(),
                $"Grip confidence {intelligence.GripConfidenceLabel.ToLowerInvariant()}; {intelligence.PushGuidance}",
                []),
            resolvedEvidence,
            CoachEvidenceTopic.Tyres);
    }

    private static CoachMessage BrakeAnswer(TelemetrySnapshot? snapshot, IReadOnlyList<TelemetryEvent> events)
    {
        var brakeEvents = events.Where(item => item.Type is EventType.UnstableBraking or EventType.AbruptBrakeRelease or EventType.BrakeOverheating or EventType.HeavyBraking).ToArray();
        var brakeTemps = snapshot?.Condition.BrakeTempC?.Where(value => value.HasValue).Select(value => value!.Value).ToArray();
        var brakeInput = snapshot?.Inputs.Brake;
        if (brakeInput is null && (brakeTemps is null or { Length: 0 }) && brakeEvents.Length == 0)
        {
            return Unavailable("Brake status is unavailable.", "Latest snapshot has no brake input, brake temperatures, or recent braking events.");
        }

        var action = brakeEvents.LastOrDefault(item => item.Type is EventType.UnstableBraking or EventType.AbruptBrakeRelease or EventType.BrakeOverheating)?.SuggestedAction
            ?? "No recent braking problem is active in the deterministic event buffer.";
        var evidence = new List<string>();
        if (brakeInput.HasValue)
        {
            evidence.Add($"current brake input: {FormatNumber(brakeInput.Value, "0.00")}");
        }

        if (brakeTemps is { Length: > 0 })
        {
            evidence.Add($"max brake temp: {FormatNumber(brakeTemps.Max(), "0.0")} C");
        }

        evidence.Add($"recent brake-related events: {brakeEvents.Length}");
        return Message(action, evidence, brakeEvents.Select(item => item.Id));
    }

    private static CoachMessage FuelAmountAnswer(
        SessionState session,
        IReadOnlyList<TelemetryEvent> events,
        SessionContextAssessment? context)
    {
        if (session.LatestFuelLevel is not { } fuel)
        {
            return Unavailable("Fuel status is unavailable.", "Latest snapshot has no fuel value.");
        }

        var action = $"You have {FormatNumber(fuel, "0.0")} liters.";
        var evidence = new List<string> { $"latest fuel: {FormatNumber(fuel, "0.0")}" };

        if (session.FuelUsedPerLap is { } fuelPerLap)
        {
            action += $" Fuel use is about {FormatNumber(fuelPerLap, "0.0")} L/lap.";
            evidence.Add($"fuel used per lap: {FormatNumber(fuelPerLap, "0.00")}");
        }
        else
        {
            evidence.Add("fuel used per lap: unavailable until enough valid laps are completed");
        }

        if (context is { HasStableLapSamples: false })
        {
            action += " Not enough race data yet for finish projections.";
        }
        else if (context is { Activity: VehicleActivity.Stationary or VehicleActivity.PitLane })
        {
            action += " Stationary — waiting for stable on-track laps before race fuel estimates.";
        }

        return Message(action, evidence, []);
    }

    private static CoachMessage FuelConsumptionAnswer(
        SessionState session,
        SessionContextAssessment? context)
    {
        if (session.FuelUsedPerLap is not { } fuelPerLap)
        {
            return Unavailable(
                "Fuel consumption is unavailable.",
                context is { HasStableLapSamples: false }
                    ? "Waiting for stable valid laps before fuel-per-lap can be estimated."
                    : "Need at least two completed fuel samples on valid laps.");
        }

        var action = $"Fuel use is about {FormatNumber(fuelPerLap, "0.0")} L/lap.";
        var evidence = new List<string> { $"fuel used per lap: {FormatNumber(fuelPerLap, "0.00")}" };

        if (session.LatestFuelLevel is { } fuel)
        {
            evidence.Add($"latest fuel: {FormatNumber(fuel, "0.0")}");
        }

        return Message(action, evidence, []);
    }

    private static CoachMessage FuelStrategyAnswer(
        SessionState session,
        IReadOnlyList<TelemetryEvent> events,
        SessionContextAssessment? context,
        CoachEvidenceBundle? evidence)
    {
        if (session.LatestFuelLevel is not { } fuel)
        {
            return Unavailable("Fuel strategy is unavailable.", "Latest snapshot has no fuel value.");
        }

        if (context is { HasStableLapSamples: false })
        {
            return Unavailable(
                "Not enough race data yet.",
                "Waiting for stable lap samples before finish fuel projections can be computed.");
        }

        var strategyPackets = evidence?.Select(CoachEvidenceTopic.Strategy).ToArray() ?? [];
        var actionParts = new List<string>();
        var evidenceLines = new List<string> { $"latest fuel: {FormatNumber(fuel, "0.0")}" };

        if (session.EstimatedLapsRemaining is { } lapsRemaining)
        {
            actionParts.Add($"About {FormatNumber(lapsRemaining, "0.0")} laps remaining on current fuel.");
            evidenceLines.Add($"estimated laps remaining: {FormatNumber(lapsRemaining, "0.0")}");
        }
        else
        {
            actionParts.Add("Finish projection is not reliable yet.");
            evidenceLines.Add("estimated laps remaining: unavailable");
        }

        var riskPacket = strategyPackets.FirstOrDefault(packet => packet.Summary == "Fuel risk")
            ?? evidence?.Select(CoachEvidenceTopic.Fuel).FirstOrDefault(packet => packet.Summary == "Fuel risk");
        if (riskPacket is not null)
        {
            actionParts.Add(riskPacket.Explanation.TrimEnd('.'));
            evidenceLines.Add($"fuel risk: {riskPacket.Explanation}");
        }

        var lowFuelEvents = events.Where(item => item.Type == EventType.LowFuel).ToArray();
        if (context?.AllowFuelRiskCallouts == true && lowFuelEvents.Length > 0)
        {
            actionParts.Add("Fuel is tight for this stint.");
        }

        if (session.FuelUsedPerLap is { } fuelPerLap)
        {
            evidenceLines.Add($"fuel used per lap: {FormatNumber(fuelPerLap, "0.00")}");
        }

        var action = actionParts.Count == 0
            ? "Fuel strategy is unavailable until more lap data is collected."
            : string.Join(" ", actionParts);

        return Message(action, evidenceLines, lowFuelEvents.Select(item => item.Id));
    }

    private static CoachMessage FuelAnswer(
        SessionState session,
        IReadOnlyList<TelemetryEvent> events,
        SessionContextAssessment? context)
    {
        return FuelAmountAnswer(session, events, context);
    }

    private static CoachMessage StrategyAnswer(CoachEvidenceBundle? evidence, SessionContextAssessment? context)
    {
        if (context is { HasStableLapSamples: false })
        {
            return Unavailable(
                "Not enough race data yet.",
                "Waiting for stable lap samples before pit strategy can be computed.");
        }

        var packets = evidence?.Select(CoachEvidenceTopic.Strategy).ToArray() ?? [];
        if (packets.Length == 0)
        {
            return Unavailable(
                "Strategy is unavailable.",
                context?.SuppressionNote ?? "Need fuel usage data and a few completed laps before pit strategy can be computed.");
        }

        if (context?.StrategyConfidence == StrategyConfidenceLevel.Low)
        {
            return Unavailable(
                "Not enough race data yet.",
                context.SuppressionNote);
        }

        var recommendation = packets.FirstOrDefault(packet => packet.Summary == "Pit recommendation");
        var action = recommendation?.Explanation
            ?? packets.FirstOrDefault(packet => packet.Summary == "Strategy summary")?.Explanation
            ?? "Review fuel, stint, and tyre risk before your next stop.";
        var bullets = packets
            .Select(packet => $"{packet.Summary}: {packet.Explanation}")
            .Distinct(StringComparer.Ordinal)
            .Take(6)
            .ToArray();
        return Message(action, bullets, []);
    }

    private static bool ShouldSurfaceLiveCallout(TelemetryEvent item, SessionContextAssessment? context)
    {
        if (context is null)
        {
            return true;
        }

        if (item.Type == EventType.LowFuel && !context.AllowLowFuelVoiceCallouts)
        {
            return false;
        }

        return context.AllowDrivingCallouts || item.Severity == EventSeverity.Critical;
    }

    private static CoachMessage LastLapAnswer(SessionState session)
    {
        var lap = session.LastLap;
        return lap is null
            ? Unavailable("No valid lap time yet.", "No completed laps have been recorded.")
            : Message("Use the last completed lap as the current baseline.", [$"lap: {lap.LapNumber}", $"time: {FormatDuration(lap.Duration)}", $"valid: {lap.IsValid}"], []);
    }

    private static CoachMessage BestLapAnswer(SessionState session)
    {
        var lap = session.BestLap;
        return lap is null
            ? Unavailable("No valid lap time yet.", "No valid completed lap with a duration has been recorded.")
            : Message("Best lap is the reference for the next comparison.", [$"lap: {lap.LapNumber}", $"time: {FormatDuration(lap.Duration)}", $"valid: {lap.IsValid}"], []);
    }

    private static CoachMessage CurrentLapAnswer(SessionState session)
    {
        if (!session.TelemetryOnline)
        {
            return Unavailable("Current lap is unavailable.", "No valid telemetry snapshot has been received.");
        }

        return Message("Stay clean on the current lap and watch repeat events.", [$"current lap: {session.CurrentLap}", $"lap valid: {session.CurrentLapIsValid}", $"stint time: {FormatDuration(session.StintDuration)}"], []);
    }

    private static CoachMessage RecentMistakesAnswer(IReadOnlyList<TelemetryEvent> events)
    {
        var mistakes = events
            .Where(item => item.Type is not EventType.LapStart and not EventType.LapEnd and not EventType.HeavyBraking)
            .GroupBy(item => item.Type)
            .OrderBy(group => group.Min(item => Priority(item)))
            .ThenByDescending(group => group.Count())
            .Take(3)
            .ToArray();
        if (mistakes.Length == 0)
        {
            return Unavailable("No recent deterministic mistakes are available.", "Recent event buffer has no driving issue events.");
        }

        var highest = mistakes.SelectMany(group => group).OrderBy(Priority).First();
        var evidence = mistakes.Select(group => $"{group.Key}: {group.Count()} recent event(s)").ToList();
        evidence.Add($"highest priority action: {highest.SuggestedAction}");
        return Message(highest.SuggestedAction, evidence, mistakes.SelectMany(group => group).Select(item => item.Id));
    }

    private static CoachMessage SessionSummaryAnswer(SessionState session, IReadOnlyList<TelemetryEvent> events)
    {
        if (!session.TelemetryOnline && session.CompletedLaps.Count == 0 && events.Count == 0)
        {
            return Unavailable("Session summary is unavailable.", "No telemetry, completed laps, or deterministic events are available.");
        }

        var evidence = new List<string>
        {
            $"completed laps: {session.CompletedLaps.Count}",
            $"current lap: {session.CurrentLap}",
            $"best lap: {FormatDuration(session.BestLap?.Duration)}",
            $"last lap: {FormatDuration(session.LastLap?.Duration)}",
            $"recent events: {events.Count}",
            $"stint time: {FormatDuration(session.StintDuration)}"
        };
        return Message("Summary is based only on recorded session facts.", evidence, events.Select(item => item.Id));
    }

    private static CoachMessage NextLapFocusAnswer(IReadOnlyList<TelemetryEvent> events)
    {
        var issue = events
            .Where(item => item.Type is not EventType.LapStart and not EventType.LapEnd and not EventType.HeavyBraking)
            .OrderBy(Priority)
            .ThenByDescending(item => item.Confidence)
            .FirstOrDefault();
        if (issue is null)
        {
            return Unavailable("Next-lap focus is unavailable.", "No recent deterministic driving issue is available.");
        }

        return Message(issue.SuggestedAction, [$"event: {issue.Type}", $"severity: {issue.Severity}", $"confidence: {FormatNumber(issue.Confidence, "0.00")}", $"lap: {issue.LapNumber?.ToString(CultureInfo.InvariantCulture) ?? "unknown"}"], [issue.Id]);
    }

    private static CoachMessage RacePrepNotesAnswer(CoachContext? context)
    {
        var notes = context?.RacePrepNotes?.Where(note => !string.IsNullOrWhiteSpace(note)).ToArray();
        return notes is null or { Length: 0 }
            ? Unavailable("Race prep notes are unavailable.", "No race prep notes are loaded in coach context.")
            : Message("Use the loaded prep reminders before the next run.", notes.Select(note => $"note: {note}"), []);
    }

    private static CoachMessage PrepFieldAnswer(string label, string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? Unavailable($"{label} is unavailable.", $"Loaded race prep has no {label.ToLowerInvariant()} field.")
            : Message($"{label}: {value}", [$"{label}: {value}"], []);
    }

    private static CoachMessage PrepSummaryAnswer(RacePrepPlan? plan)
    {
        if (plan is null || plan.IsEmpty)
        {
            return Unavailable("Preparation is unavailable.", "No structured race prep plan is loaded.");
        }

        var evidence = new[]
        {
            $"car: {ValueOrUnavailable(plan.Car)}",
            $"track: {ValueOrUnavailable(plan.Track)}",
            $"session type: {ValueOrUnavailable(plan.SessionType)}",
            $"target stint: {ValueOrUnavailable(plan.TargetStintLength)}",
            $"fuel plan: {ValueOrUnavailable(plan.FuelPlan)}",
            $"tyre plan: {ValueOrUnavailable(plan.TyrePlan)}",
            $"practice goal: {ValueOrUnavailable(plan.PracticeGoal)}",
            $"reminders: {ValueOrUnavailable(plan.DriverReminders)}",
            $"setup notes: {ValueOrUnavailable(plan.SetupNotes)}",
            $"strategy notes: {ValueOrUnavailable(plan.StrategyNotes)}"
        };
        return Message("Preparation loaded. Follow the plan and focus on the practice goal.", evidence, []);
    }

    public string GeneratePostSessionReport(SessionState session, RacePrepPlan? plan = null)
    {
        var events = session.RecentEvents;
        var recurring = events
            .Where(item => item.Type is not EventType.LapStart and not EventType.LapEnd)
            .GroupBy(item => item.Type)
            .OrderByDescending(group => group.Count())
            .Select(group => $"- {group.Key}: {group.Count()}")
            .ToArray();
        var tyreEvents = events.Count(item => item.Type == EventType.TyreOverheating);
        var brakeEvents = events.Count(item => item.Type == EventType.BrakeOverheating || item.Type == EventType.UnstableBraking || item.Type == EventType.AbruptBrakeRelease);
        var fuelEvents = events.Count(item => item.Type == EventType.LowFuel);

        return $"""
        # Post-Session Coaching Report

        ## 1. Session overview
        - Completed laps: {session.CompletedLaps.Count}
        - Stint time: {FormatDuration(session.StintDuration)}
        - Latest fuel: {(session.LatestFuelLevel.HasValue ? FormatNumber(session.LatestFuelLevel.Value, "0.0") : "unavailable")}

        ## 2. Lap performance
        - Best lap: {FormatDuration(session.BestLap?.Duration)}
        - Last lap: {FormatDuration(session.LastLap?.Duration)}

        ## 3. Main recurring issues
        {(recurring.Length > 0 ? string.Join(Environment.NewLine, recurring) : "- unavailable: no deterministic issue events recorded")}

        ## 4. Tyres/brakes/fuel
        - Tyres: {(tyreEvents > 0 ? $"{tyreEvents} overheating event(s)" : "unavailable: no tyre issue evidence recorded")}
        - Brakes: {(brakeEvents > 0 ? $"{brakeEvents} brake issue event(s)" : "unavailable: no brake issue evidence recorded")}
        - Fuel: {(fuelEvents > 0 ? $"{fuelEvents} low-fuel event(s)" : "unavailable: no low-fuel event evidence recorded")}

        ## 5. Prep goal review
        - Practice goal: {ValueOrUnavailable(plan?.PracticeGoal)}
        - Evidence followed: unavailable unless matching deterministic telemetry evidence is recorded

        ## 6. Next-session focus
        - {(recurring.Length > 0 ? recurring[0].TrimStart('-', ' ') : ValueOrUnavailable(plan?.PracticeGoal))}

        ## 7. Evidence list
        - Completed lap records: {session.CompletedLaps.Count}
        - Recent deterministic events: {events.Count}
        - Loaded prep: {(plan is null || plan.IsEmpty ? "unavailable" : "available")}
        """;
    }

    private static CoachMessage PreviousSessionAnswer(CoachContext? context)
    {
        return string.IsNullOrWhiteSpace(context?.LoadedPreviousSessionSummary)
            ? Unavailable("Previous session summary is unavailable.", "No previous session summary is loaded.")
            : Message("Loaded previous session summary is available.", [$"summary: {context.LoadedPreviousSessionSummary}"], []);
    }

    private static CoachMessage KnowledgeAnswer(string label, CoachContext? context, Func<KnowledgeSource, bool> filter, string unavailableReason)
    {
        var sources = context?.KnowledgeSources?.Where(filter).Take(5).ToArray() ?? [];
        if (sources.Length == 0)
        {
            return new CoachMessage("coach", $"{label} are unavailable.{Environment.NewLine}{Environment.NewLine}External research:{Environment.NewLine}- {unavailableReason}", [], unavailableReason, []);
        }

        var stored = sources
            .Where(source => source.SourceType is KnowledgeSourceTypes.ManualNote or KnowledgeSourceTypes.ImportedFile)
            .Select(source => $"- {source.Title}: {TrimKnowledge(source.Content)} (source: {source.SourceType}, retrieved: {source.RetrievedAt:yyyy-MM-dd HH:mm}, reliability: {ValueOrUnavailable(source.ConfidenceNote)})")
            .ToArray();
        var web = sources
            .Where(source => source.SourceType == KnowledgeSourceTypes.Web)
            .Select(source => $"- {source.Title}: {TrimKnowledge(source.Content)} (retrieved: {source.RetrievedAt:yyyy-MM-dd HH:mm}, reliability: {ValueOrUnavailable(source.ConfidenceNote)})")
            .ToArray();

        var parts = new List<string> { $"{label} from saved knowledge. Keep this separate from telemetry facts." };
        if (stored.Length > 0)
        {
            parts.Add($"{Environment.NewLine}Stored track notes:{Environment.NewLine}{string.Join(Environment.NewLine, stored)}");
        }

        parts.Add($"{Environment.NewLine}External research:{Environment.NewLine}{(web.Length > 0 ? string.Join(Environment.NewLine, web) : "- unavailable in offline mode")}");
        return new CoachMessage("coach", string.Join(Environment.NewLine, parts), [], null, []);
    }

    private static CoachMessage CombinedTelemetryKnowledgeAnswer(SessionState session, IReadOnlyList<TelemetryEvent> events, CoachContext? context)
    {
        var telemetry = new List<string>();
        if (session.LatestSnapshot?.Condition.TyreTempC?.Where(item => item.HasValue).Select(item => item!.Value).ToArray() is { Length: > 0 } temps)
        {
            telemetry.Add($"- max tyre temp: {FormatNumber(temps.Max(), "0.0")} C");
        }

        var issueEvents = events
            .Where(item => item.Type is not EventType.LapStart and not EventType.LapEnd and not EventType.HeavyBraking)
            .TakeLast(5)
            .ToArray();
        foreach (var item in issueEvents)
        {
            telemetry.Add($"- {item.Type}: {item.SuggestedAction} (lap {item.LapNumber?.ToString(CultureInfo.InvariantCulture) ?? "unknown"}, confidence {FormatNumber(item.Confidence, "0.00")})");
        }

        if (telemetry.Count == 0)
        {
            telemetry.Add("- unavailable: no latest telemetry fact or recent deterministic event is available");
        }

        var stored = context?.KnowledgeSources?
            .Where(source => MatchesKnowledge(source, context, "track") || MatchesKnowledge(source, context, "setup") || MatchesKnowledge(source, context, "strategy"))
            .Take(5)
            .Select(source => $"- {source.Title}: {TrimKnowledge(source.Content)}")
            .ToArray() ?? [];

        var content = $"""
        Compare the deterministic driving data against saved knowledge, keeping the two separate.

        Telemetry evidence:
        {string.Join(Environment.NewLine, telemetry)}

        Session evidence:
        - completed laps: {session.CompletedLaps.Count}
        - best lap: {FormatDuration(session.BestLap?.Duration)}
        - recent deterministic events: {events.Count}

        Stored track notes:
        {(stored.Length > 0 ? string.Join(Environment.NewLine, stored) : "- unavailable: no matching saved track/setup/strategy notes are loaded")}

        External research:
        {(context?.ExternalResearchAvailable == true ? "- available from loaded web sources only" : "- unavailable in offline mode")}
        """;
        return new CoachMessage("coach", content, issueEvents.Select(item => item.Id).ToArray(), stored.Length == 0 ? "No matching stored knowledge sources are loaded." : null, []);
    }

    private static bool MatchesKnowledge(KnowledgeSource source, CoachContext? context, string category)
    {
        var plan = context?.RacePrepPlan;
        var categoryMatches = string.Equals(source.Category, category, StringComparison.OrdinalIgnoreCase);
        var carMatches = string.IsNullOrWhiteSpace(source.Car)
            || string.IsNullOrWhiteSpace(plan?.Car)
            || string.Equals(source.Car, plan.Car, StringComparison.OrdinalIgnoreCase);
        var trackMatches = string.IsNullOrWhiteSpace(source.Track)
            || string.IsNullOrWhiteSpace(plan?.Track)
            || string.Equals(source.Track, plan.Track, StringComparison.OrdinalIgnoreCase);
        return categoryMatches && carMatches && trackMatches;
    }

    private static CoachMessage AnswerDrivingTechnique(
        string query,
        SessionState session,
        CoachContext? context,
        CoachQueryTopic topic,
        Func<CoachMessage> answerFactory,
        CoachEvidenceBundle? evidence,
        CoachEvidenceTopic evidenceTopic)
    {
        var gate = DrivingTechniqueGate.Evaluate(topic, session, context?.SessionContext);
        if (!gate.Allowed)
        {
            LogGateTrace(query, topic, gate, "deterministic-gate");
            return Unavailable(gate.UnavailableMessage!, gate.EvidenceReason);
        }

        return AttachEvidence(answerFactory(), evidence, evidenceTopic);
    }

    private static CoachMessage AnswerDrivingTechniqueFromEvidence(
        string query,
        SessionState session,
        CoachContext? context,
        CoachQueryTopic topic,
        string action,
        string unavailableReason,
        CoachEvidenceBundle? evidence,
        CoachEvidenceTopic evidenceTopic)
    {
        var gate = DrivingTechniqueGate.Evaluate(topic, session, context?.SessionContext);
        if (!gate.Allowed)
        {
            LogGateTrace(query, topic, gate, "deterministic-gate");
            return Unavailable(gate.UnavailableMessage!, gate.EvidenceReason);
        }

        return AnswerFromEvidence(action, unavailableReason, evidence, evidenceTopic);
    }

    private static void LogGateTrace(
        string query,
        CoachQueryTopic topic,
        DrivingTechniqueGateResult gate,
        string answerSource)
    {
        CoachQueryDiagnosticLog.Raise(new CoachAnswerTrace(
            query,
            topic,
            true,
            gate.UnavailableMessage,
            answerSource,
            null,
            gate.EvidenceReason));
    }

    private static CoachMessage AnswerFromEvidence(
        string action,
        string unavailableReason,
        CoachEvidenceBundle? evidence,
        CoachEvidenceTopic topic)
    {
        var packets = evidence?.Select(topic) ?? [];
        if (packets.Count == 0)
        {
            return Unavailable(action, unavailableReason, []);
        }

        var lead = packets[0];
        var content = string.Equals(lead.Category, "Sector", StringComparison.Ordinal)
            ? $"Focus on {lead.Summary}: {lead.Explanation}"
            : lead.Explanation;
        return new CoachMessage(
            "coach",
            content,
            packets.SelectMany(item => item.RelatedEventIds).Distinct().ToArray(),
            null,
            packets);
    }

    private static CoachMessage AttachEvidence(CoachMessage answer, CoachEvidenceBundle? evidence, CoachEvidenceTopic topic)
    {
        var packets = evidence?.Select(topic) ?? [];
        return packets.Count == 0 ? answer : answer with { EvidencePackets = packets };
    }

    private static CoachMessage Message(string action, IEnumerable<string> evidence, IEnumerable<Guid> eventIds, IReadOnlyList<CoachEvidencePacket>? packets = null)
    {
        var evidenceLines = evidence.Select(item => $"- {item}").ToArray();
        var content = evidenceLines.Length == 0
            ? action
            : $"{action}{Environment.NewLine}{Environment.NewLine}Evidence:{Environment.NewLine}{string.Join(Environment.NewLine, evidenceLines)}";
        return new CoachMessage("coach", content, eventIds.ToArray(), null, packets ?? []);
    }

    private static CoachMessage Unavailable(string action, string evidence, IReadOnlyList<CoachEvidencePacket>? packets = null)
    {
        return new CoachMessage("coach", $"{action}{Environment.NewLine}{Environment.NewLine}Evidence:{Environment.NewLine}- {evidence}", [], evidence, packets ?? []);
    }

    private static bool ContainsAny(string text, params string[] needles)
    {
        return needles.Any(needle => text.Contains(needle, StringComparison.Ordinal));
    }

    private static string FormatDuration(TimeSpan? duration)
    {
        if (!duration.HasValue)
        {
            return "unavailable";
        }

        return duration.Value.TotalHours >= 1
            ? duration.Value.ToString(@"h\:mm\:ss", CultureInfo.InvariantCulture)
            : duration.Value.ToString(@"m\:ss\.fff", CultureInfo.InvariantCulture);
    }

    private static string FormatNumber(double value, string format)
    {
        return value.ToString(format, CultureInfo.InvariantCulture);
    }

    private static string ValueOrUnavailable(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? "unavailable" : value;
    }

    private static string TrimKnowledge(string value)
    {
        const int maxLength = 420;
        var normalized = value.ReplaceLineEndings(" ").Trim();
        return normalized.Length <= maxLength ? normalized : normalized[..maxLength] + "...";
    }

    private static int Priority(TelemetryEvent item)
    {
        return item.Severity switch
        {
            EventSeverity.Critical => 0,
            EventSeverity.Warning => 1,
            _ => 2
        };
    }
}
