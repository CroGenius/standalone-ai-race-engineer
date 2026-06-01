using NAudio.CoreAudioApi;
using RaceEngineer.Core;
using RaceEngineer.Core.Voice;

namespace RaceEngineer.Desktop.Wpf;

internal static class NaudioVoiceServices
{
    public static VoiceInputService CreateVoiceInputService(
        AppSettings settings,
        VoiceInputOptions options,
        Action<string> reportWarning)
    {
        var speechProvider = SpeechRecognitionProviderFactory.CreateOrFallback(settings, reportWarning);
        return VoiceInputStartup.CreateService(speechProvider, options, enabled: false);
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
}
