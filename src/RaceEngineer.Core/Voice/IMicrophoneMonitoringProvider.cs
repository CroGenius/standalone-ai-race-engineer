namespace RaceEngineer.Core.Voice;

public interface IMicrophoneMonitoringProvider
{
    bool IsMonitoring { get; }
    event EventHandler<MicDiagnosticsSnapshot>? DiagnosticsUpdated;
    void StartMonitoring();
    void StopMonitoring();
}
