using NAudio.CoreAudioApi;

namespace RaceEngineer.Desktop.Wpf;

public sealed class MicrophoneDeviceOption
{
    public int DeviceNumber { get; init; }
    public string DisplayName { get; init; } = "";
}

public static class MicrophoneDeviceCatalog
{
    public static IReadOnlyList<MicrophoneDeviceOption> ListDevices()
    {
        var devices = new List<MicrophoneDeviceOption>
        {
            new() { DeviceNumber = -1, DisplayName = "System default (Communications)" }
        };

        try
        {
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
        }
        catch
        {
            // Fall back to default only if WASAPI enumeration fails.
        }

        return devices;
    }

    public static string ResolveDisplayName(int deviceNumber) =>
        MicrophoneDeviceResolver.ResolveDisplayName(deviceNumber);
}
