namespace RaceEngineer.Core.Telemetry.Iracing;

public static class IracingProviderDiagnostics
{
    public const string SdkExtensionPoint =
        "Wire IracingSdkTelemetrySession through IRSDKSharper shared-memory telemetry (irsdkEnableMem=1).";

    public const string SdkUnavailable =
        "iRacing SDK is not connected. Ensure iRacing is installed, irsdkEnableMem=1 is set in app.ini, and the simulator is running.";

    public const string SessionNotActive =
        "iRacing is not running or no session is active.";

    public const string ConnectedPrefix =
        "Connected to iRacing session";

    public static string DescribeConnection(IracingSessionConnectionState state, IracingConnectionDiagnostics? diagnostics = null) =>
        diagnostics?.Summary ?? state switch
        {
            IracingSessionConnectionState.Connected => ConnectedPrefix + ".",
            IracingSessionConnectionState.SessionNotActive => SessionNotActive,
            IracingSessionConnectionState.SdkUnavailable => SdkUnavailable,
            _ => "iRacing telemetry provider is offline."
        };

    public static string StartupWarning(IracingSessionConnectionState state) =>
        state switch
        {
            IracingSessionConnectionState.SdkUnavailable =>
                "iRacing telemetry provider selected, but the iRacing SDK could not be started.",
            IracingSessionConnectionState.SessionNotActive =>
                "iRacing telemetry provider selected, but no active iRacing session was found.",
            _ => string.Empty
        };

    [Obsolete("Use DescribeConnection with IracingConnectionDiagnostics.")]
    public static string DescribeConnection(IracingSessionConnectionState state, string? detail) =>
        string.IsNullOrWhiteSpace(detail)
            ? DescribeConnection(state)
            : detail;
}
