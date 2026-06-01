namespace RaceEngineer.Core.Telemetry.Iracing;

public sealed class IracingSdkTelemetrySession : IIracingTelemetrySession
{
    // TODO: Replace this stub with a live iRacing SDK session adapter (e.g. IRSDKSharper).
    // IracingTelemetryProvider polls TryReadLatest once the SDK dependency is wired here.

    public IracingSessionConnectionState ConnectionState { get; private set; } = IracingSessionConnectionState.SdkUnavailable;

    public string DiagnosticMessage =>
        IracingProviderDiagnostics.DescribeConnection(ConnectionState);

    public Task<bool> TryConnectAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ConnectionState = IracingSessionConnectionState.SdkUnavailable;
        return Task.FromResult(false);
    }

    public void Disconnect()
    {
        ConnectionState = IracingSessionConnectionState.Disconnected;
    }

    public bool TryReadLatest(out IracingTelemetryFrame? frame)
    {
        frame = null;
        return false;
    }
}
