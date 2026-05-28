using System.Reflection;
using RaceEngineer.Core.Coaching.Ai;
using RaceEngineer.Core.Profile;
using RaceEngineer.Core.RaceAwareness;
using RaceEngineer.Core.Session;
using RaceEngineer.Core.SessionContext;
using RaceEngineer.Core.Voice;

namespace RaceEngineer.Core.Coaching;

public sealed record CoachQueryPipelineResult(
    CoachMessage FinalWritten,
    SpokenSummaryResult SpokenSummary,
    string FinalTtsPayload,
    string FinalDisplayedText,
    CoachQueryRuntimeTrace Trace);

public static class CoachQueryPipeline
{
    public const string ExpectedStationaryBrakingGateMessage = "No braking data yet. Drive a clean lap first.";

    public static CoachQueryPipelineResult Resolve(
        SessionState session,
        string query,
        ICoachEngine coachEngine,
        CoachContext? context = null,
        CoachEvidenceBundle? evidence = null,
        CoachPreferencesRecord? preferences = null,
        bool confirmQuery = false)
    {
        var effectivePreferences = preferences ?? context?.Preferences;
        var topic = DrivingTechniqueOutputSanitizer.ResolveTopic(query);
        var gate = DrivingTechniqueGate.Evaluate(topic, session, context?.SessionContext);
        var deterministicWritten = coachEngine.BuildDeterministicAnswer(session, query, context, evidence);
        var primaryWritten = coachEngine.Answer(session, query, context, evidence);
        var sanitizedWritten = CoachTopicOutputGuard.EnforceTopicIsolation(
            topic,
            DrivingTechniqueOutputSanitizer.EnforceFinalWritten(
                topic,
                session,
                context?.SessionContext,
                primaryWritten,
                deterministicWritten));

        var raceRouting = RaceAwarenessQueryClassifier.Classify(query, context?.RaceContext);
        sanitizedWritten = RaceAwarenessAnswerValidator.Enforce(query, sanitizedWritten, session, context);

        var summaryResult = SpokenSummaryGenerator.GenerateSpokenSummary(sanitizedWritten, effectivePreferences, query);
        if (string.IsNullOrWhiteSpace(summaryResult.Summary))
        {
            summaryResult = SpokenSummaryGenerator.GenerateSpokenSummary(deterministicWritten, effectivePreferences, query);
        }

        var enforcedSummary = DrivingTechniqueOutputSanitizer.EnforceSpokenSummary(
            topic,
            gate,
            summaryResult.Summary,
            sanitizedWritten);

        summaryResult = summaryResult with
        {
            Summary = enforcedSummary,
            GeneratedSummary = enforcedSummary
        };

        var payload = SpokenSummaryGenerator.ComposeSpokenPayload(
            enforcedSummary,
            confirmQuery,
            SpokenSummaryGenerator.ResolveMaxWords(effectivePreferences));
        payload = DrivingTechniqueOutputSanitizer.EnforceTtsPayload(topic, gate, payload, enforcedSummary);

        var requiresUnifiedOutput = !gate.Allowed
            || DrivingTechniqueOutputSanitizer.MustSanitize(topic, primaryWritten.Content);
        var displayText = requiresUnifiedOutput
            ? enforcedSummary
            : sanitizedWritten.Content;

        var finalWritten = new CoachMessage(
            sanitizedWritten.Role,
            displayText,
            requiresUnifiedOutput ? [] : sanitizedWritten.GroundedEventIds,
            requiresUnifiedOutput ? gate.EvidenceReason : sanitizedWritten.Uncertainty,
            requiresUnifiedOutput ? [] : sanitizedWritten.EvidencePackets);

        var answerSource = InferAnswerSource(primaryWritten, deterministicWritten, sanitizedWritten, gate, requiresUnifiedOutput);
        var trace = new CoachQueryRuntimeTrace(
            query,
            topic,
            context?.SessionContext?.SessionModeLabel ?? SessionContextAssessment.InitialLive.SessionModeLabel,
            session.CompletedLaps.Count,
            !gate.Allowed,
            gate.UnavailableMessage,
            DrivingTechniqueOutputSanitizer.ExtractActionText(deterministicWritten.Content),
            DrivingTechniqueOutputSanitizer.ExtractActionText(primaryWritten.Content),
            enforcedSummary,
            payload,
            displayText,
            answerSource,
            DescribeEvidence(evidence, topic),
            InferFallbackReason(requiresUnifiedOutput, gate, primaryWritten, sanitizedWritten),
            Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "unknown",
            raceRouting.Subtopic,
            raceRouting.SelectedTelemetryFields.Count > 0
                ? string.Join(", ", raceRouting.SelectedTelemetryFields)
                : null,
            raceRouting.MissingTelemetryFields.Count > 0
                ? string.Join(", ", raceRouting.MissingTelemetryFields)
                : null,
            raceRouting.FallbackReason);

        CoachQueryDiagnosticLog.RaiseRuntime(trace);
        CoachQueryDiagnosticLog.Raise(new CoachAnswerTrace(
            query,
            topic,
            !gate.Allowed,
            gate.UnavailableMessage,
            answerSource,
            DescribeEvidence(evidence, topic),
            trace.FallbackReason));

        return new CoachQueryPipelineResult(finalWritten, summaryResult, payload, displayText, trace);
    }

    private static string InferAnswerSource(
        CoachMessage primary,
        CoachMessage deterministic,
        CoachMessage sanitized,
        DrivingTechniqueGateResult gate,
        bool unifiedOutputApplied)
    {
        if (!gate.Allowed)
        {
            return "pipeline-gate";
        }

        if (unifiedOutputApplied)
        {
            return "pipeline-sanitizer";
        }

        if (sanitized.Content == primary.Content)
        {
            return primary.Content == deterministic.Content ? "deterministic" : "hybrid-primary";
        }

        return "pipeline-sanitizer";
    }

    private static string? InferFallbackReason(
        bool unifiedOutputApplied,
        DrivingTechniqueGateResult gate,
        CoachMessage primary,
        CoachMessage sanitized)
    {
        if (!gate.Allowed)
        {
            return gate.EvidenceReason;
        }

        if (unifiedOutputApplied && primary.Content != sanitized.Content)
        {
            return "Final output sanitizer replaced blocked technique answer.";
        }

        return null;
    }

    private static string? DescribeEvidence(CoachEvidenceBundle? evidence, CoachQueryTopic topic)
    {
        if (evidence is null || evidence.Packets.Count == 0)
        {
            return null;
        }

        var evidenceTopic = CoachQueryTopicClassifier.ToEvidenceTopic(topic);
        if (evidenceTopic is not null)
        {
            var selected = evidence.Select(evidenceTopic.Value).FirstOrDefault();
            if (selected is not null)
            {
                return $"{selected.Category}/{selected.Summary}";
            }
        }

        var first = evidence.Packets[0];
        return $"{first.Category}/{first.Summary}";
    }
}
