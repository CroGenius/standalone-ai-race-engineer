namespace RaceEngineer.Core.Telemetry;

public sealed class SimHubTelemetryProvider : ITelemetryProvider
{
    private readonly TelemetryReceiver receiver;
    private TelemetryProviderStatus status = TelemetryProviderStatus.Offline;
    private string? lastError;

    public SimHubTelemetryProvider(string host = "127.0.0.1", int port = 20999)
    {
        receiver = new TelemetryReceiver(host, port);
        receiver.SnapshotReceived += OnSnapshotReceived;
        receiver.PacketProcessed += (_, packet) => PacketProcessed?.Invoke(this, packet);
    }

    public string ProviderId => Capabilities.ProviderId;
    public string DisplayName => Capabilities.ProviderDisplayName;
    public TelemetryProviderCapabilities Capabilities { get; } = TelemetryProviderCapabilities.SimHub;
    public TelemetryProviderStatus Status => status;
    public TelemetrySnapshot? LatestSnapshot { get; private set; }
    public string BindAddress => receiver.BindAddress;
    public int Port => receiver.Port;
    public string Diagnostics =>
        lastError is not null
            ? lastError
            : status == TelemetryProviderStatus.Running
                ? $"Listening on {BindAddress}:{Port}."
                : $"SimHub UDP provider offline on {BindAddress}:{Port}.";

    public event EventHandler<TelemetrySnapshot>? SnapshotReceived;
    public event EventHandler<TelemetryPacketResult>? PacketProcessed;

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        if (status == TelemetryProviderStatus.Running)
        {
            return;
        }

        try
        {
            await receiver.StartAsync(cancellationToken);
            status = TelemetryProviderStatus.Running;
            lastError = null;
        }
        catch (InvalidOperationException exception)
        {
            status = TelemetryProviderStatus.Error;
            lastError = exception.Message;
            throw;
        }
    }

    public Task StopAsync()
    {
        receiver.Stop();
        status = TelemetryProviderStatus.Offline;
        return Task.CompletedTask;
    }

    public ValueTask DisposeAsync() => receiver.DisposeAsync();

    private void OnSnapshotReceived(object? sender, TelemetrySnapshot snapshot)
    {
        LatestSnapshot = snapshot;
        SnapshotReceived?.Invoke(this, snapshot);
    }
}
