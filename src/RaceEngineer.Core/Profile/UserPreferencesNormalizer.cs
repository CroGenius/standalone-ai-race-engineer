namespace RaceEngineer.Core.Profile;

public static class UserPreferencesNormalizer
{
    public static DriverProfileRecord NormalizeDriver(DriverProfileRecord profile)
    {
        return profile with
        {
            DriverId = string.IsNullOrWhiteSpace(profile.DriverId) ? DriverProfileRecord.Default.DriverId : profile.DriverId.Trim(),
            DriverName = string.IsNullOrWhiteSpace(profile.DriverName) ? null : profile.DriverName.Trim(),
            PreferredLanguage = NormalizeLanguage(profile.PreferredLanguage),
            PreferredUnits = NormalizeChoice(profile.PreferredUnits, ["metric", "imperial"], DriverProfileRecord.Default.PreferredUnits),
            ExperienceLevel = NormalizeChoice(profile.ExperienceLevel, ["beginner", "intermediate", "advanced"], DriverProfileRecord.Default.ExperienceLevel),
            DrivingStyle = NormalizeChoice(profile.DrivingStyle, ["conservative", "balanced", "aggressive"], DriverProfileRecord.Default.DrivingStyle)
        };
    }

    public static CoachPreferencesRecord NormalizeCoach(CoachPreferencesRecord preferences)
    {
        return preferences with
        {
            ResponseLength = NormalizeChoice(preferences.ResponseLength, ["short", "normal", "detailed"], CoachPreferencesRecord.Default.ResponseLength),
            CalloutAggressiveness = NormalizeChoice(preferences.CalloutAggressiveness, ["low", "normal", "high"], CoachPreferencesRecord.Default.CalloutAggressiveness),
            SpeechRecognitionProvider = NormalizeChoice(preferences.SpeechRecognitionProvider, ["auto", "windows", "whisper"], CoachPreferencesRecord.Default.SpeechRecognitionProvider),
            PushToTalkHotkey = string.IsNullOrWhiteSpace(preferences.PushToTalkHotkey)
                ? CoachPreferencesRecord.Default.PushToTalkHotkey
                : preferences.PushToTalkHotkey.Trim(),
            VoiceInputCooldownSeconds = preferences.VoiceInputCooldownSeconds is < 0 or > 120
                ? CoachPreferencesRecord.Default.VoiceInputCooldownSeconds
                : preferences.VoiceInputCooldownSeconds,
            SpeechRecognitionCulture = preferences.SpeechRecognitionCulture?.Trim() ?? "",
            WhisperLanguageMode = Voice.WhisperLanguageModeResolver.Normalize(preferences.WhisperLanguageMode),
            CoachResponseLanguage = NormalizeLanguage(preferences.CoachResponseLanguage)
        };
    }

    public static StrategyPreferencesRecord NormalizeStrategy(StrategyPreferencesRecord preferences)
    {
        return preferences with
        {
            FuelSafetyMarginLaps = preferences.FuelSafetyMarginLaps is < 0 or > 5
                ? StrategyPreferencesRecord.Default.FuelSafetyMarginLaps
                : preferences.FuelSafetyMarginLaps,
            PitRecommendationAggressiveness = NormalizeChoice(
                preferences.PitRecommendationAggressiveness,
                ["conservative", "normal", "aggressive"],
                StrategyPreferencesRecord.Default.PitRecommendationAggressiveness),
            TyreRiskSensitivity = NormalizeChoice(
                preferences.TyreRiskSensitivity,
                ["low", "normal", "high"],
                StrategyPreferencesRecord.Default.TyreRiskSensitivity),
            PitStrategyPreference = NormalizeChoice(
                preferences.PitStrategyPreference,
                ["undercut", "balanced", "overcut"],
                StrategyPreferencesRecord.Default.PitStrategyPreference)
        };
    }

    public static string NormalizeLanguage(string? configuredLanguage)
    {
        if (string.IsNullOrWhiteSpace(configuredLanguage) || configuredLanguage.Equals("auto", StringComparison.OrdinalIgnoreCase))
        {
            return "auto";
        }

        if (configuredLanguage.Equals("hr-HR", StringComparison.OrdinalIgnoreCase)
            || configuredLanguage.Equals("hr", StringComparison.OrdinalIgnoreCase)
            || configuredLanguage.Equals("croatian", StringComparison.OrdinalIgnoreCase))
        {
            return "hr-HR";
        }

        if (configuredLanguage.Equals("en-US", StringComparison.OrdinalIgnoreCase)
            || configuredLanguage.Equals("en", StringComparison.OrdinalIgnoreCase)
            || configuredLanguage.Equals("english", StringComparison.OrdinalIgnoreCase))
        {
            return "en-US";
        }

        return "auto";
    }

    private static string NormalizeChoice(string? value, IReadOnlyList<string> allowed, string fallback)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return fallback;
        }

        var normalized = value.Trim().ToLowerInvariant();
        return allowed.FirstOrDefault(item => item.Equals(normalized, StringComparison.OrdinalIgnoreCase)) ?? fallback;
    }
}
