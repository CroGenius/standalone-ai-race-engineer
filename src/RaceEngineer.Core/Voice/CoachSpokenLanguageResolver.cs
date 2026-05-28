using System.Globalization;
using System.Text.RegularExpressions;
using RaceEngineer.Core.Coaching;
using RaceEngineer.Core.Profile;

namespace RaceEngineer.Core.Voice;

public static class CoachSpokenLanguageResolver
{
    public static bool UseEnglishSpokenOutput(CoachPreferencesRecord? preferences)
    {
        var language = NormalizeCoachResponseLanguage(preferences?.CoachResponseLanguage);
        return !language.Equals("hr-HR", StringComparison.OrdinalIgnoreCase);
    }

    public static string NormalizeCoachResponseLanguage(string? configuredLanguage)
    {
        return UserPreferencesNormalizer.NormalizeLanguage(configuredLanguage);
    }

    public static string ToSpokenLanguage(
        string summary,
        CoachMessage message,
        CoachQueryTopic topic,
        CoachPreferencesRecord? preferences)
    {
        if (!UseEnglishSpokenOutput(preferences) || !LooksNonEnglish(summary))
        {
            return summary;
        }

        return SpokenSummaryGenerator.BuildEnglishTopicSummary(message, topic) ?? summary;
    }

    private static bool LooksNonEnglish(string text)
    {
        return text.Contains("gubitak", StringComparison.OrdinalIgnoreCase)
            || text.Contains("goriv", StringComparison.OrdinalIgnoreCase)
            || text.Contains("koč", StringComparison.OrdinalIgnoreCase)
            || text.Contains("koc", StringComparison.OrdinalIgnoreCase)
            || text.Contains("gum", StringComparison.OrdinalIgnoreCase)
            || text.Contains("Najve", StringComparison.OrdinalIgnoreCase)
            || text.Contains("Trenutno", StringComparison.OrdinalIgnoreCase)
            || text.Contains("Nemam", StringComparison.OrdinalIgnoreCase)
            || text.Contains("pouzdan", StringComparison.OrdinalIgnoreCase);
    }
}
