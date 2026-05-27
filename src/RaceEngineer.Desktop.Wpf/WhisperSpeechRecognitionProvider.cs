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
    private const int MinimumAudioBytes = SampleRate / 2;
    private const float DefaultConfidence = 0.85f;

    private readonly string modelPath;
    private readonly object sync = new();
    private readonly List<SpeechRecognitionDiagnosticEventArgs> initializationDiagnostics = [];
    private bool initializationDiagnosticsEmitted;

    private WhisperFactory? factory;
    private WhisperProcessor? processor;
    private WaveInEvent? waveIn;
    private MemoryStream? pcmBuffer;
    private bool isRecording;
    private bool transcriptionPending;
    private bool speechDetected;

    private WhisperSpeechRecognitionProvider(string modelPath)
    {
        this.modelPath = modelPath;
        AvailabilityDetail = $"Whisper model {modelPath}. Language auto-detect (Croatian/English).";
        initializationDiagnostics.Add(new SpeechRecognitionDiagnosticEventArgs(
            "Recognizer initialized",
            "Whisper provider configured.",
            Path.GetFileName(modelPath)));
    }

    public string ProviderName => "Whisper (local)";
    public bool IsAvailable { get; private init; } = true;
    public bool IsListening => isRecording || transcriptionPending;
    public string AvailabilityDetail { get; }

    public event EventHandler<SpeechRecognizedResult>? SpeechRecognized;
    public event EventHandler<SpeechRecognitionStatusChangedEventArgs>? StatusChanged;
    public event EventHandler<SpeechRecognitionDiagnosticEventArgs>? DiagnosticRaised;

    public static ISpeechRecognitionProvider TryCreate(string modelPath)
    {
        if (string.IsNullOrWhiteSpace(modelPath) || !File.Exists(modelPath))
        {
            return new UnavailableSpeechRecognitionProvider($"Whisper model not found at {modelPath}.");
        }

        return new WhisperSpeechRecognitionProvider(modelPath);
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

            try
            {
                EnsureWhisperLoaded();
            }
            catch (Exception exception)
            {
                RaiseDiagnostic("Recognition failed", "Whisper model failed to load.", exception.Message);
                StatusChanged?.Invoke(this, new SpeechRecognitionStatusChangedEventArgs("Error", exception.Message));
                return;
            }

            pcmBuffer = new MemoryStream();
            speechDetected = false;

            waveIn = new WaveInEvent
            {
                WaveFormat = new WaveFormat(SampleRate, 16, 1),
                BufferMilliseconds = 50
            };
            waveIn.DataAvailable += OnDataAvailable;
            waveIn.RecordingStopped += OnRecordingStopped;

            try
            {
                waveIn.StartRecording();
                isRecording = true;
                RaiseDiagnostic("Recognition started", "Whisper microphone recording started.");
                StatusChanged?.Invoke(this, new SpeechRecognitionStatusChangedEventArgs(
                    "Listening",
                    "Whisper microphone active (auto language)."));
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
            if (!isRecording || waveIn is null)
            {
                return;
            }

            RaiseDiagnostic("PTT release", "Stopping Whisper recording and starting transcription.");
            try
            {
                waveIn.StopRecording();
            }
            catch (Exception exception)
            {
                CleanupRecordingResources();
                RaiseDiagnostic("Exception", "Whisper recording failed to stop.", exception.Message);
                StatusChanged?.Invoke(this, new SpeechRecognitionStatusChangedEventArgs("Error", exception.Message));
            }
        }
    }

    public void Dispose()
    {
        lock (sync)
        {
            CleanupRecordingResources();
            processor?.Dispose();
            processor = null;
            factory?.Dispose();
            factory = null;
        }
    }

    private void EnsureWhisperLoaded()
    {
        if (factory is not null && processor is not null)
        {
            return;
        }

        factory = WhisperFactory.FromPath(modelPath);
        processor = factory.CreateBuilder()
            .WithLanguage("auto")
            .Build();

        initializationDiagnostics.Add(new SpeechRecognitionDiagnosticEventArgs(
            "Recognizer initialized",
            "Whisper processor loaded.",
            Path.GetFileName(modelPath)));
    }

    private void OnDataAvailable(object? sender, WaveInEventArgs e)
    {
        if (e.BytesRecorded <= 0 || pcmBuffer is null)
        {
            return;
        }

        pcmBuffer.Write(e.Buffer, 0, e.BytesRecorded);
        if (!speechDetected && e.BytesRecorded > 0)
        {
            speechDetected = true;
            RaiseDiagnostic("Speech detected", "Audio captured for Whisper transcription.");
        }
    }

    private void OnRecordingStopped(object? sender, StoppedEventArgs e)
    {
        byte[] pcm;
        lock (sync)
        {
            isRecording = false;
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
        try
        {
            if (pcm.Length < MinimumAudioBytes)
            {
                RaiseDiagnostic(
                    "Recognition rejected",
                    "Captured audio was too short for Whisper transcription.",
                    $"{pcm.Length} bytes");
                StatusChanged?.Invoke(this, new SpeechRecognitionStatusChangedEventArgs(
                    "Rejected",
                    "Captured audio was too short."));
                return;
            }

            WhisperProcessor? activeProcessor;
            lock (sync)
            {
                activeProcessor = processor;
            }

            if (activeProcessor is null)
            {
                RaiseDiagnostic("Recognition failed", "Whisper processor is unavailable.");
                return;
            }

            RaiseDiagnostic("Recognition started", "Whisper transcription started.");
            using var wavStream = WhisperPcmAudio.CreateWavStream(pcm, SampleRate);
            var transcript = new StringBuilder();
            await foreach (var segment in activeProcessor.ProcessAsync(wavStream))
            {
                var text = segment.Text.Trim();
                if (text.Length == 0)
                {
                    continue;
                }

                RaiseDiagnostic("Speech hypothesis", text);
                if (transcript.Length > 0 && !char.IsWhiteSpace(transcript[^1]))
                {
                    transcript.Append(' ');
                }

                transcript.Append(text);
            }

            var finalText = transcript.ToString().Trim();
            RaiseDiagnostic("Recognition completed", "Whisper transcription completed.");

            if (finalText.Length == 0)
            {
                RaiseDiagnostic("Recognition rejected", "Whisper returned an empty transcript.");
                StatusChanged?.Invoke(this, new SpeechRecognitionStatusChangedEventArgs(
                    "Rejected",
                    "Whisper returned an empty transcript."));
                return;
            }

            RaiseDiagnostic(
                "Recognition completed",
                "Final speech recognized.",
                $"Text='{finalText}' Confidence={DefaultConfidence.ToString("0.00", CultureInfo.InvariantCulture)}");
            SpeechRecognized?.Invoke(this, new SpeechRecognizedResult(finalText, DefaultConfidence));
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

internal static class WhisperPcmAudio
{
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
