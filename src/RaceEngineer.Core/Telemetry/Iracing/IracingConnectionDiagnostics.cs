namespace RaceEngineer.Core.Telemetry.Iracing;

public sealed record IracingConnectionDiagnostics(
    bool SdkConnected,
    bool SessionActive,
    bool DriverDetected,
    bool TrackDetected,
    string? DriverName = null,
    string? TrackName = null,
    string? CarName = null,
    string? Detail = null)
{
    public static IracingConnectionDiagnostics Disconnected(string? detail = null) =>
        new(false, false, false, false, Detail: detail);

    public static IracingConnectionDiagnostics SdkUnavailable(string? detail = null) =>
        new(false, false, false, false, Detail: detail ?? IracingProviderDiagnostics.SdkUnavailable);

    public string Summary
    {
        get
        {
        if (!SdkConnected)
        {
            return Detail ?? "iRacing telemetry provider is offline.";
        }

            var parts = new List<string> { "SDK connected" };
            parts.Add(SessionActive ? "Session active" : "Session inactive");
            parts.Add(DriverDetected
                ? $"Driver detected{(string.IsNullOrWhiteSpace(DriverName) ? string.Empty : $": {DriverName}")}"
                : "Driver not detected");
            parts.Add(TrackDetected
                ? $"Track detected{(string.IsNullOrWhiteSpace(TrackName) ? string.Empty : $": {TrackName}")}"
                : "Track not detected");

            if (!string.IsNullOrWhiteSpace(CarName))
            {
                parts.Add($"Car: {CarName}");
            }

            if (!string.IsNullOrWhiteSpace(Detail))
            {
                parts.Add(Detail);
            }

            return string.Join("; ", parts);
        }
    }
}
