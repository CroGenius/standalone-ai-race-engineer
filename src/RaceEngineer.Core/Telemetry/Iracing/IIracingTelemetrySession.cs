namespace RaceEngineer.Core.Telemetry.Iracing;

public interface IIracingTelemetrySession
{
    IracingSessionConnectionState ConnectionState { get; }
    IracingConnectionDiagnostics ConnectionDiagnostics { get; }
    string DiagnosticMessage { get; }

    Task<bool> TryConnectAsync(CancellationToken cancellationToken = default);

    void Disconnect();

    bool TryReadLatest(out IracingTelemetryFrame? frame);
}
