using System.Globalization;
using System.IO;
using System.Text;
using RaceEngineer.Core.Voice;
using Whisper.net;

namespace RaceEngineer.Desktop.Wpf;

public sealed class WhisperSpeechRecognitionProvider
    : ISpeechRecognitionProvider, IMicrophoneMonitoringProvider, IMicrophoneCalibrationProvider
{
    private const int SampleRate = MicrophonePcmConverter.TargetSampleRate;
    private const int BytesPerSample = 2;
    private const int MinimumAudioBytes = SampleRate * BytesPerSample / 4;
    private const float MinimumLanguageDetectionConfidence = 0.25f;
    private const float SegmentConfidenceThreshold = 0.45f;

    private readonly string modelPath;
    private readonly WhisperSpeechOptions options;
    private readonly object sync = new();
    private readonly List<SpeechRecognitionDiagnosticEventArgs> initializationDiagnostics = [];
    private readonly int ringBufferCapacityBytes;
    private bool initializationDiagnosticsEmitted;

    private WhisperFactory? factory;
    private WasapiMicrophoneCapture? captureSession;
    private CapturedAudioFormat? activeCaptureFormat;
    private MemoryStream? pcmBuffer;
    private byte[] ringBuffer = [];
    private int ringWritePosition;
    private int ringFilledBytes;
    private CancellationTokenSource? trailingCaptureCts;
    private bool monitoringActive;
    private bool capturingForPtt;
    private bool transcriptionPending;
    private bool stopScheduled;
    private bool speechDetected;
    private bool clippingDetected;
    private float capturePeakRms;
    private float captureRawPeakRms;
    private float captureConvertedPeakRms;
    private float lastWhisperInputRms;
    private float currentRms;
    private string microphoneDeviceName = "Unknown";

    private WhisperSpeechRecognitionProvider(string modelPath, WhisperSpeechOptions options)
    {
        this.modelPath = modelPath;
        this.options = options;
        ringBufferCapacityBytes = BytesForMilliseconds(options.PreRollAudioMilliseconds);
        ringBuffer = new byte[ringBufferCapacityBytes];
        microphoneDeviceName = MicrophoneDeviceResolver.ResolveDisplayName(options.MicrophoneDeviceNumber);
        AvailabilityDetail =
            $"Whisper model {modelPath}. Language mode {options.LanguageMode}. Mic {microphoneDeviceName}. Trailing {options.TrailingAudioMilliseconds} ms, pre-roll {options.PreRollAudioMilliseconds} ms.";
        initializationDiagnostics.Add(new SpeechRecognitionDiagnosticEventArgs(
            "Recognizer initialized",
            "Whisper provider configured.",
            $"Model={Path.GetFileName(modelPath)} LanguageMode={options.LanguageMode} NoSpeechThreshold={options.NoSpeechThreshold:0.00} Device={microphoneDeviceName}"));
    }

    public string ProviderName => "Whisper (local)";
    public bool IsAvailable { get; private init; } = true;
    public bool IsListening => capturingForPtt || transcriptionPending;
    public bool IsMonitoring => monitoringActive;
    public string AvailabilityDetail { get; }

    public event EventHandler<SpeechRecognizedResult>? SpeechRecognized;
    public event EventHandler<SpeechRecognitionStatusChangedEventArgs>? StatusChanged;
    public event EventHandler<SpeechRecognitionDiagnosticEventArgs>? DiagnosticRaised;
    public event EventHandler<MicDiagnosticsSnapshot>? DiagnosticsUpdated;

    public static ISpeechRecognitionProvider TryCreate(string modelPath, WhisperSpeechOptions? speechOptions = null)
    {
        if (string.IsNullOrWhiteSpace(modelPath) || !File.Exists(modelPath))
        {
            return new UnavailableSpeechRecognitionProvider($"Whisper model not found at {modelPath}.");
        }

        return new WhisperSpeechRecognitionProvider(modelPath, speechOptions ?? WhisperSpeechOptions.Default);
    }

    public Task<MicrophoneCalibrationResult> RunCalibrationAsync(
        TimeSpan duration,
        CancellationToken cancellationToken = default) =>
        MicrophoneCalibrationRunner.RunAsync(options.MicrophoneDeviceNumber, duration, cancellationToken);

    public void StartMonitoring()
    {
        lock (sync)
        {
            if (monitoringActive)
            {
                return;
            }

            try
            {
                EnsureMicrophoneOpen();
                monitoringActive = true;
                RaiseDiagnostic("Mic monitoring", "Microphone monitoring started.", activeCaptureFormat?.Summary ?? microphoneDeviceName);
                PublishDiagnostics();
            }
            catch (Exception exception)
            {
                RaiseDiagnostic("Exception", "Microphone monitoring failed to start.", exception.Message);
            }
        }
    }

    public void StopMonitoring()
    {
        lock (sync)
        {
            monitoringActive = false;
            if (!capturingForPtt && !transcriptionPending)
            {
                CloseMicrophone();
            }

            RaiseDiagnostic("Mic monitoring", "Microphone monitoring stopped.");
        }
    }

    public void StartListening()
    {
        EmitPendingInitializationDiagnostics();

        lock (sync)
        {
            if (capturingForPtt || transcriptionPending)
            {
                RaiseDiagnostic("Recognition started", "Whisper recording already active.");
                return;
            }

            CancelTrailingCapture();

            try
            {
                EnsureFactoryLoaded();
                EnsureMicrophoneOpen();
            }
            catch (Exception exception)
            {
                RaiseDiagnostic("Recognition failed", "Whisper model or microphone failed to start.", exception.Message);
                StatusChanged?.Invoke(this, new SpeechRecognitionStatusChangedEventArgs("Error", exception.Message));
                return;
            }

            pcmBuffer = new MemoryStream();
            AppendPreRollToCapture();
            speechDetected = false;
            clippingDetected = false;
            capturePeakRms = 0f;
            captureRawPeakRms = 0f;
            captureConvertedPeakRms = 0f;
            currentRms = 0f;
            stopScheduled = false;
            capturingForPtt = true;
            monitoringActive = true;

            RaiseDiagnostic(
                "Recognition started",
                "Whisper microphone recording started.",
                $"Target=mono/{SampleRate}Hz/16-bit PreRollMs={options.PreRollAudioMilliseconds} Source={activeCaptureFormat?.Summary ?? microphoneDeviceName}");
            StatusChanged?.Invoke(this, new SpeechRecognitionStatusChangedEventArgs(
                "Listening",
                $"Whisper microphone active ({options.LanguageMode})."));
        }
    }

    public void StopListening()
    {
        lock (sync)
        {
            if (!capturingForPtt || stopScheduled)
            {
                return;
            }

            stopScheduled = true;
            RaiseDiagnostic(
                "PTT release",
                $"Capturing trailing audio ({options.TrailingAudioMilliseconds} ms) before transcription.",
                $"TrailingMs={options.TrailingAudioMilliseconds}");
        }

        trailingCaptureCts = new CancellationTokenSource();
        var token = trailingCaptureCts.Token;
        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(options.TrailingAudioMilliseconds, token);
                lock (sync)
                {
                    if (token.IsCancellationRequested || !capturingForPtt)
                    {
                        return;
                    }

                    FinishCaptureAndTranscribe();
                }
            }
            catch (TaskCanceledException)
            {
            }
            catch (Exception exception)
            {
                lock (sync)
                {
                    ResetCaptureState();
                }

                RaiseDiagnostic("Exception", "Whisper trailing capture failed.", exception.Message);
                StatusChanged?.Invoke(this, new SpeechRecognitionStatusChangedEventArgs("Error", exception.Message));
            }
        }, token);
    }

    public void Dispose()
    {
        lock (sync)
        {
            CancelTrailingCapture();
            ResetCaptureState();
            CloseMicrophone();
            factory?.Dispose();
            factory = null;
        }
    }

    private void EnsureMicrophoneOpen()
    {
        if (captureSession?.IsRecording == true)
        {
            return;
        }

        captureSession?.Dispose();
        captureSession = new WasapiMicrophoneCapture();
        captureSession.DiagnosticRaised += (_, diagnostic) => DiagnosticRaised?.Invoke(this, diagnostic);
        captureSession.CaptureFormatDetected += (_, format) =>
        {
            activeCaptureFormat = format;
            microphoneDeviceName = format.DeviceName;
            RaiseDiagnostic("Capture format", "Raw microphone format logged.", format.Summary);
        };
        captureSession.PcmChunkAvailable += OnPcmChunkAvailable;
        captureSession.Start(options.MicrophoneDeviceNumber);
    }

    private void CloseMicrophone()
    {
        if (captureSession is null)
        {
            return;
        }

        captureSession.PcmChunkAvailable -= OnPcmChunkAvailable;
        captureSession.Dispose();
        captureSession = null;
        activeCaptureFormat = null;
    }

    private void OnPcmChunkAvailable(object? sender, ConvertedPcmChunk chunk)
    {
        if (chunk.Pcm16.Length == 0)
        {
            return;
        }

        WriteRingBuffer(chunk.Pcm16);

        lock (sync)
        {
            if (capturingForPtt && pcmBuffer is not null)
            {
                pcmBuffer.Write(chunk.Pcm16, 0, chunk.Pcm16.Length);
            }
        }

        currentRms = chunk.CurrentRms;
        captureRawPeakRms = Math.Max(captureRawPeakRms, chunk.RawPeakRms);
        captureConvertedPeakRms = Math.Max(captureConvertedPeakRms, chunk.ConvertedPeakRms);
        capturePeakRms = Math.Max(capturePeakRms, chunk.ConvertedPeakRms);
        if (chunk.Clipping)
        {
            clippingDetected = true;
        }

        if (!speechDetected && chunk.ConvertedPeakRms >= options.SpeechDetectionRmsThreshold)
        {
            speechDetected = true;
            RaiseDiagnostic(
                "Speech detected",
                "Audio captured for Whisper transcription.",
                $"RawPeak={chunk.RawPeakRms:0} ConvertedPeak={chunk.ConvertedPeakRms:0} ChunkRms={chunk.CurrentRms:0}");
        }

        PublishDiagnostics();
    }

    private void FinishCaptureAndTranscribe()
    {
        capturingForPtt = false;
        stopScheduled = false;
        CancelTrailingCapture();
        var pcm = pcmBuffer?.ToArray() ?? [];
        pcmBuffer?.Dispose();
        pcmBuffer = null;
        transcriptionPending = true;

        if (!monitoringActive)
        {
            CloseMicrophone();
        }

        _ = Task.Run(() => TranscribeAsync(pcm));
    }

    private void ResetCaptureState()
    {
        CancelTrailingCapture();
        capturingForPtt = false;
        stopScheduled = false;
        transcriptionPending = false;
        pcmBuffer?.Dispose();
        pcmBuffer = null;
        if (!monitoringActive)
        {
            CloseMicrophone();
        }
    }

    private void AppendPreRollToCapture()
    {
        if (pcmBuffer is null || ringFilledBytes <= 0)
        {
            return;
        }

        if (ringFilledBytes >= ringBufferCapacityBytes)
        {
            var start = ringWritePosition;
            CopyRingSegment(start, ringBufferCapacityBytes - start, pcmBuffer);
            if (start > 0)
            {
                CopyRingSegment(0, start, pcmBuffer);
            }
        }
        else
        {
            CopyRingSegment(0, ringFilledBytes, pcmBuffer);
        }
    }

    private void CopyRingSegment(int ringOffset, int length, MemoryStream target)
    {
        if (length <= 0)
        {
            return;
        }

        target.Write(ringBuffer, ringOffset, length);
    }

    private void WriteRingBuffer(byte[] pcm16)
    {
        foreach (var value in pcm16)
        {
            ringBuffer[ringWritePosition] = value;
            ringWritePosition = (ringWritePosition + 1) % ringBufferCapacityBytes;
        }

        ringFilledBytes = Math.Min(ringBufferCapacityBytes, ringFilledBytes + pcm16.Length);
    }

    private void PublishDiagnostics()
    {
        DiagnosticsUpdated?.Invoke(
            this,
            new MicDiagnosticsSnapshot(
                currentRms,
                Math.Max(capturePeakRms, currentRms),
                captureRawPeakRms,
                Math.Max(captureConvertedPeakRms, currentRms),
                lastWhisperInputRms,
                speechDetected,
                clippingDetected,
                microphoneDeviceName,
                MicSignalQualityClassifier.Classify(Math.Max(captureConvertedPeakRms, currentRms), speechDetected, clippingDetected)));
    }

    private void EnsureFactoryLoaded()
    {
        if (factory is not null)
        {
            return;
        }

        factory = WhisperFactory.FromPath(modelPath);
        initializationDiagnostics.Add(new SpeechRecognitionDiagnosticEventArgs(
            "Recognizer initialized",
            "Whisper factory loaded.",
            Path.GetFileName(modelPath)));
    }

    private WhisperProcessor CreateProcessor(string languageCode)
    {
        if (factory is null)
        {
            throw new InvalidOperationException("Whisper factory is unavailable.");
        }

        var builder = factory.CreateBuilder()
            .WithLanguage(languageCode)
            .WithProbabilities()
            .WithNoSpeechThreshold(options.NoSpeechThreshold);

        if (!string.IsNullOrWhiteSpace(options.Prompt))
        {
            builder = builder.WithPrompt(options.Prompt);
        }

        return builder.Build();
    }

    private async Task TranscribeAsync(byte[] pcm)
    {
        var startedAt = Environment.TickCount64;
        try
        {
            var durationSeconds = pcm.Length / (double)(SampleRate * BytesPerSample);
            var captureMetrics = BuildCaptureMetrics(0f, 0f, 1f);
            if (pcm.Length < MinimumAudioBytes)
            {
                RaiseDiagnostic(
                    "Recognition rejected",
                    "Captured audio was too short for Whisper transcription.",
                    $"Duration={durationSeconds:0.00}s Bytes={pcm.Length}");
                StatusChanged?.Invoke(this, new SpeechRecognitionStatusChangedEventArgs(
                    "Rejected",
                    "Captured audio was too short."));
                return;
            }

            if (factory is null)
            {
                RaiseDiagnostic("Recognition failed", "Whisper factory is unavailable.");
                return;
            }

            var audioMetrics = WhisperPcmAudio.NormalizePcm16(pcm);
            lastWhisperInputRms = audioMetrics.NormalizedRms;
            captureMetrics = BuildCaptureMetrics(
                audioMetrics.PeakRms,
                audioMetrics.NormalizedRms,
                audioMetrics.AppliedGain);
            RaiseDiagnostic(
                "Audio captured",
                "Whisper input prepared.",
                $"Duration={durationSeconds:0.00}s RawPeak={captureRawPeakRms:0} ConvertedPeak={audioMetrics.PeakRms:0} WhisperRms={audioMetrics.NormalizedRms:0} Gain={audioMetrics.AppliedGain:0.00}x SpeechDetected={speechDetected} Clipping={clippingDetected} Source={activeCaptureFormat?.Summary ?? microphoneDeviceName}");

            if (audioMetrics.PeakRms < options.MinimumPeakRmsForTranscription)
            {
                RaiseDiagnostic(
                    "Recognition rejected",
                    "Microphone signal too weak for transcription.",
                    $"PeakRms={audioMetrics.PeakRms:0} Minimum={options.MinimumPeakRmsForTranscription:0}");
                StatusChanged?.Invoke(this, new SpeechRecognitionStatusChangedEventArgs(
                    "Rejected",
                    "Microphone signal too weak."));
                return;
            }

            if (!speechDetected && audioMetrics.NormalizedRms < 350f)
            {
                RaiseDiagnostic(
                    "Recognition rejected",
                    "No speech detected in microphone capture.",
                    $"PeakRms={audioMetrics.PeakRms:0} NormalizedRms={audioMetrics.NormalizedRms:0}");
                StatusChanged?.Invoke(this, new SpeechRecognitionStatusChangedEventArgs(
                    "Rejected",
                    "No speech detected."));
                return;
            }

            var samples = MicrophonePcmConverter.Pcm16ToFloat(audioMetrics.Pcm);
            var languageCode = ResolveTranscriptionLanguage(samples);
            RaiseDiagnostic(
                "Language detected",
                $"Whisper will transcribe as '{languageCode}'.",
                $"LanguageMode={options.LanguageMode}");

            RaiseDiagnostic("Recognition started", "Whisper transcription started.");
            using var processor = CreateProcessor(languageCode);
            using var wavStream = WhisperPcmAudio.CreateWavStream(audioMetrics.Pcm, SampleRate);

            var transcript = new StringBuilder();
            float lowestConfidence = 1f;
            float highestNoSpeech = 0f;
            string? detectedLanguage = null;
            var segmentCount = 0;

            await foreach (var segment in processor.ProcessAsync(wavStream))
            {
                segmentCount++;
                detectedLanguage ??= segment.Language;
                highestNoSpeech = Math.Max(highestNoSpeech, segment.NoSpeechProbability);
                lowestConfidence = Math.Min(lowestConfidence, segment.Probability);

                var text = segment.Text.Trim();
                if (text.Length == 0)
                {
                    continue;
                }

                if (segment.NoSpeechProbability > options.NoSpeechThreshold && segment.Probability < SegmentConfidenceThreshold)
                {
                    RaiseDiagnostic(
                        "Recognition rejected",
                        "Whisper segment looked like non-speech.",
                        $"Text='{text}' NoSpeech={segment.NoSpeechProbability:0.00} Confidence={segment.Probability:0.00}");
                    continue;
                }

                RaiseDiagnostic(
                    "Speech hypothesis",
                    text,
                    $"Confidence={segment.Probability:0.00} NoSpeech={segment.NoSpeechProbability:0.00} Language={segment.Language}");
                if (transcript.Length > 0 && !char.IsWhiteSpace(transcript[^1]))
                {
                    transcript.Append(' ');
                }

                transcript.Append(text);
            }

            var inferenceMs = Environment.TickCount64 - startedAt;
            var finalText = transcript.ToString().Trim();
            var confidence = segmentCount > 0 ? lowestConfidence : 0f;

            RaiseDiagnostic(
                "Whisper metrics",
                "Whisper inference completed.",
                $"InferenceMs={inferenceMs} Segments={segmentCount} Language={detectedLanguage ?? languageCode} NoSpeechMax={highestNoSpeech:0.00} Confidence={confidence:0.00} RawTranscript='{finalText}'");

            if (finalText.Length == 0)
            {
                RaiseDiagnostic(
                    "Recognition rejected",
                    "Whisper returned an empty transcript.",
                    $"NoSpeechMax={highestNoSpeech:0.00} Threshold={options.NoSpeechThreshold:0.00}");
                StatusChanged?.Invoke(this, new SpeechRecognitionStatusChangedEventArgs(
                    "Rejected",
                    "Whisper returned an empty transcript."));
                return;
            }

            if (highestNoSpeech > options.NoSpeechThreshold && confidence < SegmentConfidenceThreshold)
            {
                RaiseDiagnostic(
                    "Recognition rejected",
                    "Whisper classified the utterance as non-speech.",
                    $"NoSpeechMax={highestNoSpeech:0.00} Confidence={confidence:0.00}");
                StatusChanged?.Invoke(this, new SpeechRecognitionStatusChangedEventArgs(
                    "Rejected",
                    "Whisper classified the utterance as non-speech."));
                return;
            }

            RaiseDiagnostic(
                "Recognition completed",
                "Final speech recognized.",
                $"Text='{finalText}' Confidence={confidence:0.00} Language={detectedLanguage ?? languageCode} NoSpeechMax={highestNoSpeech:0.00} InferenceMs={inferenceMs}");
            SpeechRecognized?.Invoke(this, new SpeechRecognizedResult(finalText, confidence, captureMetrics));
            StatusChanged?.Invoke(this, new SpeechRecognitionStatusChangedEventArgs("Idle", "Whisper microphone stopped."));
        }
        catch (Exception exception)
        {
            RaiseDiagnostic("Exception", "Whisper transcription failed.", exception.Message);
            StatusChanged?.Invoke(this, new SpeechRecognitionStatusChangedEventArgs("Error", exception.Message));
        }
        finally
        {
            lock (sync)
            {
                transcriptionPending = false;
            }

            PublishDiagnostics();
        }
    }

    private SpeechCaptureMetrics BuildCaptureMetrics(float peakRms, float normalizedRms, float gain)
    {
        var peak = peakRms > 0 ? peakRms : captureConvertedPeakRms;
        return new SpeechCaptureMetrics(
            currentRms,
            peak,
            normalizedRms > 0 ? normalizedRms : peak,
            gain,
            speechDetected,
            clippingDetected,
            microphoneDeviceName,
            MicSignalQualityClassifier.Classify(peak, speechDetected, clippingDetected),
            captureRawPeakRms,
            captureConvertedPeakRms);
    }

    private string ResolveTranscriptionLanguage(ReadOnlySpan<float> samples)
    {
        var configured = WhisperLanguageModeResolver.ResolveWhisperLanguageCode(options.LanguageMode);
        if (configured is not null)
        {
            return configured;
        }

        using var detector = CreateProcessor("auto");
        var detection = detector.DetectLanguageWithProbability(samples);
        var detected = detection.language?.Trim().ToLowerInvariant();
        if ((detected == WhisperLanguageModeResolver.Croatian || detected == WhisperLanguageModeResolver.English)
            && detection.probability >= MinimumLanguageDetectionConfidence)
        {
            RaiseDiagnostic(
                "Language detected",
                $"Auto-detected language '{detected}'.",
                $"Probability={detection.probability:0.00}");
            return detected;
        }

        var fallback = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == WhisperLanguageModeResolver.Croatian
            ? WhisperLanguageModeResolver.Croatian
            : WhisperLanguageModeResolver.English;
        RaiseDiagnostic(
            "Language detected",
            $"Auto language uncertain; using fallback '{fallback}'.",
            $"Detected='{detected ?? "unknown"}' Probability={detection.probability:0.00}");
        return fallback;
    }

    private static int BytesForMilliseconds(int milliseconds) =>
        Math.Max(SampleRate * BytesPerSample * milliseconds / 1000, SampleRate * BytesPerSample / 10);

    private void CancelTrailingCapture()
    {
        trailingCaptureCts?.Cancel();
        trailingCaptureCts = null;
    }

    private void EmitPendingInitializationDiagnostics()
    {
        if (initializationDiagnosticsEmitted)
        {
            return;
        }

        initializationDiagnosticsEmitted = true;
        foreach (var diagnostic in initializationDiagnostics)
        {
            DiagnosticRaised?.Invoke(this, diagnostic);
        }
    }

    private void RaiseDiagnostic(string stage, string message, string? detail = null)
    {
        DiagnosticRaised?.Invoke(this, new SpeechRecognitionDiagnosticEventArgs(stage, message, detail));
    }
}

internal sealed record WhisperAudioMetrics(byte[] Pcm, float PeakRms, float NormalizedRms, float AppliedGain);

internal static class WhisperPcmAudio
{
    private const float TargetRms = 4500f;
    private const float MaxGain = 8f;
    private const float MinGain = 0.25f;

    public static WhisperAudioMetrics NormalizePcm16(byte[] pcm)
    {
        if (pcm.Length < 2)
        {
            return new WhisperAudioMetrics(pcm, 0f, 0f, 1f);
        }

        var peakRms = MicrophonePcmConverter.ComputeRms16(pcm);
        if (peakRms < 120f)
        {
            return new WhisperAudioMetrics(pcm, peakRms, peakRms, 1f);
        }

        var gain = Math.Clamp(TargetRms / peakRms, MinGain, MaxGain);
        if (Math.Abs(gain - 1f) < 0.05f)
        {
            return new WhisperAudioMetrics(pcm, peakRms, peakRms, 1f);
        }

        var normalized = new byte[pcm.Length];
        for (var index = 0; index < pcm.Length; index += 2)
        {
            var sample = BitConverter.ToInt16(pcm, index);
            var scaled = (int)Math.Round(sample * gain);
            scaled = Math.Clamp(scaled, short.MinValue, short.MaxValue);
            BitConverter.TryWriteBytes(normalized.AsSpan(index, 2), (short)scaled);
        }

        var normalizedRms = MicrophonePcmConverter.ComputeRms16(normalized);
        return new WhisperAudioMetrics(normalized, peakRms, normalizedRms, gain);
    }

    public static MemoryStream CreateWavStream(byte[] pcm, int sampleRate)
    {
        const short channels = 1;
        const short bitsPerSample = 16;
        var byteRate = sampleRate * channels * bitsPerSample / 8;
        var blockAlign = (short)(channels * bitsPerSample / 8);
        var stream = new MemoryStream(44 + pcm.Length);
        using var writer = new BinaryWriter(stream, Encoding.ASCII, leaveOpen: true);

        writer.Write(Encoding.ASCII.GetBytes("RIFF"));
        writer.Write(36 + pcm.Length);
        writer.Write(Encoding.ASCII.GetBytes("WAVE"));
        writer.Write(Encoding.ASCII.GetBytes("fmt "));
        writer.Write(16);
        writer.Write((short)1);
        writer.Write(channels);
        writer.Write(sampleRate);
        writer.Write(byteRate);
        writer.Write(blockAlign);
        writer.Write(bitsPerSample);
        writer.Write(Encoding.ASCII.GetBytes("data"));
        writer.Write(pcm.Length);
        writer.Write(pcm);
        stream.Position = 0;
        return stream;
    }
}
