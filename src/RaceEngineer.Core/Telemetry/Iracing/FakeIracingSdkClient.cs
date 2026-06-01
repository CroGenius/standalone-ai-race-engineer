using IRSDKSharper;

namespace RaceEngineer.Core.Telemetry.Iracing;

public sealed class FakeIracingSdkClient : IIracingSdkClient
{
    public bool IsStarted { get; private set; }

    public bool IsConnected { get; set; }

    public IRacingSdkData? Data { get; set; }

    public Exception? StartException { get; set; }

    public void Start()
    {
        if (StartException is not null)
        {
            throw StartException;
        }

        IsStarted = true;
    }

    public void Stop()
    {
        IsStarted = false;
        IsConnected = false;
        Data = null;
    }

    public void Dispose()
    {
        Stop();
    }
}
