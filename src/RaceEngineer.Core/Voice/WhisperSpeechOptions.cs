using RaceEngineer.Core.Coaching;

namespace RaceEngineer.Core.Voice;

public sealed record WhisperSpeechOptions(
    string LanguageMode,
    string Prompt,
    int TrailingAudioMilliseconds,
    float NoSpeechThreshold)
{
    public const string DefaultPrompt =
        "racing engineer telemetry braking throttle fuel pace sector lap how is my braking kako kočim kako kocim gorivo tempo kočenje gas";

    public const int DefaultTrailingAudioMilliseconds = 500;
    public const float DefaultNoSpeechThreshold = 0.5f;
    public const int MinimumTrailingAudioMilliseconds = 300;
    public const int MaximumTrailingAudioMilliseconds = 700;

    public static WhisperSpeechOptions Default { get; } = new(
        WhisperLanguageModeResolver.Auto,
        DefaultPrompt,
        DefaultTrailingAudioMilliseconds,
        DefaultNoSpeechThreshold);

    public static WhisperSpeechOptions FromSettings(AppSettings settings)
    {
        return new WhisperSpeechOptions(
            WhisperLanguageModeResolver.Normalize(settings.WhisperLanguageMode),
            ResolvePrompt(settings.WhisperPrompt),
            ClampTrailingAudioMilliseconds(settings.WhisperTrailingAudioMilliseconds),
            ClampNoSpeechThreshold(settings.WhisperNoSpeechThreshold));
    }

    public static string ResolvePrompt(string? configuredPrompt)
    {
        if (string.IsNullOrWhiteSpace(configuredPrompt))
        {
            return BuildDefaultPrompt();
        }

        return configuredPrompt.Trim();
    }

    public static int ClampTrailingAudioMilliseconds(int configuredMilliseconds)
    {
        if (configuredMilliseconds <= 0)
        {
            return DefaultTrailingAudioMilliseconds;
        }

        return Math.Clamp(
            configuredMilliseconds,
            MinimumTrailingAudioMilliseconds,
            MaximumTrailingAudioMilliseconds);
    }

    public static float ClampNoSpeechThreshold(float configuredThreshold)
    {
        if (configuredThreshold <= 0f)
        {
            return DefaultNoSpeechThreshold;
        }

        return Math.Clamp(configuredThreshold, 0.1f, 0.95f);
    }

    private static string BuildDefaultPrompt()
    {
        var phraseHints = CoachQueryPhrases.AllRecognitionPhrases
            .Take(12)
            .Where(phrase => phrase.Length <= 32);
        return string.Join(' ', new[] { DefaultPrompt }.Concat(phraseHints).Distinct(StringComparer.OrdinalIgnoreCase));
    }
}
