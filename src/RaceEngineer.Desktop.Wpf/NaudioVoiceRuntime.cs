using RaceEngineer.Core;
using RaceEngineer.Core.Voice;

namespace RaceEngineer.Desktop.Wpf;

internal static class NaudioVoiceRuntime
{
    public const string BlockedWasapiMessage = "Voice input unavailable: Windows blocked NAudio.Wasapi.dll";

    public static bool TryCreateVoiceInputService(
        AppSettings settings,
        VoiceInputOptions options,
        Action<string> reportWarning,
        out VoiceInputService? service,
        out VoiceInitializationReport? report,
        out string? errorMessage)
    {
        service = null;
        report = null;
        errorMessage = null;

        try
        {
            (service, report) = NaudioVoiceServices.CreateVoiceInputServiceWithDiagnostics(settings, options, reportWarning);
            if (report.ProviderAvailable)
            {
                return true;
            }

            errorMessage = report.SummaryForChat;
            return false;
        }
        catch (Exception exception)
        {
            errorMessage = DescribeVoiceFailure(exception);
            service = VoiceInputStartup.CreateUnavailableService(errorMessage, options);
            report = new VoiceInitializationReport(
                SelectedProvider: settings.SpeechRecognitionProvider,
                NaudioAvailable: false,
                NaudioError: errorMessage,
                MicrophoneDeviceCount: 0,
                MicrophoneSummary: "unavailable",
                ActiveProviderName: "Speech Recognition",
                ProviderAvailable: false,
                ProviderAvailabilityDetail: errorMessage,
                FailedComponent: "NAudio runtime",
                InitializationException: exception.Message);
            return false;
        }
    }

    public static bool TryListMicrophoneDevices(
        out IReadOnlyList<MicrophoneDeviceOption> devices,
        out string? errorMessage)
    {
        devices = [];
        errorMessage = null;

        try
        {
            devices = NaudioVoiceServices.ListMicrophoneDevices();
            return true;
        }
        catch (Exception exception)
        {
            errorMessage = DescribeVoiceFailure(exception);
            return false;
        }
    }

    public static async Task<(MicrophoneCalibrationResult? Result, string? ErrorMessage)> TryRunMicCalibrationAsync(
        int deviceNumber,
        TimeSpan duration,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await NaudioVoiceServices.RunMicCalibrationAsync(deviceNumber, duration, cancellationToken);
            return (result, null);
        }
        catch (Exception exception)
        {
            return (null, DescribeVoiceFailure(exception));
        }
    }

    public static string DescribeVoiceFailure(Exception exception)
    {
        foreach (var current in EnumerateExceptions(exception))
        {
            if (ContainsWasapiBlock(current.Message))
            {
                return BlockedWasapiMessage;
            }
        }

        return $"Voice input unavailable: {exception.Message}";
    }

    private static bool ContainsWasapiBlock(string? text) =>
        !string.IsNullOrWhiteSpace(text) &&
        (text.Contains("NAudio.Wasapi", StringComparison.OrdinalIgnoreCase) ||
         text.Contains("Application Control policy", StringComparison.OrdinalIgnoreCase));

    private static IEnumerable<Exception> EnumerateExceptions(Exception exception)
    {
        for (var current = exception; current is not null; current = current.InnerException)
        {
            yield return current;
        }
    }
}
