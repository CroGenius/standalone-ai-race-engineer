namespace RaceEngineer.Core.Telemetry.Iracing;

public sealed class IracingOfflineTelemetrySession : IIracingTelemetrySession
{
    private readonly string diagnosticDetail;

    public IracingOfflineTelemetrySession(string? diagnosticDetail = null)
    {
        this.diagnosticDetail = diagnosticDetail ?? IracingProviderDiagnostics.SdkUnavailable;
        ConnectionState = IracingSessionConnectionState.SdkUnavailable;
        ConnectionDiagnostics = IracingConnectionDiagnostics.SdkUnavailable(this.diagnosticDetail);
    }

    public IracingSessionConnectionState ConnectionState { get; private set; }

    public IracingConnectionDiagnostics ConnectionDiagnostics { get; private set; }

    public string DiagnosticMessage => ConnectionDiagnostics.Summary;

    public Task<bool> TryConnectAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ConnectionState = IracingSessionConnectionState.SdkUnavailable;
        ConnectionDiagnostics = IracingConnectionDiagnostics.SdkUnavailable(diagnosticDetail);
        return Task.FromResult(false);
    }

    public void Disconnect()
    {
        ConnectionState = IracingSessionConnectionState.Disconnected;
        ConnectionDiagnostics = IracingConnectionDiagnostics.Disconnected();
    }

    public bool TryReadLatest(out IracingTelemetryFrame? frame)
    {
        frame = null;
        return false;
    }
}
