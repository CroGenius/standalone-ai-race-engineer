using RaceEngineer.Core.Coaching;
using RaceEngineer.Core.Events;
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

public sealed record VoiceQueryResult(CoachMessage WrittenResponse, string SpokenResponse);

public sealed class VoiceService
{
    private readonly IVoiceOutput output;

    public VoiceService(IVoiceOutput? output = null)
    {
        this.output = output ?? new RecordingVoiceOutput();
    }

    public bool VoiceEnabled { get; private set; }
    public bool EngineerMuted { get; private set; } = true;
    public bool PushToTalkEnabled { get; private set; }
    public string LastSpokenCallout { get; private set; } = "";
    public string StateText => VoiceEnabled ? "Voice enabled" : "Voice disabled";
    public string MuteText => EngineerMuted ? "Engineer muted" : "Engineer audible";

    public void SetVoiceEnabled(bool enabled) => VoiceEnabled = enabled;

    public void SetPushToTalk(bool enabled) => PushToTalkEnabled = enabled;

    public void SetMuted(bool muted) => EngineerMuted = muted;

    public void TestVoiceOutput()
    {
        Speak("Radio check.");
    }

    public bool Speak(string callout)
    {
        var safe = RaceSafeText(callout);
        if (!VoiceEnabled || EngineerMuted || string.IsNullOrWhiteSpace(safe))
        {
            return false;
        }

        LastSpokenCallout = safe;
        output.Speak(safe);
        return true;
    }

    public VoiceQueryResult HandleSpokenQuery(
        SessionState session,
        string query,
        CoachEngine coachEngine,
        CoachContext? context = null,
        CoachEvidenceBundle? evidence = null,
        bool confirmQuery = false)
    {
        var written = coachEngine.Answer(session, query, context, evidence);
        var spoken = ShortenForSpeech(written.Content);
        if (confirmQuery && spoken.Length > 0)
        {
            spoken = $"Copy. {spoken}";
        }

        Speak(spoken);
        return new VoiceQueryResult(written, spoken);
    }

    private static string ShortenForSpeech(string content)
    {
        var beforeEvidence = content.Split("Evidence:", StringSplitOptions.None)[0].Trim();
        return RaceSafeText(beforeEvidence);
    }

    public static string RaceSafeText(string text)
    {
        var compact = text.ReplaceLineEndings(" ").Trim();
        var firstSentenceEnd = compact.IndexOfAny(['.', '!', '?']);
        if (firstSentenceEnd >= 0)
        {
            compact = compact[..(firstSentenceEnd + 1)];
        }

        var words = compact.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return words.Length <= 10 ? compact : string.Join(' ', words.Take(10)) + ".";
    }
}

