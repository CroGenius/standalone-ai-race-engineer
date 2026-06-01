using IRSDKSharper;

namespace RaceEngineer.Core.Telemetry.Iracing;

public interface IIracingSdkClient : IDisposable
{
    bool IsStarted { get; }
    bool IsConnected { get; }
    IRacingSdkData? Data { get; }

    void Start();

    void Stop();
}

public sealed class IracingSdkClient : IIracingSdkClient
{
    private readonly IRacingSdk sdk;
    private bool disposed;

    public IracingSdkClient()
    {
        sdk = new IRacingSdk();
        sdk.UpdateInterval = 1;
    }

    public bool IsStarted => sdk.IsStarted;

    public bool IsConnected => sdk.IsConnected;

    public IRacingSdkData? Data => sdk.IsConnected ? sdk.Data : null;

    public void Start()
    {
        if (!sdk.IsStarted)
        {
            sdk.Start();
        }
    }

    public void Stop()
    {
        if (sdk.IsStarted)
        {
            sdk.Stop();
        }
    }

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        Stop();
        disposed = true;
    }
}
