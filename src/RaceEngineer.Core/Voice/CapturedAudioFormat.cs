namespace RaceEngineer.Core.Voice;

public enum CapturedAudioEncoding
{
    Pcm,
    IeeeFloat,
    Unknown
}

public sealed record CapturedAudioFormat(
    int SampleRate,
    int BitsPerSample,
    int Channels,
    CapturedAudioEncoding Encoding,
    string DeviceName,
    int BlockAlign = 0,
    string WaveEncodingName = "")
{
    public string Summary =>
        $"{SampleRate} Hz, {BitsPerSample}-bit, {Channels} ch, {Encoding}, block={EffectiveBlockAlign}, device={DeviceName}";
    public int EffectiveBlockAlign => BlockAlign > 0 ? BlockAlign : BitsPerSample / 8 * Math.Max(1, Channels);
}

public sealed record ConvertedPcmChunk(
    byte[] Pcm16,
    float RawPeakRms,
    float ConvertedPeakRms,
    float CurrentRms,
    bool Clipping)
{
    public float PeakRms => ConvertedPeakRms;
}

public sealed record MicrophoneCalibrationResult(
    float PeakRms,
    float AverageRms,
    bool Clipping,
    MicSignalQuality SignalQuality,
    string SignalQualityLabel,
    string AssessmentMessage,
    string CaptureFormatSummary,
    float RawPeakRms = 0f,
    float ConvertedPeakRms = 0f);
