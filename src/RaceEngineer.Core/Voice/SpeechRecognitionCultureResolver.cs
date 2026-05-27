using System.Globalization;

namespace RaceEngineer.Core.Voice;

public sealed record SpeechRecognitionCultureResolution(
    CultureInfo Culture,
    string Detail,
    string? WarningMessage = null);

public static class SpeechRecognitionCultureResolver
{
    public static SpeechRecognitionCultureResolution Resolve(string? configuredCulture)
    {
        if (string.IsNullOrWhiteSpace(configuredCulture)
            || configuredCulture.Equals("auto", StringComparison.OrdinalIgnoreCase))
        {
            var culture = ResolveWindowsSpeechCulture();
            return new SpeechRecognitionCultureResolution(
                culture,
                $"Using Windows speech culture {culture.Name}.");
        }

        try
        {
            var culture = CultureInfo.GetCultureInfo(configuredCulture.Trim());
            if (!IsKnownCulture(culture))
            {
                throw new CultureNotFoundException(configuredCulture);
            }

            return new SpeechRecognitionCultureResolution(
                culture,
                $"Using configured speech culture {culture.Name}.");
        }
        catch (Exception exception)
        {
            var fallback = ResolveWindowsSpeechCulture();
            return new SpeechRecognitionCultureResolution(
                fallback,
                $"Using fallback speech culture {fallback.Name}.",
                $"Speech recognition culture '{configuredCulture}' is unavailable ({exception.Message}). Falling back to {fallback.Name}.");
        }
    }

    private static CultureInfo ResolveWindowsSpeechCulture()
    {
        var uiCulture = CultureInfo.CurrentUICulture;
        if (!string.IsNullOrWhiteSpace(uiCulture.Name))
        {
            return uiCulture;
        }

        return CultureInfo.CurrentCulture;
    }

    private static bool IsKnownCulture(CultureInfo culture)
    {
        return CultureInfo.GetCultures(CultureTypes.AllCultures)
            .Any(item => item.Name.Equals(culture.Name, StringComparison.OrdinalIgnoreCase));
    }
}
