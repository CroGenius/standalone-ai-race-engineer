namespace RaceEngineer.Core.Telemetry;

public interface ITelemetryProvider : IAsyncDisposable
{
    string ProviderId { get; }
    string DisplayName { get; }
    TelemetryProviderCapabilities Capabilities { get; }
    TelemetryProviderStatus Status { get; }
    TelemetrySnapshot? LatestSnapshot { get; }
    string Diagnostics { get; }

    event EventHandler<TelemetrySnapshot>? SnapshotReceived;
    event EventHandler<TelemetryPacketResult>? PacketProcessed;

    Task StartAsync(CancellationToken cancellationToken = default);
    Task StopAsync();
}
