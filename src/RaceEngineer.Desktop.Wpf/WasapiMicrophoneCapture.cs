using NAudio.CoreAudioApi;
using NAudio.Wave;
using RaceEngineer.Core.Voice;

namespace RaceEngineer.Desktop.Wpf;

internal static class WasapiAudioSubtypes
{
    public static readonly Guid IeeeFloat = new("00000003-0000-0010-8000-00AA00389B71");
    public static readonly Guid Pcm = new("00000001-0000-0010-8000-00AA00389B71");
}

public sealed class WasapiMicrophoneCapture : IDisposable
{
    private WasapiCapture? capture;
    private CapturedAudioFormat? captureFormat;
    private bool isRecording;

    public event EventHandler<ConvertedPcmChunk>? PcmChunkAvailable;
    public event EventHandler<CapturedAudioFormat>? CaptureFormatDetected;
    public event EventHandler<SpeechRecognitionDiagnosticEventArgs>? DiagnosticRaised;

    public bool IsRecording => isRecording;
    public CapturedAudioFormat? CaptureFormat => captureFormat;

    public void Start(int deviceNumber)
    {
        if (isRecording)
        {
            return;
        }

        var device = MicrophoneDeviceResolver.Resolve(deviceNumber);
        capture = new WasapiCapture(device);
        captureFormat = DescribeFormat(capture.WaveFormat, device.FriendlyName);
        RaiseDiagnostic(
            "Capture format",
            "Microphone capture format detected.",
            captureFormat.Summary);
        CaptureFormatDetected?.Invoke(this, captureFormat);

        capture.DataAvailable += OnDataAvailable;
        capture.RecordingStopped += OnRecordingStopped;
        capture.StartRecording();
        isRecording = true;
    }

    public void Stop()
    {
        if (!isRecording || capture is null)
        {
            return;
        }

        capture.StopRecording();
    }

    public void Dispose()
    {
        if (capture is null)
        {
            return;
        }

        capture.DataAvailable -= OnDataAvailable;
        capture.RecordingStopped -= OnRecordingStopped;
        if (isRecording)
        {
            try
            {
                capture.StopRecording();
            }
            catch (InvalidOperationException)
            {
            }
        }

        capture.Dispose();
        capture = null;
        isRecording = false;
    }

    private void OnDataAvailable(object? sender, WaveInEventArgs e)
    {
        if (captureFormat is null || e.BytesRecorded <= 0)
        {
            return;
        }

        var chunk = MicrophonePcmConverter.ConvertToWhisperPcm16(
            e.Buffer,
            0,
            e.BytesRecorded,
            captureFormat);
        if (chunk.Pcm16.Length == 0)
        {
            return;
        }

        PcmChunkAvailable?.Invoke(this, chunk);
    }

    private void OnRecordingStopped(object? sender, StoppedEventArgs e)
    {
        isRecording = false;
        if (e.Exception is not null)
        {
            RaiseDiagnostic("Exception", "Microphone capture stopped with an error.", e.Exception.Message);
        }
    }

    private static CapturedAudioFormat DescribeFormat(WaveFormat waveFormat, string deviceName)
    {
        var encoding = ResolveEncoding(waveFormat);
        return new CapturedAudioFormat(
            waveFormat.SampleRate,
            waveFormat.BitsPerSample,
            waveFormat.Channels,
            encoding,
            deviceName,
            waveFormat.BlockAlign,
            waveFormat.Encoding.ToString());
    }

    private static CapturedAudioEncoding ResolveEncoding(WaveFormat waveFormat)
    {
        if (waveFormat.Encoding == WaveFormatEncoding.IeeeFloat)
        {
            return CapturedAudioEncoding.IeeeFloat;
        }

        if (waveFormat.Encoding == WaveFormatEncoding.Pcm)
        {
            return CapturedAudioEncoding.Pcm;
        }

        if (waveFormat is WaveFormatExtensible extensible)
        {
            if (extensible.SubFormat == WasapiAudioSubtypes.IeeeFloat)
            {
                return CapturedAudioEncoding.IeeeFloat;
            }

            if (extensible.SubFormat == WasapiAudioSubtypes.Pcm)
            {
                return CapturedAudioEncoding.Pcm;
            }
        }

        return MicrophonePcmConverter.InferEncoding(
            waveFormat.BitsPerSample,
            waveFormat.Channels,
            waveFormat.BlockAlign);
    }

    private void RaiseDiagnostic(string stage, string message, string? detail = null)
    {
        DiagnosticRaised?.Invoke(this, new SpeechRecognitionDiagnosticEventArgs(stage, message, detail));
    }
}

public static class MicrophoneDeviceResolver
{
    public static MMDevice Resolve(int deviceNumber)
    {
        using var enumerator = new MMDeviceEnumerator();
        if (deviceNumber < 0)
        {
            return enumerator.GetDefaultAudioEndpoint(DataFlow.Capture, Role.Communications);
        }

        var devices = enumerator.EnumerateAudioEndPoints(DataFlow.Capture, DeviceState.Active).ToList();
        if (deviceNumber >= devices.Count)
        {
            return enumerator.GetDefaultAudioEndpoint(DataFlow.Capture, Role.Communications);
        }

        return devices[deviceNumber];
    }

    public static string ResolveDisplayName(int deviceNumber)
    {
        try
        {
            using var device = Resolve(deviceNumber);
            return deviceNumber < 0
                ? $"System default ({device.FriendlyName})"
                : device.FriendlyName;
        }
        catch
        {
            return deviceNumber < 0 ? "System default" : $"Device #{deviceNumber}";
        }
    }
}

public static class MicrophoneCalibrationRunner
{
    public static Task<MicrophoneCalibrationResult> RunAsync(
        int deviceNumber,
        TimeSpan duration,
        CancellationToken cancellationToken = default)
    {
        return Task.Run(() => Run(deviceNumber, duration, cancellationToken), cancellationToken);
    }

    private static MicrophoneCalibrationResult Run(
        int deviceNumber,
        TimeSpan duration,
        CancellationToken cancellationToken)
    {
        using var capture = new WasapiMicrophoneCapture();
        var pcmChunks = new List<byte>();
        CapturedAudioFormat? format = null;
        var peakRms = 0f;
        var rawPeakRms = 0f;
        var convertedPeakRms = 0f;
        var clipping = false;
        var chunkCount = 0;
        double rmsSum = 0d;

        capture.CaptureFormatDetected += (_, detectedFormat) => format = detectedFormat;
        capture.PcmChunkAvailable += (_, chunk) =>
        {
            pcmChunks.AddRange(chunk.Pcm16);
            peakRms = Math.Max(peakRms, chunk.ConvertedPeakRms);
            rawPeakRms = Math.Max(rawPeakRms, chunk.RawPeakRms);
            convertedPeakRms = Math.Max(convertedPeakRms, chunk.ConvertedPeakRms);
            rmsSum += chunk.CurrentRms;
            chunkCount++;
            clipping |= chunk.Clipping;
        };

        capture.Start(deviceNumber);
        Thread.Sleep(duration);
        cancellationToken.ThrowIfCancellationRequested();
        capture.Stop();
        Thread.Sleep(150);

        var averageRms = chunkCount > 0 ? (float)(rmsSum / chunkCount) : 0f;
        var speechDetected = peakRms >= 120f;
        var quality = MicSignalQualityClassifier.Classify(peakRms, speechDetected, clipping);
        var message = quality switch
        {
            MicSignalQuality.Good => "Microphone signal looks good for radio input.",
            MicSignalQuality.Weak => "Microphone signal is weak. Move closer or raise Windows input level.",
            MicSignalQuality.Bad => "Microphone signal is too quiet. Check device selection and Windows input level.",
            _ => "Microphone signal could not be classified."
        };

        if (clipping)
        {
            message = "Microphone signal is clipping. Lower Windows input gain.";
            quality = MicSignalQuality.Weak;
        }

        return new MicrophoneCalibrationResult(
            peakRms,
            averageRms,
            clipping,
            quality,
            MicSignalQualityClassifier.ToLabel(quality),
            message,
            format?.Summary ?? "Unknown capture format",
            rawPeakRms,
            convertedPeakRms);
    }
}
