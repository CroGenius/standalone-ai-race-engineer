using System.Reflection;
using RaceEngineer.Core.Coaching.Ai;
using RaceEngineer.Core.Knowledge;
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
    public const string UnclearTranscriptMessage = "I didn't catch that.";

    public static CoachQueryPipelineResult Resolve(
        SessionState session,
        string query,
        ICoachEngine coachEngine,
        CoachContext? context = null,
        CoachEvidenceBundle? evidence = null,
        CoachPreferencesRecord? preferences = null,
        bool confirmQuery = false,
        CoachQueryTranscriptContext? transcriptContext = null)
    {
        var effectivePreferences = preferences ?? context?.Preferences;
        var rawTranscript = transcriptContext?.RawTranscript ?? query;
        var normalizedTranscript = transcriptContext?.NormalizedTranscript
            ?? CoachQueryTopicClassifier.NormalizeQuery(query);

        if (ShouldRejectUnclearTranscript(transcriptContext, normalizedTranscript))
        {
            return BuildUnclearTranscriptResult(
                query,
                rawTranscript,
                normalizedTranscript,
                CoachQueryTopicClassifier.ClassifyPrimary(query),
                session,
                context,
                effectivePreferences,
                confirmQuery,
                transcriptContext);
        }

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
                deterministicWritten),
            context?.RaceContext);

        var raceRouting = RaceAwarenessQueryClassifier.Classify(query, context?.RaceContext);
        sanitizedWritten = RaceAwarenessAnswerValidator.Enforce(query, sanitizedWritten, session, context);
        sanitizedWritten = CoachTopicOutputGuard.RejectIdentityBleed(topic, sanitizedWritten, context?.RaceContext);
        sanitizedWritten = CoachResponsePrioritizer.Apply(sanitizedWritten, query, evidence);

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

        var (trackName, resolutionTrace) = TrackGuideZoneMapper.ResolveTrackNameWithTrace(
            context?.RaceContext,
            context?.RacePrepPlan,
            session,
            context?.PreviousStoredSessionMemory,
            context?.ReviewSessionTrack,
            context?.RecentSnapshots,
            context?.CachedTrackGuide,
            context?.KnowledgeSources,
            raiseDiagnostic: true);
        var (guide, guideSource) = TrackGuideZoneMapper.ResolveGuideWithSource(
            context?.CachedTrackGuide,
            context?.KnowledgeSources,
            trackName);
        finalWritten = TrackGuideZoneMapper.MapCoachMessage(finalWritten, guide, "CoachQueryPipeline");
        displayText = finalWritten.Content;
        enforcedSummary = TrackGuideZoneMapper.MapZoneReferences(enforcedSummary, guide, "CoachQueryPipeline.Summary");
        payload = TrackGuideZoneMapper.MapZoneReferences(payload, guide, "CoachQueryPipeline.Tts");
        TrackGuideZoneMapper.LogMappingAudit(
            "CoachQueryPipeline.Resolution",
            trackName,
            guide,
            guideSource,
            resolutionTrace.GuideLookup,
            guide?.TrackName ?? "none");

        var answerSource = InferAnswerSource(primaryWritten, deterministicWritten, sanitizedWritten, gate, requiresUnifiedOutput);
        var evidenceSelection = DescribeEvidence(evidence, topic);
        var aiDiagnostic = AiEngineerRuntime.LastDiagnostic;
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
            evidenceSelection.FullDescription,
            InferFallbackReason(requiresUnifiedOutput, gate, primaryWritten, sanitizedWritten),
            Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "unknown",
            raceRouting.Subtopic,
            raceRouting.SelectedTelemetryFields.Count > 0
                ? string.Join(", ", raceRouting.SelectedTelemetryFields)
                : null,
            raceRouting.MissingTelemetryFields.Count > 0
                ? string.Join(", ", raceRouting.MissingTelemetryFields)
                : null,
            raceRouting.FallbackReason,
            raceRouting.SelectedField,
            raceRouting.SelectedValue,
            rawTranscript,
            normalizedTranscript,
            evidenceSelection.Category,
            aiDiagnostic?.ProviderSelected,
            aiDiagnostic?.Model,
            aiDiagnostic?.TimeoutSeconds,
            aiDiagnostic?.LatencyMs,
            aiDiagnostic?.FallbackReason);

        CoachQueryDiagnosticLog.RaiseRuntime(trace);
        CoachQueryDiagnosticLog.Raise(new CoachAnswerTrace(
            query,
            topic,
            !gate.Allowed,
            gate.UnavailableMessage,
            answerSource,
            evidenceSelection.FullDescription,
            trace.FallbackReason));

        return new CoachQueryPipelineResult(finalWritten, summaryResult, payload, displayText, trace);
    }

    private static bool ShouldRejectUnclearTranscript(
        CoachQueryTranscriptContext? transcriptContext,
        string normalizedTranscript)
    {
        if (!string.IsNullOrWhiteSpace(normalizedTranscript)
            && CoachQueryPhrases.ShouldBypassTranscriptRejection(normalizedTranscript))
        {
            return false;
        }

        if (transcriptContext?.GateDecision is { Accepted: false })
        {
            return true;
        }

        if (transcriptContext?.Confidence is { } confidence
            && confidence < TranscriptGateOptions.Default.MinimumConfidence)
        {
            return true;
        }

        return false;
    }

    private static CoachQueryPipelineResult BuildUnclearTranscriptResult(
        string query,
        string rawTranscript,
        string normalizedTranscript,
        CoachQueryTopic topic,
        SessionState session,
        CoachContext? context,
        CoachPreferencesRecord? preferences,
        bool confirmQuery,
        CoachQueryTranscriptContext? transcriptContext)
    {
        var unclearMessage = transcriptContext?.GateDecision?.SpokenRejectionMessage;
        if (string.IsNullOrWhiteSpace(unclearMessage))
        {
            unclearMessage = UnclearTranscriptMessage;
        }

        var written = new CoachMessage("coach", unclearMessage, [], "Transcript rejected before routing.", []);
        var summaryResult = new SpokenSummaryResult(unclearMessage, unclearMessage, unclearMessage, "transcript-rejected");
        var payload = SpokenSummaryGenerator.ComposeSpokenPayload(
            unclearMessage,
            confirmQuery,
            SpokenSummaryGenerator.ResolveMaxWords(preferences));
        var raceRouting = RaceAwarenessQueryClassifier.Classify(query, context?.RaceContext);
        var trace = new CoachQueryRuntimeTrace(
            query,
            topic,
            context?.SessionContext?.SessionModeLabel ?? SessionContextAssessment.InitialLive.SessionModeLabel,
            session.CompletedLaps.Count,
            false,
            unclearMessage,
            unclearMessage,
            unclearMessage,
            unclearMessage,
            payload,
            unclearMessage,
            "transcript-rejected",
            null,
            transcriptContext?.GateDecision?.Reason ?? "Transcript rejected before routing.",
            Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "unknown",
            raceRouting.Subtopic,
            null,
            null,
            transcriptContext?.GateDecision?.Reason,
            null,
            null,
            rawTranscript,
            normalizedTranscript,
            null);

        CoachQueryDiagnosticLog.RaiseRuntime(trace);
        CoachQueryDiagnosticLog.Raise(new CoachAnswerTrace(
            query,
            topic,
            false,
            unclearMessage,
            "transcript-rejected",
            null,
            trace.FallbackReason));

        return new CoachQueryPipelineResult(written, summaryResult, payload, unclearMessage, trace);
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

    private static (string? Category, string? FullDescription) DescribeEvidence(
        CoachEvidenceBundle? evidence,
        CoachQueryTopic topic)
    {
        if (evidence is null || evidence.Packets.Count == 0)
        {
            return (null, null);
        }

        var evidenceTopic = CoachQueryTopicClassifier.ToEvidenceTopic(topic);
        if (evidenceTopic is not null)
        {
            var selected = evidence.Select(evidenceTopic.Value).FirstOrDefault();
            if (selected is not null)
            {
                return (selected.Category, $"{selected.Category}/{selected.Summary}");
            }

            if (CoachQueryTopicClassifier.BlocksIdentityRouting(topic))
            {
                return (null, null);
            }
        }

        if (CoachQueryTopicClassifier.BlocksIdentityRouting(topic))
        {
            return (null, null);
        }

        var first = evidence.Packets.FirstOrDefault(packet =>
            !string.Equals(packet.Category, "RaceAwareness", StringComparison.Ordinal));
        if (first is null)
        {
            return (null, null);
        }

        return (first.Category, $"{first.Category}/{first.Summary}");
    }
}
