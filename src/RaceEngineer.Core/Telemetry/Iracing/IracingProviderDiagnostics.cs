namespace RaceEngineer.Core.Telemetry.Iracing;

public static class IracingProviderDiagnostics
{
    public const string SdkExtensionPoint =
        "TODO: Wire IracingSdkTelemetrySession to the iRacing SDK package when a buildable dependency is added.";

    public const string SdkUnavailable =
        "iRacing SDK is not connected. Add the iRacing SDK package and connect to a live iRacing session.";

    public const string SessionNotActive =
        "iRacing is not running or no session is active.";

    public const string Connected =
        "Connected to iRacing session.";

    public static string DescribeConnection(IracingSessionConnectionState state, string? detail = null) =>
        state switch
        {
            IracingSessionConnectionState.Connected => Connected,
            IracingSessionConnectionState.SessionNotActive => SessionNotActive,
            IracingSessionConnectionState.SdkUnavailable => SdkUnavailable,
            _ => string.IsNullOrWhiteSpace(detail)
                ? "iRacing telemetry provider is offline."
                : detail
        };

    public static string StartupWarning(IracingSessionConnectionState state) =>
        state switch
        {
            IracingSessionConnectionState.SdkUnavailable =>
                "iRacing telemetry provider selected, but the iRacing SDK is not available yet.",
            IracingSessionConnectionState.SessionNotActive =>
                "iRacing telemetry provider selected, but no active iRacing session was found.",
            _ => string.Empty
        };
}
