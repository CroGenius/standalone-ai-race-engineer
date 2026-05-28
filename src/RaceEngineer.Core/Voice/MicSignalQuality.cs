namespace RaceEngineer.Core.Voice;

public enum MicSignalQuality
{
    Unknown,
    Bad,
    Weak,
    Good
}

public static class MicSignalQualityClassifier
{
    public static MicSignalQuality Classify(float peakRms, bool speechDetected, bool clipping)
    {
        if (clipping)
        {
            return MicSignalQuality.Weak;
        }

        if (peakRms >= 1200f || (speechDetected && peakRms >= 400f))
        {
            return MicSignalQuality.Good;
        }

        if (peakRms >= 120f || speechDetected)
        {
            return MicSignalQuality.Weak;
        }

        return MicSignalQuality.Bad;
    }

    public static string ToLabel(MicSignalQuality quality) =>
        quality switch
        {
            MicSignalQuality.Good => "good",
            MicSignalQuality.Weak => "weak",
            MicSignalQuality.Bad => "bad",
            _ => "unknown"
        };
}

public sealed record MicDiagnosticsSnapshot(
    float CurrentRms,
    float PeakRms,
    float RawPeakRms,
    float ConvertedPeakRms,
    float WhisperInputRms,
    bool SpeechDetected,
    bool Clipping,
    string MicrophoneDeviceName,
    MicSignalQuality SignalQuality)
{
    public string SignalQualityLabel => MicSignalQualityClassifier.ToLabel(SignalQuality);
}

public sealed record SpeechCaptureMetrics(
    float CurrentRms,
    float PeakRms,
    float NormalizedRms,
    float AppliedGain,
    bool SpeechDetected,
    bool Clipping,
    string MicrophoneDeviceName,
    MicSignalQuality SignalQuality,
    float RawPeakRms = 0f,
    float ConvertedPeakRms = 0f);
