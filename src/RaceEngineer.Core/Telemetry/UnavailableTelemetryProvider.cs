namespace RaceEngineer.Core.Telemetry;

public sealed class UnavailableTelemetryProvider : ITelemetryProvider
{
    private readonly string diagnostics;

    public UnavailableTelemetryProvider(
        string providerId,
        string displayName,
        TelemetryProviderCapabilities capabilities,
        string diagnostics)
    {
        ProviderId = providerId;
        DisplayName = displayName;
        Capabilities = capabilities;
        this.diagnostics = diagnostics;
    }

    public string ProviderId { get; }
    public string DisplayName { get; }
    public TelemetryProviderCapabilities Capabilities { get; }
    public TelemetryProviderStatus Status => TelemetryProviderStatus.Error;
    public TelemetrySnapshot? LatestSnapshot => null;
    public string Diagnostics => diagnostics;

    public event EventHandler<TelemetrySnapshot>? SnapshotReceived;
    public event EventHandler<TelemetryPacketResult>? PacketProcessed;

    public static UnavailableTelemetryProvider NotImplemented(string providerId, string displayName) =>
        new(
            providerId,
            displayName,
            providerId.Equals("iracing", StringComparison.OrdinalIgnoreCase)
                ? TelemetryProviderCapabilities.IracingPlanned
                : new TelemetryProviderCapabilities(
                    providerId,
                    displayName,
                    false,
                    false,
                    false,
                    false,
                    false,
                    false,
                    false,
                    false,
                    false,
                    false,
                    false,
                    false),
            $"{displayName} telemetry provider is not implemented yet.");

    public Task StartAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task StopAsync() => Task.CompletedTask;

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
