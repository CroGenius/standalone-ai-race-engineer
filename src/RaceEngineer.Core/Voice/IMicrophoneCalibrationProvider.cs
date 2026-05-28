namespace RaceEngineer.Core.Voice;

public interface IMicrophoneCalibrationProvider
{
    Task<MicrophoneCalibrationResult> RunCalibrationAsync(
        TimeSpan duration,
        CancellationToken cancellationToken = default);
}
