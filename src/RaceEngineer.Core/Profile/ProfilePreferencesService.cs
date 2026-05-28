using RaceEngineer.Core.Voice;

namespace RaceEngineer.Core.Profile;

public sealed class ProfilePreferencesService
{
    public const string DefaultDriverId = "default";
    public const string DefaultPreferencesId = "default";

    private readonly Storage.StorageService storage;

    public ProfilePreferencesService(Storage.StorageService storage)
    {
        this.storage = storage;
    }

    public async Task<UserPreferencesBundle> LoadAsync(AppSettings appSettings, CancellationToken cancellationToken = default)
    {
        var driver = await storage.LoadDriverProfileAsync(DefaultDriverId, cancellationToken);
        var payload = await storage.LoadCoachingPreferencesAsync(DefaultPreferencesId, cancellationToken);
        var coach = UserPreferencesNormalizer.NormalizeCoach(
            payload?.Coach ?? CoachPreferencesRecord.FromAppSettings(appSettings));
        var strategy = UserPreferencesNormalizer.NormalizeStrategy(payload?.Strategy ?? StrategyPreferencesRecord.Default);
        return new UserPreferencesBundle(
            UserPreferencesNormalizer.NormalizeDriver(driver ?? DriverProfileRecord.Default),
            coach,
            strategy);
    }

    public Task SaveAsync(UserPreferencesBundle preferences, CancellationToken cancellationToken = default)
    {
        var normalized = new UserPreferencesBundle(
            UserPreferencesNormalizer.NormalizeDriver(preferences.Driver),
            UserPreferencesNormalizer.NormalizeCoach(preferences.Coach),
            UserPreferencesNormalizer.NormalizeStrategy(preferences.Strategy));
        return Task.WhenAll(
            storage.SaveDriverProfileAsync(normalized.Driver.DriverId, normalized.Driver, cancellationToken),
            storage.SaveCoachingPreferencesAsync(
                DefaultPreferencesId,
                new CoachingPreferencesPayload(normalized.Coach, normalized.Strategy),
                cancellationToken));
    }

    public static AppSettings MergeAppSettings(AppSettings baseSettings, UserPreferencesBundle preferences)
    {
        var coach = UserPreferencesNormalizer.NormalizeCoach(preferences.Coach);
        var driver = UserPreferencesNormalizer.NormalizeDriver(preferences.Driver);
        var speechCulture = ResolveSpeechCulture(driver.PreferredLanguage, coach.SpeechRecognitionCulture, baseSettings.SpeechRecognitionCulture);
        var whisperLanguage = ResolveWhisperLanguage(driver.PreferredLanguage, coach.WhisperLanguageMode, baseSettings.WhisperLanguageMode);

        return baseSettings with
        {
            VoiceEnabledDefault = coach.VoiceEnabledDefault,
            PushToTalkHotkey = coach.PushToTalkHotkey,
            VoiceInputConfirmationsEnabled = coach.VoiceInputConfirmationsEnabled,
            VoiceInputCooldownSeconds = coach.VoiceInputCooldownSeconds,
            SpeechRecognitionProvider = coach.SpeechRecognitionProvider,
            SpeechRecognitionCulture = speechCulture,
            WhisperLanguageMode = whisperLanguage,
            MicrophoneDeviceNumber = coach.MicrophoneDeviceNumber
        };
    }

    private static string ResolveSpeechCulture(string preferredLanguage, string coachCulture, string appSettingsCulture)
    {
        if (!string.IsNullOrWhiteSpace(coachCulture))
        {
            return coachCulture;
        }

        return preferredLanguage switch
        {
            "hr-HR" => "hr-HR",
            "en-US" => "en-US",
            _ => appSettingsCulture
        };
    }

    private static string ResolveWhisperLanguage(string preferredLanguage, string coachWhisperLanguage, string appSettingsWhisperLanguage)
    {
        if (!coachWhisperLanguage.Equals("auto", StringComparison.OrdinalIgnoreCase))
        {
            return coachWhisperLanguage;
        }

        return preferredLanguage switch
        {
            "hr-HR" => WhisperLanguageModeResolver.Croatian,
            "en-US" => WhisperLanguageModeResolver.English,
            _ => appSettingsWhisperLanguage
        };
    }
}
