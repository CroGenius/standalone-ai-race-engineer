using RaceEngineer.Core.Coaching;

namespace RaceEngineer.Core.Voice;

public sealed record WhisperSpeechOptions(
    string LanguageMode,
    string Prompt,
    int TrailingAudioMilliseconds,
    int PreRollAudioMilliseconds,
    float NoSpeechThreshold,
    int MicrophoneDeviceNumber,
    float MinimumPeakRmsForTranscription,
    float SpeechDetectionRmsThreshold)
{
    public const string DefaultPrompt =
        "racing engineer radio copy fuel pace lap tyre tire gume gorivo kočenje kocenje tempo braking throttle pit box boks strategija pozicija";

    public const int DefaultTrailingAudioMilliseconds = 500;
    public const int DefaultPreRollAudioMilliseconds = 400;
    public const float DefaultNoSpeechThreshold = 0.5f;
    public const float DefaultMinimumPeakRmsForTranscription = 120f;
    public const float DefaultSpeechDetectionRmsThreshold = 100f;
    public const int MinimumTrailingAudioMilliseconds = 300;
    public const int MaximumTrailingAudioMilliseconds = 700;
    public const int MinimumPreRollAudioMilliseconds = 200;
    public const int MaximumPreRollAudioMilliseconds = 800;

    public static WhisperSpeechOptions Default { get; } = new(
        WhisperLanguageModeResolver.Auto,
        DefaultPrompt,
        DefaultTrailingAudioMilliseconds,
        DefaultPreRollAudioMilliseconds,
        DefaultNoSpeechThreshold,
        -1,
        DefaultMinimumPeakRmsForTranscription,
        DefaultSpeechDetectionRmsThreshold);

    public static WhisperSpeechOptions FromSettings(AppSettings settings)
    {
        return new WhisperSpeechOptions(
            WhisperLanguageModeResolver.Normalize(settings.WhisperLanguageMode),
            ResolvePrompt(settings.WhisperPrompt),
            ClampTrailingAudioMilliseconds(settings.WhisperTrailingAudioMilliseconds),
            ClampPreRollAudioMilliseconds(settings.WhisperPreRollAudioMilliseconds),
            ClampNoSpeechThreshold(settings.WhisperNoSpeechThreshold),
            settings.MicrophoneDeviceNumber,
            ClampMinimumPeakRms(settings.WhisperMinimumPeakRms),
            ClampSpeechDetectionRms(settings.WhisperSpeechDetectionRmsThreshold));
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

    public static int ClampPreRollAudioMilliseconds(int configuredMilliseconds)
    {
        if (configuredMilliseconds <= 0)
        {
            return DefaultPreRollAudioMilliseconds;
        }

        return Math.Clamp(
            configuredMilliseconds,
            MinimumPreRollAudioMilliseconds,
            MaximumPreRollAudioMilliseconds);
    }

    public static float ClampMinimumPeakRms(float configuredPeakRms)
    {
        if (configuredPeakRms <= 0f)
        {
            return DefaultMinimumPeakRmsForTranscription;
        }

        return Math.Clamp(configuredPeakRms, 40f, 2000f);
    }

    public static float ClampSpeechDetectionRms(float configuredThreshold)
    {
        if (configuredThreshold <= 0f)
        {
            return DefaultSpeechDetectionRmsThreshold;
        }

        return Math.Clamp(configuredThreshold, 40f, 1000f);
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
            .Take(40)
            .Where(phrase => phrase.Length <= 40);
        return string.Join(' ', new[] { DefaultPrompt }.Concat(phraseHints).Distinct(StringComparer.OrdinalIgnoreCase));
    }
}
