using System.Globalization;
using RaceEngineer.Core.Coaching;

namespace RaceEngineer.Core.Voice;

public sealed record TranscriptGateOptions(
    float MinimumConfidence = 0.50f,
    float MinimumPeakRms = 120f,
    float MinimumNormalizedRms = 350f,
    int MinimumWordCount = 2,
    bool RequireSpeechDetected = true,
    bool RequireRacingVocabulary = true)
{
    public static TranscriptGateOptions Default { get; } = new();
}

public sealed record TranscriptGateDecision(
    bool Accepted,
    string Reason,
    string SpokenRejectionMessage)
{
    public static TranscriptGateDecision Accept() =>
        new(true, "Accepted", "");

    public static TranscriptGateDecision Reject(string reason, string spokenMessage) =>
        new(false, reason, spokenMessage);
}

public static class TranscriptGate
{
    private static readonly string[] HallucinationPhrases =
    [
        "bye bye",
        "goodbye",
        "thank you",
        "thanks for watching",
        "subscribe",
        "you you",
        "uh uh",
        "mm hmm",
        "silence",
        "music",
        "applause",
        "subtitle",
        "copyright"
    ];

    private static readonly string[] RacingVocabulary = BuildRacingVocabulary();

    public static TranscriptGateDecision Evaluate(
        string text,
        float confidence,
        SpeechCaptureMetrics? metrics,
        TranscriptGateOptions? options = null)
    {
        options ??= TranscriptGateOptions.Default;
        var normalized = Normalize(text);
        if (normalized.Length == 0)
        {
            return TranscriptGateDecision.Reject("Empty transcript.", "I didn't catch that.");
        }

        if (metrics is not null)
        {
            if (metrics.SignalQuality == MicSignalQuality.Bad)
            {
                return TranscriptGateDecision.Reject(
                    "Microphone signal quality is bad.",
                    "Radio unclear.");
            }

            if (metrics.PeakRms < options.MinimumPeakRms)
            {
                return TranscriptGateDecision.Reject(
                    "Microphone signal too weak.",
                    "Radio unclear.");
            }

            if (metrics.NormalizedRms < options.MinimumNormalizedRms && !metrics.SpeechDetected)
            {
                return TranscriptGateDecision.Reject(
                    "Normalized audio level too low.",
                    "Radio unclear.");
            }

            if (options.RequireSpeechDetected && !metrics.SpeechDetected)
            {
                return TranscriptGateDecision.Reject(
                    "No speech detected in capture.",
                    "I didn't catch that.");
            }
        }

        if (confidence < options.MinimumConfidence)
        {
            return TranscriptGateDecision.Reject(
                "Speech confidence below threshold.",
                "Radio unclear.");
        }

        var words = normalized.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (words.Length < options.MinimumWordCount && !LooksLikeKnownPhrase(normalized))
        {
            return TranscriptGateDecision.Reject(
                "Transcript too short.",
                "I didn't catch that.");
        }

        if (IsLikelyHallucination(normalized))
        {
            return TranscriptGateDecision.Reject(
                "Likely hallucinated transcript.",
                "I didn't catch that.");
        }

        if (options.RequireRacingVocabulary && !MatchesRacingVocabulary(normalized))
        {
            return TranscriptGateDecision.Reject(
                "Transcript does not match racing vocabulary.",
                "I didn't catch that.");
        }

        return TranscriptGateDecision.Accept();
    }

    private static bool IsLikelyHallucination(string normalized)
    {
        foreach (var phrase in HallucinationPhrases)
        {
            if (normalized.Equals(phrase, StringComparison.OrdinalIgnoreCase)
                || normalized.Contains(phrase, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return normalized.Length <= 3;
    }

    private static bool MatchesRacingVocabulary(string normalized)
    {
        if (LooksLikeKnownPhrase(normalized))
        {
            return true;
        }

        foreach (var token in RacingVocabulary)
        {
            if (normalized.Contains(token, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static bool LooksLikeKnownPhrase(string normalized)
    {
        foreach (var phrase in CoachQueryPhrases.AllRecognitionPhrases)
        {
            var candidate = phrase.Trim().ToLowerInvariant();
            if (candidate.Length == 0)
            {
                continue;
            }

            if (normalized.Contains(candidate, StringComparison.OrdinalIgnoreCase)
                || candidate.Contains(normalized, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static string Normalize(string text) =>
        text.ReplaceLineEndings(" ").Trim().ToLowerInvariant();

    private static string[] BuildRacingVocabulary()
    {
        var tokens = CoachQueryPhrases.AllRecognitionPhrases
            .SelectMany(phrase => phrase.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            .Select(token => token.Trim().ToLowerInvariant())
            .Where(token => token.Length >= 3)
            .Concat(
            [
                "fuel", "gorivo", "tyre", "tire", "gume", "brake", "braking", "throttle", "gas",
                "pace", "tempo", "lap", "krug", "pit", "box", "boks", "strategy", "strategija",
                "position", "pozicija", "sector", "delta", "push", "copy", "radio", "engineer"
            ])
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        return tokens;
    }
}
