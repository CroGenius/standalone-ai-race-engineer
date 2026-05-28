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
    string SpeechDiagnostic);

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

        var result = SpeakInternal(callout, maxWords: 10);
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
        var spoken = ShortenForSpeech(content, preferences);
        var result = SpeakInternal(spoken, CoachResponseFormatter.MaxSpeechWords(preferences));
        if (result.Spoken)
        {
            interactionGate.LogDirectAnswerSpoken(spoken);
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
        var written = coachEngine.Answer(session, query, context, evidence);
        var spoken = ShortenForSpeech(written.Content, preferences ?? context?.Preferences);
        if (confirmQuery && spoken.Length > 0)
        {
            spoken = $"Copy. {spoken}";
        }

        var speech = SpeakInternal(spoken, CoachResponseFormatter.MaxSpeechWords(preferences ?? context?.Preferences));
        if (speech.Spoken)
        {
            interactionGate.LogDirectAnswerSpoken(spoken);
        }
        else
        {
            interactionGate.LogDirectAnswerNotSpoken(speech.Diagnostic);
        }

        return new VoiceQueryResult(written, spoken, speech.Spoken, speech.Diagnostic);
    }

    private SpeechAttemptResult SpeakInternal(string text, int maxWords = 10)
    {
        var safe = RaceSafeText(text, maxWords);
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

        LastSpokenCallout = safe;
        output.Speak(safe);
        return new SpeechAttemptResult(true, safe);
    }

    public static string ShortenForSpeech(string content, CoachPreferencesRecord? preferences = null)
    {
        var action = content.Split("Evidence:", StringSplitOptions.None)[0].Trim();
        if (string.IsNullOrWhiteSpace(action))
        {
            action = content.Trim();
        }

        return RaceSafeText(action, CoachResponseFormatter.MaxSpeechWords(preferences));
    }

    public static string RaceSafeText(string text, int maxWords = 10)
    {
        var compact = text.ReplaceLineEndings(" ").Trim();
        if (compact.Length == 0)
        {
            return "";
        }

        var firstSentenceEnd = compact.IndexOfAny(['.', '!', '?']);
        var firstSentence = firstSentenceEnd >= 0
            ? compact[..(firstSentenceEnd + 1)].Trim()
            : compact;

        var words = firstSentence.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (words.Length == 0)
        {
            words = compact.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        }

        return words.Length <= maxWords ? string.Join(' ', words) : string.Join(' ', words.Take(maxWords)) + ".";
    }
}
