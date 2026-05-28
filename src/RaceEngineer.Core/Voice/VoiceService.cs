using RaceEngineer.Core.Coaching;
using RaceEngineer.Core.Coaching.Ai;
using RaceEngineer.Core.Profile;
using RaceEngineer.Core.Session;

namespace RaceEngineer.Core.Voice;

public interface IVoiceOutput
{
    void Speak(string text);
}

public sealed class RecordingVoiceOutput : IVoiceOutput
{
    public List<string> SpokenTexts { get; } = [];

    public void Speak(string text)
    {
        SpokenTexts.Add(text);
    }
}

public sealed record SpeechAttemptResult(bool Spoken, string Diagnostic);

public sealed record VoiceQueryResult(
    CoachMessage WrittenResponse,
    string SpokenResponse,
    bool Spoken,
    string SpeechDiagnostic,
    string OriginalAnswer = "",
    string GeneratedSummary = "",
    string FinalTtsPayload = "",
    string FinalDisplayedText = "");

public sealed class VoiceService
{
    private readonly IVoiceOutput output;
    private readonly VoiceInteractionGate interactionGate;

    public VoiceService(IVoiceOutput? output = null, VoiceInteractionGate? interactionGate = null)
    {
        this.output = output ?? new RecordingVoiceOutput();
        this.interactionGate = interactionGate ?? new VoiceInteractionGate();
    }

    public VoiceInteractionGate InteractionGate => interactionGate;

    public bool VoiceEnabled { get; private set; }
    public bool EngineerMuted { get; private set; }
    public bool PushToTalkEnabled { get; private set; }
    public string LastSpokenCallout { get; private set; } = "";
    public string StateText => VoiceEnabled ? "Voice enabled" : "Voice disabled";
    public string MuteText => EngineerMuted ? "Engineer muted" : "Engineer audible";
    public string InteractionDiagnostic => interactionGate.LastDiagnostic;

    public void SetVoiceEnabled(bool enabled) => VoiceEnabled = enabled;

    public void SetPushToTalk(bool enabled) => PushToTalkEnabled = enabled;

    public void SetMuted(bool muted) => EngineerMuted = muted;

    public void TestVoiceOutput()
    {
        _ = SpeakDirectAnswer("Radio check.");
    }

    public bool SpeakAutomaticCallout(string callout, DateTimeOffset timestamp)
    {
        if (!interactionGate.IsAutomaticCalloutAllowed(timestamp))
        {
            var reason = interactionGate.AutomaticSuppressionReason(timestamp);
            interactionGate.LogAutomaticCalloutSuppressed(reason);
            return false;
        }

        var result = SpeakInternal(VoiceText.RaceSafeCallout(callout, maxWords: 10), skipFinalLimit: true);
        if (result.Spoken)
        {
            interactionGate.LogAutomaticCalloutSpoken(result.Diagnostic);
        }
        else
        {
            interactionGate.LogAutomaticCalloutSuppressed(result.Diagnostic);
        }

        return result.Spoken;
    }

    public SpeechAttemptResult SpeakDirectAnswer(string content, CoachPreferencesRecord? preferences = null)
    {
        interactionGate.BeginUserQuestionQuietWindow(DateTimeOffset.UtcNow);
        var message = new CoachMessage("coach", content, [], null, []);
        var summaryResult = SpokenSummaryGenerator.GenerateSpokenSummary(message, preferences);
        var payload = SpokenSummaryGenerator.ComposeSpokenPayload(
            summaryResult.Summary,
            confirmQuery: false,
            SpokenSummaryGenerator.ResolveMaxWords(preferences));
        var result = SpeakInternal(payload);
        if (result.Spoken)
        {
            interactionGate.LogDirectAnswerSpoken(payload);
        }
        else
        {
            interactionGate.LogDirectAnswerNotSpoken(result.Diagnostic);
        }

        return result;
    }

    public VoiceQueryResult HandleSpokenQuery(
        SessionState session,
        string query,
        ICoachEngine coachEngine,
        CoachContext? context = null,
        CoachEvidenceBundle? evidence = null,
        bool confirmQuery = false,
        CoachPreferencesRecord? preferences = null)
    {
        interactionGate.BeginUserQuestionQuietWindow(DateTimeOffset.UtcNow);
        var pipeline = CoachQueryPipeline.Resolve(
            session,
            query,
            coachEngine,
            context,
            evidence,
            preferences,
            confirmQuery);

        var payload = pipeline.FinalTtsPayload;
        if (string.IsNullOrWhiteSpace(payload))
        {
            var diagnostic = $"summary empty: {pipeline.SpokenSummary.Diagnostic}; original='{pipeline.SpokenSummary.OriginalAnswer}'";
            interactionGate.LogDirectAnswerNotSpoken(diagnostic);
            return new VoiceQueryResult(
                pipeline.FinalWritten,
                "",
                false,
                diagnostic,
                pipeline.Trace.PrimaryAnswer,
                pipeline.SpokenSummary.GeneratedSummary,
                "",
                pipeline.FinalDisplayedText);
        }

        var speech = SpeakInternal(payload);
        var speechDiagnostic = SpokenSummaryGenerator.BuildDiagnostic(pipeline.SpokenSummary, payload);
        if (!speech.Spoken)
        {
            speechDiagnostic = $"{speech.Diagnostic}; {speechDiagnostic}";
            interactionGate.LogDirectAnswerNotSpoken(speechDiagnostic);
        }
        else
        {
            interactionGate.LogDirectAnswerSpoken(payload);
        }

        return new VoiceQueryResult(
            pipeline.FinalWritten,
            payload,
            speech.Spoken,
            speechDiagnostic,
            pipeline.Trace.PrimaryAnswer,
            pipeline.SpokenSummary.GeneratedSummary,
            payload,
            pipeline.FinalDisplayedText);
    }

    private SpeechAttemptResult SpeakInternal(string text, bool skipFinalLimit = false)
    {
        var safe = skipFinalLimit
            ? text.Trim()
            : VoiceText.LimitWords(text.Trim(), text.StartsWith("Copy.", StringComparison.Ordinal) ? 20 : 18);
        if (!VoiceEnabled)
        {
            return new SpeechAttemptResult(false, "voice disabled");
        }

        if (EngineerMuted)
        {
            return new SpeechAttemptResult(false, "engineer muted");
        }

        if (string.IsNullOrWhiteSpace(safe))
        {
            return new SpeechAttemptResult(false, "empty speech text after shortening");
        }

        if (safe.Equals("Copy.", StringComparison.Ordinal))
        {
            return new SpeechAttemptResult(false, "spoken payload collapsed to confirmation prefix only");
        }

        LastSpokenCallout = safe;
        output.Speak(safe);
        return new SpeechAttemptResult(true, safe);
    }

    [Obsolete("Use SpokenSummaryGenerator.GenerateSpokenSummary instead.")]
    public static string ShortenForSpeech(string content, CoachPreferencesRecord? preferences = null)
    {
        var message = new CoachMessage("coach", content, [], null, []);
        return SpokenSummaryGenerator.GenerateSpokenSummary(message, preferences).Summary;
    }

    [Obsolete("Use VoiceText.LimitWords or VoiceText.RaceSafeCallout instead.")]
    public static string RaceSafeText(string text, int maxWords = 10) =>
        VoiceText.RaceSafeCallout(text, maxWords);
}
