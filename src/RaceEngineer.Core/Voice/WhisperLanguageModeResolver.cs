namespace RaceEngineer.Core.Voice;

public static class WhisperLanguageModeResolver
{
    public const string Auto = "auto";
    public const string Croatian = "hr";
    public const string English = "en";

    public static string Normalize(string? configuredMode)
    {
        if (string.IsNullOrWhiteSpace(configuredMode)
            || configuredMode.Equals(Auto, StringComparison.OrdinalIgnoreCase))
        {
            return Auto;
        }

        if (configuredMode.Equals(Croatian, StringComparison.OrdinalIgnoreCase)
            || configuredMode.Equals("hr-HR", StringComparison.OrdinalIgnoreCase)
            || configuredMode.Equals("croatian", StringComparison.OrdinalIgnoreCase))
        {
            return Croatian;
        }

        if (configuredMode.Equals(English, StringComparison.OrdinalIgnoreCase)
            || configuredMode.Equals("en-US", StringComparison.OrdinalIgnoreCase)
            || configuredMode.Equals("english", StringComparison.OrdinalIgnoreCase))
        {
            return English;
        }

        return Auto;
    }

    public static string? ResolveWhisperLanguageCode(string normalizedMode)
    {
        return normalizedMode switch
        {
            Croatian => Croatian,
            English => English,
            _ => null
        };
    }
}
