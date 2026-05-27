using RaceEngineer.Core;

namespace RaceEngineer.Core.Profile;

public sealed record CoachPreferencesRecord(
    string ResponseLength = "normal",
    string CalloutAggressiveness = "normal",
    bool VoiceEnabledDefault = false,
    string SpeechRecognitionProvider = "auto",
    string SpeechRecognitionCulture = "",
    string PushToTalkHotkey = "F6",
    bool VoiceInputConfirmationsEnabled = true,
    bool EvidenceBulletsEnabled = true,
    string WhisperLanguageMode = "auto",
    int VoiceInputCooldownSeconds = 3)
{
    public static CoachPreferencesRecord Default { get; } = new();

    public static CoachPreferencesRecord FromAppSettings(AppSettings settings)
    {
        return new CoachPreferencesRecord(
            VoiceEnabledDefault: settings.VoiceEnabledDefault,
            SpeechRecognitionProvider: settings.SpeechRecognitionProvider,
            SpeechRecognitionCulture: settings.SpeechRecognitionCulture,
            PushToTalkHotkey: settings.PushToTalkHotkey,
            VoiceInputConfirmationsEnabled: settings.VoiceInputConfirmationsEnabled,
            WhisperLanguageMode: settings.WhisperLanguageMode,
            VoiceInputCooldownSeconds: settings.VoiceInputCooldownSeconds);
    }
}
