using System.Globalization;
using System.IO;
using System.Text;
using NAudio.Wave;
using RaceEngineer.Core.Voice;
using Whisper.net;

namespace RaceEngineer.Desktop.Wpf;

public sealed class WhisperSpeechRecognitionProvider : ISpeechRecognitionProvider
{
    private const int SampleRate = 16_000;
    private const int BytesPerSample = 2;
    private const int MinimumAudioBytes = SampleRate * BytesPerSample / 4;
    private const float SpeechDetectionRmsThreshold = 350f;
    private const float MinimumLanguageDetectionConfidence = 0.25f;

    private readonly string modelPath;
    private readonly WhisperSpeechOptions options;
    private readonly object sync = new();
    private readonly List<SpeechRecognitionDiagnosticEventArgs> initializationDiagnostics = [];
    private bool initializationDiagnosticsEmitted;

    private WhisperFactory? factory;
    private WaveInEvent? waveIn;
    private MemoryStream? pcmBuffer;
    private CancellationTokenSource? trailingCaptureCts;
    private bool isRecording;
    private bool transcriptionPending;
    private bool stopScheduled;
    private bool speechDetected;
    private float capturePeakRms;

    private WhisperSpeechRecognitionProvider(string modelPath, WhisperSpeechOptions options)
    {
        this.modelPath = modelPath;
        this.options = options;
        AvailabilityDetail =
            $"Whisper model {modelPath}. Language mode {options.LanguageMode}. Trailing capture {options.TrailingAudioMilliseconds} ms.";
        initializationDiagnostics.Add(new SpeechRecognitionDiagnosticEventArgs(
            "Recognizer initialized",
            "Whisper provider configured.",
            $"Model={Path.GetFileName(modelPath)} LanguageMode={options.LanguageMode} NoSpeechThreshold={options.NoSpeechThreshold:0.00}"));
    }

    public string ProviderName => "Whisper (local)";
    public bool IsAvailable { get; private init; } = true;
    public bool IsListening => isRecording || transcriptionPending;
    public string AvailabilityDetail { get; }

    public event EventHandler<SpeechRecognizedResult>? SpeechRecognized;
    public event EventHandler<SpeechRecognitionStatusChangedEventArgs>? StatusChanged;
    public event EventHandler<SpeechRecognitionDiagnosticEventArgs>? DiagnosticRaised;

    public static ISpeechRecognitionProvider TryCreate(string modelPath, WhisperSpeechOptions? speechOptions = null)
    {
        if (string.IsNullOrWhiteSpace(modelPath) || !File.Exists(modelPath))
        {
            return new UnavailableSpeechRecognitionProvider($"Whisper model not found at {modelPath}.");
        }

        return new WhisperSpeechRecognitionProvider(modelPath, speechOptions ?? WhisperSpeechOptions.Default);
    }

    public void StartListening()
    {
        EmitPendingInitializationDiagnostics();

        lock (sync)
        {
            if (isRecording || transcriptionPending)
            {
                RaiseDiagnostic("Recognition started", "Whisper recording already active.");
                return;
            }

            CancelTrailingCapture();

            try
            {
                EnsureFactoryLoaded();
            }
            catch (Exception exception)
            {
                RaiseDiagnostic("Recognition failed", "Whisper model failed to load.", exception.Message);
                StatusChanged?.Invoke(this, new SpeechRecognitionStatusChangedEventArgs("Error", exception.Message));
                return;
            }

            pcmBuffer = new MemoryStream();
            speechDetected = false;
            capturePeakRms = 0f;
            stopScheduled = false;

            waveIn = new WaveInEvent
            {
                WaveFormat = new WaveFormat(SampleRate, 16, 1),
                BufferMilliseconds = 20
            };
            waveIn.DataAvailable += OnDataAvailable;
            waveIn.RecordingStopped += OnRecordingStopped;

            try
            {
                waveIn.StartRecording();
                isRecording = true;
                RaiseDiagnostic(
                    "Recognition started",
                    "Whisper microphone recording started.",
                    $"Format=mono/{SampleRate}Hz/16-bit PromptLength={options.Prompt.Length}");
                StatusChanged?.Invoke(this, new SpeechRecognitionStatusChangedEventArgs(
                    "Listening",
                    $"Whisper microphone active ({options.LanguageMode})."));
            }
            catch (Exception exception)
            {
                CleanupRecordingResources();
                RaiseDiagnostic("Exception", "Whisper microphone failed to start.", exception.Message);
                StatusChanged?.Invoke(this, new SpeechRecognitionStatusChangedEventArgs("Error", exception.Message));
            }
        }
    }

    public void StopListening()
    {
        lock (sync)
        {
            if (!isRecording || waveIn is null || stopScheduled)
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
                    if (token.IsCancellationRequested || waveIn is null || !isRecording)
                    {
                        return;
                    }

                    waveIn.StopRecording();
                }
            }
            catch (TaskCanceledException)
            {
            }
            catch (Exception exception)
            {
                lock (sync)
                {
                    CleanupRecordingResources();
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
            CleanupRecordingResources();
            factory?.Dispose();
            factory = null;
        }
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

    private void OnDataAvailable(object? sender, WaveInEventArgs e)
    {
        if (e.BytesRecorded <= 0 || pcmBuffer is null)
        {
            return;
        }

        pcmBuffer.Write(e.Buffer, 0, e.BytesRecorded);
        var chunkRms = WhisperPcmAudio.ComputeRms16(e.Buffer, e.BytesRecorded);
        capturePeakRms = Math.Max(capturePeakRms, chunkRms);

        if (!speechDetected && chunkRms >= SpeechDetectionRmsThreshold)
        {
            speechDetected = true;
            RaiseDiagnostic(
                "Speech detected",
                "Audio captured for Whisper transcription.",
                $"ChunkRms={chunkRms:0}");
        }
    }

    private void OnRecordingStopped(object? sender, StoppedEventArgs e)
    {
        byte[] pcm;
        lock (sync)
        {
            isRecording = false;
            stopScheduled = false;
            CancelTrailingCapture();
            pcm = pcmBuffer?.ToArray() ?? [];
            CleanupRecordingResources();
            transcriptionPending = true;
        }

        if (e.Exception is not null)
        {
            transcriptionPending = false;
            RaiseDiagnostic("Exception", "Whisper recording stopped with an error.", e.Exception.Message);
            StatusChanged?.Invoke(this, new SpeechRecognitionStatusChangedEventArgs("Error", e.Exception.Message));
            return;
        }

        _ = Task.Run(() => TranscribeAsync(pcm));
    }

    private async Task TranscribeAsync(byte[] pcm)
    {
        var startedAt = Environment.TickCount64;
        try
        {
            var durationSeconds = pcm.Length / (double)(SampleRate * BytesPerSample);
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
            RaiseDiagnostic(
                "Audio captured",
                "Whisper input prepared.",
                $"Duration={durationSeconds:0.00}s PeakRms={audioMetrics.PeakRms:0} NormalizedRms={audioMetrics.NormalizedRms:0} Gain={audioMetrics.AppliedGain:0.00}x SpeechDetected={speechDetected}");

            var samples = WhisperPcmAudio.Pcm16ToFloat(audioMetrics.Pcm);
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

                if (segment.NoSpeechProbability > options.NoSpeechThreshold && segment.Probability < 0.35f)
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

            RaiseDiagnostic("Recognition completed", "Whisper transcription completed.");

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

            if (highestNoSpeech > options.NoSpeechThreshold && confidence < 0.35f)
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
            SpeechRecognized?.Invoke(this, new SpeechRecognizedResult(finalText, confidence));
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
        }
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

    private void CleanupRecordingResources()
    {
        if (waveIn is not null)
        {
            waveIn.DataAvailable -= OnDataAvailable;
            waveIn.RecordingStopped -= OnRecordingStopped;
            waveIn.Dispose();
            waveIn = null;
        }

        pcmBuffer?.Dispose();
        pcmBuffer = null;
        isRecording = false;
    }

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
    private const float MaxGain = 10f;
    private const float MinGain = 0.25f;

    public static float ComputeRms16(byte[] buffer, int bytesRecorded)
    {
        if (bytesRecorded <= 0)
        {
            return 0f;
        }

        double sumSquares = 0d;
        var sampleCount = bytesRecorded / 2;
        for (var index = 0; index < sampleCount; index++)
        {
            var sample = BitConverter.ToInt16(buffer, index * 2);
            sumSquares += sample * sample;
        }

        return (float)Math.Sqrt(sumSquares / sampleCount);
    }

    public static WhisperAudioMetrics NormalizePcm16(byte[] pcm)
    {
        if (pcm.Length < 2)
        {
            return new WhisperAudioMetrics(pcm, 0f, 0f, 1f);
        }

        var peakRms = ComputeRms16(pcm, pcm.Length);
        var gain = peakRms <= 1f ? MaxGain : Math.Clamp(TargetRms / peakRms, MinGain, MaxGain);
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

        var normalizedRms = ComputeRms16(normalized, normalized.Length);
        return new WhisperAudioMetrics(normalized, peakRms, normalizedRms, gain);
    }

    public static float[] Pcm16ToFloat(byte[] pcm)
    {
        var samples = new float[pcm.Length / 2];
        for (var index = 0; index < samples.Length; index++)
        {
            samples[index] = BitConverter.ToInt16(pcm, index * 2) / 32768f;
        }

        return samples;
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
