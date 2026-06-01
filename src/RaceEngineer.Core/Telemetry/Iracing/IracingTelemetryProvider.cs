namespace RaceEngineer.Core.Telemetry.Iracing;

public sealed class IracingTelemetryProvider : ITelemetryProvider
{
    private readonly IIracingTelemetrySession session;
    private readonly object sync = new();
    private CancellationTokenSource? pollCancellation;
    private Task? pollTask;
    private TelemetryProviderStatus status = TelemetryProviderStatus.Offline;
    private string diagnostics = IracingProviderDiagnostics.SdkUnavailable;

    public IracingTelemetryProvider(IIracingTelemetrySession? session = null)
    {
        this.session = session ?? IracingTelemetrySessionFactory.CreateDefault();
    }

    public string ProviderId => Capabilities.ProviderId;
    public string DisplayName => Capabilities.ProviderDisplayName;
    public TelemetryProviderCapabilities Capabilities { get; } = TelemetryProviderCapabilities.Iracing;
    public TelemetryProviderStatus Status
    {
        get
        {
            lock (sync)
            {
                return status;
            }
        }
    }

    public TelemetrySnapshot? LatestSnapshot { get; private set; }
    public string Diagnostics
    {
        get
        {
            lock (sync)
            {
                return diagnostics;
            }
        }
    }

    public event EventHandler<TelemetrySnapshot>? SnapshotReceived;
    public event EventHandler<TelemetryPacketResult>? PacketProcessed;

    public static IReadOnlyList<string> CreateStartupWarnings(IIracingTelemetrySession session)
    {
        var warning = IracingProviderDiagnostics.StartupWarning(session.ConnectionState);
        return string.IsNullOrWhiteSpace(warning) ? [] : [warning];
    }

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        if (Status == TelemetryProviderStatus.Running)
        {
            return;
        }

        await StopPollingAsync();

        var connected = await session.TryConnectAsync(cancellationToken);
        if (!connected)
        {
            SetState(
                session.ConnectionState == IracingSessionConnectionState.SessionNotActive
                    ? TelemetryProviderStatus.Offline
                    : TelemetryProviderStatus.Error,
                session.DiagnosticMessage);
            return;
        }

        SetState(TelemetryProviderStatus.Running, session.DiagnosticMessage);
        StartPolling();
    }

    public async Task StopAsync()
    {
        await StopPollingAsync();
        session.Disconnect();
        SetState(TelemetryProviderStatus.Offline, IracingProviderDiagnostics.DescribeConnection(IracingSessionConnectionState.Disconnected));
    }

    public async ValueTask DisposeAsync()
    {
        await StopAsync();
    }

    internal void PublishSnapshotForTests(IracingTelemetryFrame frame)
    {
        var snapshot = IracingTelemetryMapper.Map(frame);
        LatestSnapshot = snapshot;
        SnapshotReceived?.Invoke(this, snapshot);
        PacketProcessed?.Invoke(
            this,
            new TelemetryPacketResult(DateTimeOffset.UtcNow, true, snapshot, null, null, "iracing.telemetry", 1));
    }

    private void StartPolling()
    {
        pollCancellation = new CancellationTokenSource();
        var token = pollCancellation.Token;
        pollTask = Task.Run(async () =>
        {
            while (!token.IsCancellationRequested)
            {
                if (session.TryReadLatest(out var frame) && frame is not null)
                {
                    var snapshot = IracingTelemetryMapper.Map(frame);
                    LatestSnapshot = snapshot;
                    SnapshotReceived?.Invoke(this, snapshot);
                    PacketProcessed?.Invoke(
                        this,
                        new TelemetryPacketResult(DateTimeOffset.UtcNow, true, snapshot, null, null, "iracing.telemetry", 1));
                }

                try
                {
                    await Task.Delay(100, token);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }, token);
    }

    private async Task StopPollingAsync()
    {
        if (pollCancellation is null)
        {
            return;
        }

        pollCancellation.Cancel();
        if (pollTask is not null)
        {
            try
            {
                await pollTask;
            }
            catch (OperationCanceledException)
            {
            }
        }

        pollCancellation.Dispose();
        pollCancellation = null;
        pollTask = null;
    }

    private void SetState(TelemetryProviderStatus nextStatus, string nextDiagnostics)
    {
        lock (sync)
        {
            status = nextStatus;
            diagnostics = nextDiagnostics;
        }
    }
}
