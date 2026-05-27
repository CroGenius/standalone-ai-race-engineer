using RaceEngineer.Core;
using RaceEngineer.Core.Voice;

namespace RaceEngineer.Desktop.Wpf;

public static class SpeechRecognitionProviderFactory
{
    public static ISpeechRecognitionProvider CreateOrFallback(AppSettings settings, Action<string> reportWarning)
    {
        var selection = SpeechRecognitionProviderSelection.Resolve(settings.SpeechRecognitionProvider);
        if (selection.WarningMessage is not null)
        {
            reportWarning(selection.WarningMessage);
        }

        var modelPath = WhisperModelLocator.ResolveModelPath(settings.WhisperModelPath);
        return new LazySpeechRecognitionProvider(() =>
            CreateActiveProvider(settings, reportWarning, selection.Kind, modelPath));
    }

    private static ISpeechRecognitionProvider CreateActiveProvider(
        AppSettings settings,
        Action<string> reportWarning,
        SpeechRecognitionProviderKind kind,
        string modelPath)
    {
        var tryWhisper = kind switch
        {
            SpeechRecognitionProviderKind.Whisper => true,
            SpeechRecognitionProviderKind.Auto => WhisperModelLocator.IsModelAvailable(settings.WhisperModelPath),
            _ => false
        };

        if (tryWhisper)
        {
            var whisper = WhisperSpeechRecognitionProvider.TryCreate(modelPath);
            if (whisper.IsAvailable)
            {
                return whisper;
            }

            if (kind == SpeechRecognitionProviderKind.Whisper)
            {
                reportWarning(
                    $"Whisper speech recognition unavailable ({whisper.AvailabilityDetail}). Falling back to Windows Speech.");
            }
            else
            {
                reportWarning(
                    $"Whisper auto-selection failed ({whisper.AvailabilityDetail}). Falling back to Windows Speech.");
            }
        }
        else if (kind == SpeechRecognitionProviderKind.Whisper)
        {
            reportWarning(
                $"Whisper model not found at {modelPath}. Falling back to Windows Speech.");
        }

        return WindowsSpeechRecognitionProvider.CreateEngineProvider(
            reportWarning,
            settings.SpeechRecognitionCulture);
    }
}
