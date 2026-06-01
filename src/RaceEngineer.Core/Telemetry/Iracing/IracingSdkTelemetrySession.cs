namespace RaceEngineer.Core.Telemetry.Iracing;

public sealed class IracingSdkTelemetrySession : IIracingTelemetrySession, IDisposable
{
    private readonly IIracingSdkClient sdkClient;
    private readonly TimeSpan connectTimeout;
    private readonly object sync = new();
    private IracingTelemetryFrame? latestFrame;
    private IracingConnectionDiagnostics connectionDiagnostics = IracingConnectionDiagnostics.Disconnected();
    private IracingSessionConnectionState connectionState = IracingSessionConnectionState.Disconnected;
    private bool started;

    public IracingSdkTelemetrySession(IIracingSdkClient? sdkClient = null, TimeSpan? connectTimeout = null)
    {
        this.sdkClient = sdkClient ?? new IracingSdkClient();
        this.connectTimeout = connectTimeout ?? TimeSpan.FromSeconds(5);
    }

    public IracingSessionConnectionState ConnectionState
    {
        get
        {
            lock (sync)
            {
                return connectionState;
            }
        }
    }

    public IracingConnectionDiagnostics ConnectionDiagnostics
    {
        get
        {
            lock (sync)
            {
                return connectionDiagnostics;
            }
        }
    }

    public string DiagnosticMessage => ConnectionDiagnostics.Summary;

    public async Task<bool> TryConnectAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            sdkClient.Start();
            started = true;
        }
        catch (Exception exception)
        {
            SetState(
                IracingSessionConnectionState.SdkUnavailable,
                IracingConnectionDiagnostics.SdkUnavailable(exception.Message));
            return false;
        }

        var deadline = DateTime.UtcNow + connectTimeout;
        while (DateTime.UtcNow < deadline)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (sdkClient.IsConnected && sdkClient.Data is not null)
            {
                var diagnostics = IracingSdkFrameReader.BuildDiagnostics(sdkClient.Data, sdkConnected: true);
                SetState(IracingSessionConnectionState.Connected, diagnostics);
                CaptureLatestFrame();
                return true;
            }

            await Task.Delay(100, cancellationToken);
        }

        SetState(
            IracingSessionConnectionState.SessionNotActive,
            IracingConnectionDiagnostics.Disconnected(IracingProviderDiagnostics.SessionNotActive));
        return false;
    }

    public void Disconnect()
    {
        if (started)
        {
            sdkClient.Stop();
            started = false;
        }

        latestFrame = null;
        SetState(
            IracingSessionConnectionState.Disconnected,
            IracingConnectionDiagnostics.Disconnected());
    }

    public bool TryReadLatest(out IracingTelemetryFrame? frame)
    {
        lock (sync)
        {
            if (!sdkClient.IsConnected || sdkClient.Data is null)
            {
                if (started && !sdkClient.IsConnected)
                {
                    connectionState = IracingSessionConnectionState.SessionNotActive;
                    connectionDiagnostics = IracingConnectionDiagnostics.Disconnected(IracingProviderDiagnostics.SessionNotActive);
                }

                frame = null;
                return false;
            }

            CaptureLatestFrameUnsafe();
            frame = latestFrame;
            return frame is not null;
        }
    }

    public void Dispose()
    {
        Disconnect();
        sdkClient.Dispose();
    }

    private void CaptureLatestFrame()
    {
        lock (sync)
        {
            CaptureLatestFrameUnsafe();
        }
    }

    private void CaptureLatestFrameUnsafe()
    {
        if (sdkClient.Data is null)
        {
            latestFrame = null;
            return;
        }

        connectionDiagnostics = IracingSdkFrameReader.BuildDiagnostics(sdkClient.Data, sdkConnected: sdkClient.IsConnected);
        latestFrame = IracingSdkFrameReader.TryRead(sdkClient.Data);
    }

    private void SetState(IracingSessionConnectionState state, IracingConnectionDiagnostics diagnostics)
    {
        lock (sync)
        {
            connectionState = state;
            connectionDiagnostics = diagnostics;
        }
    }
}
