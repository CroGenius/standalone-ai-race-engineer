namespace RaceEngineer.Core.Voice;

public enum SpeechRecognitionProviderKind
{
    Auto,
    Windows,
    Whisper
}

public sealed record SpeechRecognitionProviderSelectionResult(
    SpeechRecognitionProviderKind Kind,
    string? WarningMessage = null);

public static class SpeechRecognitionProviderSelection
{
    public static SpeechRecognitionProviderKind Parse(string? configuredProvider)
    {
        if (string.IsNullOrWhiteSpace(configuredProvider)
            || configuredProvider.Equals("auto", StringComparison.OrdinalIgnoreCase))
        {
            return SpeechRecognitionProviderKind.Auto;
        }

        if (configuredProvider.Equals("windows", StringComparison.OrdinalIgnoreCase))
        {
            return SpeechRecognitionProviderKind.Windows;
        }

        if (configuredProvider.Equals("whisper", StringComparison.OrdinalIgnoreCase))
        {
            return SpeechRecognitionProviderKind.Whisper;
        }

        return SpeechRecognitionProviderKind.Auto;
    }

    public static SpeechRecognitionProviderSelectionResult Resolve(string? configuredProvider)
    {
        if (string.IsNullOrWhiteSpace(configuredProvider)
            || configuredProvider.Equals("auto", StringComparison.OrdinalIgnoreCase))
        {
            return new SpeechRecognitionProviderSelectionResult(SpeechRecognitionProviderKind.Auto);
        }

        if (configuredProvider.Equals("windows", StringComparison.OrdinalIgnoreCase))
        {
            return new SpeechRecognitionProviderSelectionResult(SpeechRecognitionProviderKind.Windows);
        }

        if (configuredProvider.Equals("whisper", StringComparison.OrdinalIgnoreCase))
        {
            return new SpeechRecognitionProviderSelectionResult(SpeechRecognitionProviderKind.Whisper);
        }

        return new SpeechRecognitionProviderSelectionResult(
            SpeechRecognitionProviderKind.Auto,
            $"Unknown speechRecognitionProvider '{configuredProvider}'. Using auto.");
    }
}
