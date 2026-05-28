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
        var fallback = deterministic.Answer(session, userMessage, context, evidence);
        if (!ShouldTryAi(evidence))
        {
            return fallback;
        }

        var aiContext = EngineerAiContextBuilder.Build(session, userMessage, context, evidence!);
        if (aiContext.Facts.Count == 0)
        {
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
                return fallback;
            }

            var validated = EngineerAiResponseValidator.Validate(result.Answer, aiContext, options.MaxResponseWords);
            if (validated is null)
            {
                return fallback;
            }

            var uncertainty = result.Uncertainty ?? EngineerAiResponseValidator.BuildUncertainty(aiContext);
            var packets = AttachEvidencePackets(evidence!, aiContext);
            return CoachResponseFormatter.ApplyPreferences(
                new CoachMessage("coach", validated, [], uncertainty, packets),
                context?.Preferences);
        }
        catch (OperationCanceledException)
        {
            return fallback;
        }
        catch (Exception)
        {
            return fallback;
        }
    }

    public CoachMessage? ChooseLiveCallout(
        IReadOnlyList<TelemetryEvent> events,
        SessionContextAssessment? context = null) =>
        deterministic.ChooseLiveCallout(events, context);

    public string GeneratePostSessionReport(SessionState session, RacePrepPlan? plan = null) =>
        deterministic.GeneratePostSessionReport(session, plan);

    private bool ShouldTryAi(CoachEvidenceBundle? evidence) =>
        options.Enabled
        && aiProvider.IsEnabled
        && evidence is not null
        && evidence.Packets.Count > 0;

    private static IReadOnlyList<CoachEvidencePacket> AttachEvidencePackets(
        CoachEvidenceBundle evidence,
        EngineerAiContext aiContext)
    {
        var summaries = aiContext.Facts
            .Select(fact => fact.Summary)
            .ToHashSet(StringComparer.Ordinal);

        return evidence.Packets
            .Where(packet => summaries.Contains(packet.Summary))
            .Take(6)
            .ToArray();
    }
}
