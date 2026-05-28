using RaceEngineer.Core.Events;
using RaceEngineer.Core.Profile;
using RaceEngineer.Core.Session;
using RaceEngineer.Core.SessionContext;

namespace RaceEngineer.Core.Coaching.Ai;

public sealed class HybridCoachEngine : ICoachEngine
{
    private readonly CoachEngine deterministic = new();
    private readonly IEngineerAiProvider aiProvider;
    private readonly EngineerAiOptions options;

    public HybridCoachEngine(IEngineerAiProvider aiProvider, EngineerAiOptions options)
    {
        this.aiProvider = aiProvider;
        this.options = options;
    }

    public static HybridCoachEngine FromSettings(Core.AppSettings settings) =>
        new(EngineerAiProviderFactory.Create(settings), EngineerAiOptions.FromAppSettings(settings));

    public CoachMessage BuildDeterministicAnswer(
        SessionState session,
        string userMessage,
        CoachContext? context = null,
        CoachEvidenceBundle? evidence = null) =>
        deterministic.Answer(session, userMessage, context, evidence);

    public CoachMessage Answer(
        SessionState session,
        string userMessage,
        CoachContext? context = null,
        CoachEvidenceBundle? evidence = null)
    {
        var topic = CoachQueryTopicClassifier.ClassifyPrimary(userMessage);
        if (topic == CoachQueryTopic.Unknown && userMessage.Contains("brake", StringComparison.OrdinalIgnoreCase))
        {
            topic = CoachQueryTopic.Braking;
        }

        var gate = DrivingTechniqueGate.Evaluate(topic, session, context?.SessionContext);
        var fallback = deterministic.Answer(session, userMessage, context, evidence);

        if (!gate.Allowed)
        {
            LogTrace(userMessage, topic, gate, "deterministic-gate", null, "DrivingTechniqueGate blocked AI path.");
            return fallback;
        }

        if (!ShouldTryAi(evidence, topic, session, context))
        {
            LogTrace(
                userMessage,
                topic,
                gate,
                "deterministic",
                DescribeEvidence(evidence, topic),
                "AI disabled or no eligible evidence.");
            return fallback;
        }

        var aiContext = EngineerAiContextBuilder.Build(session, userMessage, context, evidence!);
        if (aiContext.Facts.Count == 0)
        {
            LogTrace(
                userMessage,
                topic,
                gate,
                "deterministic-fallback",
                DescribeEvidence(evidence, topic),
                "AI context had no eligible facts.");
            return fallback;
        }

        try
        {
            using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(options.TimeoutSeconds));
            var result = aiProvider
                .GenerateAnswerAsync(new EngineerAiRequest(userMessage, aiContext, options.MaxResponseWords), cancellation.Token)
                .GetAwaiter()
                .GetResult();

            if (!result.Success || string.IsNullOrWhiteSpace(result.Answer))
            {
                LogTrace(
                    userMessage,
                    topic,
                    gate,
                    "deterministic-fallback",
                    aiContext.Facts[0].Topic,
                    result.FailureReason ?? "AI provider returned no answer.");
                return fallback;
            }

            if (DrivingTechniqueGate.IsDrivingTechniqueTopic(topic)
                && DrivingTechniqueGate.ContainsBlockedTechniqueLeak(result.Answer))
            {
                LogTrace(
                    userMessage,
                    topic,
                    gate,
                    "deterministic-fallback",
                    aiContext.Facts[0].Topic,
                    "AI answer leaked invalid technique evidence.");
                return fallback;
            }

            var validated = EngineerAiResponseValidator.Validate(result.Answer, aiContext, options.MaxResponseWords);
            if (validated is null)
            {
                LogTrace(
                    userMessage,
                    topic,
                    gate,
                    "deterministic-fallback",
                    aiContext.Facts[0].Topic,
                    "AI answer failed validation.");
                return fallback;
            }

            var uncertainty = result.Uncertainty ?? EngineerAiResponseValidator.BuildUncertainty(aiContext);
            var packets = AttachEvidencePackets(evidence!, aiContext);
            LogTrace(
                userMessage,
                topic,
                gate,
                "hybrid-ai",
                aiContext.Facts[0].Topic,
                null);
            return CoachResponseFormatter.ApplyPreferences(
                new CoachMessage("coach", validated, [], uncertainty, packets),
                context?.Preferences);
        }
        catch (OperationCanceledException)
        {
            LogTrace(userMessage, topic, gate, "deterministic-fallback", null, "AI request timed out.");
            return fallback;
        }
        catch (Exception exception)
        {
            LogTrace(userMessage, topic, gate, "deterministic-fallback", null, exception.Message);
            return fallback;
        }
    }

    public CoachMessage? ChooseLiveCallout(
        IReadOnlyList<TelemetryEvent> events,
        SessionContextAssessment? context = null) =>
        deterministic.ChooseLiveCallout(events, context);

    public string GeneratePostSessionReport(SessionState session, RacePrepPlan? plan = null) =>
        deterministic.GeneratePostSessionReport(session, plan);

    private bool ShouldTryAi(
        CoachEvidenceBundle? evidence,
        CoachQueryTopic topic,
        SessionState session,
        CoachContext? context)
    {
        if (!options.Enabled || !aiProvider.IsEnabled || evidence is null || evidence.Packets.Count == 0)
        {
            return false;
        }

        if (!DrivingTechniqueGate.Evaluate(topic, session, context?.SessionContext).Allowed)
        {
            return false;
        }

        return true;
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

    private static void LogTrace(
        string query,
        CoachQueryTopic topic,
        DrivingTechniqueGateResult gate,
        string answerSource,
        string? selectedEvidenceType,
        string? fallbackReason)
    {
        CoachQueryDiagnosticLog.Raise(new CoachAnswerTrace(
            query,
            topic,
            !gate.Allowed,
            gate.UnavailableMessage,
            answerSource,
            selectedEvidenceType,
            fallbackReason));
    }

    private static IReadOnlyList<CoachEvidencePacket> AttachEvidencePackets(
        CoachEvidenceBundle evidence,
        EngineerAiContext aiContext)
    {
        var summaries = aiContext.Facts
            .Select(fact => fact.Summary)
            .ToHashSet(StringComparer.Ordinal);

        return evidence.Packets
            .Where(packet => summaries.Contains(packet.Summary))
            .Where(packet => packet.Summary != EventType.InvalidLapOrFlags.ToString())
            .Take(6)
            .ToArray();
    }
}
