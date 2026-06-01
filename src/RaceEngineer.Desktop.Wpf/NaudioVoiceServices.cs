using NAudio.CoreAudioApi;
using RaceEngineer.Core;
using RaceEngineer.Core.Voice;

namespace RaceEngineer.Desktop.Wpf;

internal static class NaudioVoiceServices
{
    public static (VoiceInputService Service, VoiceInitializationReport Report) CreateVoiceInputServiceWithDiagnostics(
        AppSettings settings,
        VoiceInputOptions options,
        Action<string> reportWarning)
    {
        var report = ProbeVoiceInitialization(settings, reportWarning, out var speechProvider);
        var service = VoiceInputStartup.CreateService(speechProvider, options, enabled: false);
        return (service, report);
    }

    public static VoiceInitializationReport ProbeVoiceInitialization(
        AppSettings settings,
        Action<string> reportWarning,
        out ISpeechRecognitionProvider speechProvider)
    {
        var selection = SpeechRecognitionProviderSelection.Resolve(settings.SpeechRecognitionProvider);
        var selectedProvider = selection.Kind.ToString();
        if (selection.WarningMessage is not null)
        {
            reportWarning(selection.WarningMessage);
        }

        var naudioAvailable = true;
        string? naudioError = null;
        var microphoneDeviceCount = 0;
        var microphoneSummary = "not probed";
        try
        {
            var devices = ListMicrophoneDevices();
            microphoneDeviceCount = devices.Count;
            microphoneSummary = devices.Count == 0
                ? "none detected"
                : string.Join("; ", devices.Select(device => device.DisplayName).Take(4));
        }
        catch (Exception exception)
        {
            naudioAvailable = false;
            naudioError = NaudioVoiceRuntime.DescribeVoiceFailure(exception);
            microphoneSummary = "unavailable";
        }

        var failedComponent = "none";
        string? initializationException = null;
        var activeProviderName = "not initialized";
        var providerAvailable = false;
        var providerAvailabilityDetail = "Speech provider not initialized yet.";

        try
        {
            speechProvider = SpeechRecognitionProviderFactory.CreateOrFallback(settings, reportWarning);
            var activeProvider = speechProvider is LazySpeechRecognitionProvider lazy
                ? lazy.EnsureInitialized()
                : speechProvider;

            activeProviderName = activeProvider.ProviderName;
            providerAvailable = activeProvider.IsAvailable;
            providerAvailabilityDetail = activeProvider.AvailabilityDetail;

            if (!providerAvailable)
            {
                failedComponent = ResolveFailedComponent(activeProvider, providerAvailabilityDetail);
                initializationException = providerAvailabilityDetail;
            }
            else if (!naudioAvailable && activeProviderName.Contains("Whisper", StringComparison.OrdinalIgnoreCase))
            {
                failedComponent = "NAudio runtime";
                initializationException = naudioError;
                providerAvailable = false;
            }
        }
        catch (Exception exception)
        {
            failedComponent = "SpeechRecognitionProviderFactory";
            initializationException = NaudioVoiceRuntime.DescribeVoiceFailure(exception);
            speechProvider = new UnavailableSpeechRecognitionProvider(initializationException);
            activeProviderName = speechProvider.ProviderName;
            providerAvailabilityDetail = speechProvider.AvailabilityDetail;
        }

        return new VoiceInitializationReport(
            selectedProvider,
            naudioAvailable,
            naudioError,
            microphoneDeviceCount,
            microphoneSummary,
            activeProviderName,
            providerAvailable,
            providerAvailabilityDetail,
            failedComponent,
            initializationException);
    }

    public static IReadOnlyList<MicrophoneDeviceOption> ListMicrophoneDevices()
    {
        var devices = new List<MicrophoneDeviceOption>
        {
            new() { DeviceNumber = -1, DisplayName = "System default (Communications)" }
        };

        using var enumerator = new MMDeviceEnumerator();
        var endpoints = enumerator.EnumerateAudioEndPoints(DataFlow.Capture, DeviceState.Active);
        for (var index = 0; index < endpoints.Count; index++)
        {
            devices.Add(new MicrophoneDeviceOption
            {
                DeviceNumber = index,
                DisplayName = endpoints[index].FriendlyName
            });
        }

        return devices;
    }

    public static Task<MicrophoneCalibrationResult> RunMicCalibrationAsync(
        int deviceNumber,
        TimeSpan duration,
        CancellationToken cancellationToken = default) =>
        MicrophoneCalibrationRunner.RunAsync(deviceNumber, duration, cancellationToken);

    private static string ResolveFailedComponent(ISpeechRecognitionProvider provider, string detail)
    {
        if (provider is UnavailableSpeechRecognitionProvider)
        {
            if (detail.Contains("Whisper", StringComparison.OrdinalIgnoreCase) ||
                detail.Contains("model not found", StringComparison.OrdinalIgnoreCase))
            {
                return "Whisper provider";
            }

            if (detail.Contains("Windows", StringComparison.OrdinalIgnoreCase) ||
                detail.Contains("recognizer", StringComparison.OrdinalIgnoreCase))
            {
                return "Windows Speech provider";
            }

            if (detail.Contains("NAudio", StringComparison.OrdinalIgnoreCase) ||
                detail.Contains("Application Control policy", StringComparison.OrdinalIgnoreCase))
            {
                return "NAudio runtime";
            }

            return "LazySpeechRecognitionProvider";
        }

        return provider.ProviderName;
    }
}
