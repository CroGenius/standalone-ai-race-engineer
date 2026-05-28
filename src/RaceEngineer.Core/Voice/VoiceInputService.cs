namespace RaceEngineer.Core.Voice;

public sealed class VoiceInputOptions
{
    public TimeSpan QueryCooldown { get; init; } = TimeSpan.FromSeconds(3);
    public bool ConfirmationsEnabled { get; init; } = true;
    public bool TranscriptConfirmationEnabled { get; init; }
    public float MinimumConfidence { get; init; } = TranscriptGateOptions.Default.MinimumConfidence;
    public float TranscriptConfirmationAutoAcceptConfidence { get; init; } = 0.68f;
    public TimeSpan PttResultGracePeriod { get; init; } = TimeSpan.FromSeconds(2);
    public TranscriptGateOptions TranscriptGate { get; init; } = TranscriptGateOptions.Default;
}

public sealed class VoiceInputQueryEventArgs(string Text, float Confidence) : EventArgs
{
    public string Text { get; } = Text;
    public float Confidence { get; } = Confidence;
}

public sealed class VoiceInputRejectionEventArgs(string Reason, string SpokenMessage, string? RawTranscript = null) : EventArgs
{
    public string Reason { get; } = Reason;
    public string SpokenMessage { get; } = SpokenMessage;
    public string? RawTranscript { get; } = RawTranscript;
}

public sealed class VoiceInputPendingTranscriptEventArgs(string Text, float Confidence) : EventArgs
{
    public string Text { get; } = Text;
    public float Confidence { get; } = Confidence;
}

public sealed class VoiceInputService : IDisposable
{
    private readonly ISpeechRecognitionProvider provider;
    private readonly IMicrophoneMonitoringProvider? monitoringProvider;
    private VoiceInputOptions options = new();
    private DateTimeOffset? lastQueryAt;
    private bool disposed;
    private CancellationTokenSource? pttGraceCts;
    private bool acceptingPttResults;
    private bool pttSpeechReceived;
    private bool recognitionRejected;
    private bool recognitionCompleted;
    private bool speechDetected;
    private string? lastDiagnosticDetail;
    private string? pendingTranscript;
    private float pendingConfidence;
    private MicDiagnosticsSnapshot micDiagnostics = new(0f, 0f, 0f, 0f, 0f, false, false, "Unknown", MicSignalQuality.Unknown);

    public VoiceInputService(ISpeechRecognitionProvider provider, VoiceInputOptions? options = null)
    {
        this.provider = provider;
        monitoringProvider = provider as IMicrophoneMonitoringProvider;
        if (options is not null)
        {
            this.options = options;
        }

        this.provider.SpeechRecognized += OnSpeechRecognized;
        this.provider.StatusChanged += OnProviderStatusChanged;
        this.provider.DiagnosticRaised += OnProviderDiagnostic;
        if (monitoringProvider is not null)
        {
            monitoringProvider.DiagnosticsUpdated += OnMicDiagnosticsUpdated;
        }

        ProviderStatus = provider.IsAvailable
            ? provider.AvailabilityDetail
            : $"Unavailable: {provider.AvailabilityDetail}";
    }

    public event EventHandler<VoiceInputQueryEventArgs>? QueryRecognized;
    public event EventHandler<VoiceInputRejectionEventArgs>? TranscriptRejected;
    public event EventHandler<VoiceInputPendingTranscriptEventArgs>? TranscriptPendingConfirmation;
    public event EventHandler<SpeechRecognitionDiagnosticEventArgs>? DiagnosticRaised;
    public event EventHandler? StateChanged;

    public bool VoiceInputEnabled { get; private set; }
    public bool InputMuted { get; private set; }
    public bool IsListening { get; private set; }
    public bool PushToTalkActive { get; private set; }
    public bool HasPendingTranscript => !string.IsNullOrWhiteSpace(pendingTranscript);
    public bool ConfirmationsEnabled => options.ConfirmationsEnabled;
    public bool TranscriptConfirmationEnabled => options.TranscriptConfirmationEnabled;
    public string LastRecognizedText { get; private set; } = "";
    public string PendingTranscriptText => pendingTranscript ?? "";
    public string RecognizedSpeechPreview
    {
        get
        {
            if (HasPendingTranscript)
            {
                return $"Pending: {pendingTranscript} (confirm to route)";
            }

            return string.IsNullOrWhiteSpace(LastRecognizedText)
                ? "Recognized speech will appear here."
                : LastRecognizedText;
        }
    }

    public string PushToTalkIndicator => PushToTalkActive ? "PTT Active" : "PTT Idle";
    public string ListeningIndicator => IsListening ? "Listening..." : "Mic idle";
    public string StatusText { get; private set; } = "Voice input disabled";
    public string ProviderName => provider.ProviderName;
    public string ProviderStatus { get; private set; }
    public float MicCurrentRms => micDiagnostics.CurrentRms;
    public float MicPeakRms => micDiagnostics.PeakRms;
    public float MicRawPeakRms => micDiagnostics.RawPeakRms;
    public float MicConvertedPeakRms => micDiagnostics.ConvertedPeakRms;
    public float MicWhisperInputRms => micDiagnostics.WhisperInputRms;
    public bool MicSpeechDetected => micDiagnostics.SpeechDetected;
    public bool MicClipping => micDiagnostics.Clipping;
    public string MicDeviceName => micDiagnostics.MicrophoneDeviceName;
    public string MicSignalQualityLabel => micDiagnostics.SignalQualityLabel;

    public void Configure(VoiceInputOptions newOptions) => options = newOptions;

    public void SetEnabled(bool enabled)
    {
        VoiceInputEnabled = enabled;
        if (!enabled)
        {
            ClearPendingTranscript();
            EndPushToTalk();
            StopMonitoring();
            StatusText = "Voice input disabled";
        }
        else if (!provider.IsAvailable)
        {
            StatusText = "Voice input unavailable";
        }
        else if (InputMuted)
        {
            StatusText = "Voice input muted";
        }
        else
        {
            StartMonitoring();
            StatusText = "Voice input ready";
        }

        RaiseStateChanged();
    }

    public void SetInputMuted(bool muted)
    {
        InputMuted = muted;
        if (muted)
        {
            ClearPendingTranscript();
            EndPushToTalk();
            StopMonitoring();
            StatusText = "Voice input muted";
        }
        else if (VoiceInputEnabled && provider.IsAvailable)
        {
            StartMonitoring();
            StatusText = PushToTalkActive ? "Listening for speech..." : "Voice input ready";
        }

        RaiseStateChanged();
    }

    public void BeginPushToTalk()
    {
        if (!VoiceInputEnabled)
        {
            RaiseDiagnostic("PTT ignored", "Voice input is disabled.");
            return;
        }

        if (InputMuted)
        {
            RaiseDiagnostic("PTT ignored", "Microphone is muted.");
            return;
        }

        if (!provider.IsAvailable)
        {
            RaiseDiagnostic("PTT ignored", "Speech provider is unavailable.");
            return;
        }

        if (PushToTalkActive)
        {
            return;
        }

        ClearPendingTranscript();
        CancelPttGraceTimer();
        ResetPttSessionFlags();
        StartMonitoring();
        PushToTalkActive = true;
        acceptingPttResults = false;
        StatusText = "Listening for speech...";
        RaiseDiagnostic("PTT start", "Push-to-talk started.");

        try
        {
            provider.StartListening();
            IsListening = provider.IsListening || provider.IsAvailable;
            if (!IsListening)
            {
                PushToTalkActive = false;
                StatusText = "Voice input unavailable";
                RaiseDiagnostic("Recognition failed", "Recognizer did not enter listening state.");
            }
            else
            {
                RaiseDiagnostic("Recognition started", "Recognizer listening requested.");
            }
        }
        catch (Exception exception)
        {
            PushToTalkActive = false;
            IsListening = false;
            StatusText = "Voice input unavailable";
            ProviderStatus = "Speech recognition failed to start.";
            RaiseDiagnostic("Exception", "Speech recognition failed to start.", exception.Message);
        }

        RaiseStateChanged();
    }

    public void EndPushToTalk()
    {
        if (!PushToTalkActive && !IsListening && !acceptingPttResults)
        {
            return;
        }

        PushToTalkActive = false;
        acceptingPttResults = true;
        RaiseDiagnostic("PTT release", "Push-to-talk released; waiting for recognition result.");

        try
        {
            provider.StopListening();
        }
        catch (Exception exception)
        {
            RaiseDiagnostic("Exception", "Speech recognition failed to stop gracefully.", exception.Message);
        }

        IsListening = provider.IsListening;
        StartPttGraceTimer();
        RaiseStateChanged();
    }

    public bool ConfirmPendingTranscript()
    {
        if (!HasPendingTranscript)
        {
            return false;
        }

        var text = pendingTranscript!;
        var confidence = pendingConfidence;
        ClearPendingTranscript();
        return RouteAcceptedQuery(text, confidence);
    }

    public void RejectPendingTranscript()
    {
        if (!HasPendingTranscript)
        {
            return;
        }

        var rejected = pendingTranscript;
        ClearPendingTranscript();
        StatusText = "Transcript rejected.";
        TranscriptRejected?.Invoke(this, new VoiceInputRejectionEventArgs("Transcript rejected by user.", "I didn't catch that.", rejected));
        RaiseStateChanged();
    }

    public bool TrySubmitQuery(string text, out string? rejectionReason)
    {
        rejectionReason = null;
        if (!VoiceInputEnabled)
        {
            rejectionReason = "Voice input disabled.";
            return false;
        }

        if (InputMuted)
        {
            rejectionReason = "Voice input muted.";
            return false;
        }

        var normalized = NormalizeQuery(text);
        if (normalized.Length == 0)
        {
            rejectionReason = "Empty query.";
            return false;
        }

        return RouteAcceptedQuery(normalized, 1f);
    }

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        CancelPttGraceTimer();
        provider.SpeechRecognized -= OnSpeechRecognized;
        provider.StatusChanged -= OnProviderStatusChanged;
        provider.DiagnosticRaised -= OnProviderDiagnostic;
        if (monitoringProvider is not null)
        {
            monitoringProvider.DiagnosticsUpdated -= OnMicDiagnosticsUpdated;
        }

        EndPushToTalk();
        StopMonitoring();
        provider.Dispose();
    }

    private void OnSpeechRecognized(object? sender, SpeechRecognizedResult result)
    {
        if (!VoiceInputEnabled || InputMuted)
        {
            return;
        }

        if (!PushToTalkActive && !acceptingPttResults)
        {
            RaiseDiagnostic(
                "Recognition dropped",
                "Speech result arrived outside the active PTT session.",
                result.Text);
            return;
        }

        RaiseDiagnostic(
            "Recognition completed",
            "Speech recognized.",
            $"Text='{result.Text}' Confidence={result.Confidence:0.00}");

        var gateDecision = TranscriptGate.Evaluate(
            result.Text,
            result.Confidence,
            result.CaptureMetrics,
            options.TranscriptGate with { MinimumConfidence = options.MinimumConfidence });
        if (!gateDecision.Accepted)
        {
            RejectTranscript(gateDecision, result.Text);
            return;
        }

        var normalized = NormalizeQuery(result.Text);
        if (normalized.Length == 0)
        {
            RejectTranscript(TranscriptGateDecision.Reject("Recognized speech was empty.", "I didn't catch that."), result.Text);
            return;
        }

        pttSpeechReceived = true;
        CancelPttGraceTimer();
        acceptingPttResults = false;
        IsListening = false;
        LastRecognizedText = normalized;

        if (options.TranscriptConfirmationEnabled
            && result.Confidence < options.TranscriptConfirmationAutoAcceptConfidence)
        {
            pendingTranscript = normalized;
            pendingConfidence = result.Confidence;
            StatusText = "Confirm recognized transcript.";
            TranscriptPendingConfirmation?.Invoke(this, new VoiceInputPendingTranscriptEventArgs(normalized, result.Confidence));
            RaiseStateChanged();
            return;
        }

        if (!RouteAcceptedQuery(normalized, result.Confidence))
        {
            RaiseStateChanged();
        }
    }

    private bool RouteAcceptedQuery(string normalized, float confidence)
    {
        if (!TryConsumeCooldown(out var rejectionReason))
        {
            StatusText = rejectionReason ?? "Voice query cooldown active.";
            RaiseDiagnostic("Recognition rejected", StatusText, rejectionReason);
            return false;
        }

        QueryRecognized?.Invoke(this, new VoiceInputQueryEventArgs(normalized, confidence));
        StatusText = "Voice query accepted.";
        return true;
    }

    private void RejectTranscript(TranscriptGateDecision decision, string rawTranscript)
    {
        recognitionRejected = true;
        pttSpeechReceived = false;
        acceptingPttResults = false;
        IsListening = false;
        StatusText = decision.Reason;
        RaiseDiagnostic("Recognition rejected", decision.Reason, rawTranscript);
        TranscriptRejected?.Invoke(this, new VoiceInputRejectionEventArgs(decision.Reason, decision.SpokenRejectionMessage, rawTranscript));
        RaiseStateChanged();
    }

    private void OnMicDiagnosticsUpdated(object? sender, MicDiagnosticsSnapshot snapshot)
    {
        micDiagnostics = snapshot;
        RaiseStateChanged();
    }

    private void StartMonitoring()
    {
        if (monitoringProvider is null || monitoringProvider.IsMonitoring)
        {
            return;
        }

        try
        {
            monitoringProvider.StartMonitoring();
        }
        catch (Exception exception)
        {
            RaiseDiagnostic("Exception", "Microphone monitoring failed to start.", exception.Message);
        }
    }

    private void StopMonitoring()
    {
        if (monitoringProvider is null || !monitoringProvider.IsMonitoring)
        {
            return;
        }

        monitoringProvider.StopMonitoring();
    }

    private void ClearPendingTranscript()
    {
        pendingTranscript = null;
        pendingConfidence = 0f;
    }

    private void OnProviderStatusChanged(object? sender, SpeechRecognitionStatusChangedEventArgs e)
    {
        ProviderStatus = string.IsNullOrWhiteSpace(e.Detail) ? e.State : $"{e.State} — {e.Detail}";
        RaiseStateChanged();
    }

    private void OnProviderDiagnostic(object? sender, SpeechRecognitionDiagnosticEventArgs e)
    {
        if (e.Stage.Equals("Speech detected", StringComparison.OrdinalIgnoreCase))
        {
            speechDetected = true;
        }
        else if (e.Stage.Equals("Recognition rejected", StringComparison.OrdinalIgnoreCase))
        {
            recognitionRejected = true;
        }
        else if (e.Stage.Equals("Recognition completed", StringComparison.OrdinalIgnoreCase))
        {
            recognitionCompleted = true;
        }
        else if (e.Stage.Equals("Speech hypothesis", StringComparison.OrdinalIgnoreCase)
            && !string.IsNullOrWhiteSpace(e.Message))
        {
            LastRecognizedText = e.Message;
        }
        else if (acceptingPttResults && ShouldExtendPttGrace(e))
        {
            StartPttGraceTimer();
        }

        lastDiagnosticDetail = e.Detail ?? e.Message;
        RaiseDiagnostic(e.Stage, e.Message, e.Detail);
    }

    private static bool ShouldExtendPttGrace(SpeechRecognitionDiagnosticEventArgs diagnostic)
    {
        if (diagnostic.Stage.Equals("Speech hypothesis", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (diagnostic.Stage.Equals("Audio captured", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (diagnostic.Stage.Equals("Whisper metrics", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (diagnostic.Stage.Equals("Language detected", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (diagnostic.Stage.Equals("PTT release", StringComparison.OrdinalIgnoreCase)
            && diagnostic.Message.Contains("trailing audio", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return diagnostic.Stage.Equals("Recognition started", StringComparison.OrdinalIgnoreCase)
            && diagnostic.Message.Contains("transcription", StringComparison.OrdinalIgnoreCase);
    }

    private void StartPttGraceTimer()
    {
        CancelPttGraceTimer();
        pttGraceCts = new CancellationTokenSource();
        var token = pttGraceCts.Token;
        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(options.PttResultGracePeriod, token);
                if (token.IsCancellationRequested || pttSpeechReceived || HasPendingTranscript)
                {
                    return;
                }

                acceptingPttResults = false;
                IsListening = false;
                StatusText = "No speech recognized";
                var reason = BuildNoSpeechReason();
                RaiseDiagnostic("No speech timeout", "No speech recognized.", reason);
                TranscriptRejected?.Invoke(this, new VoiceInputRejectionEventArgs(reason, "I didn't catch that."));
                RaiseStateChanged();
            }
            catch (TaskCanceledException)
            {
            }
        }, token);
    }

    private string BuildNoSpeechReason()
    {
        if (recognitionRejected)
        {
            return "Recognizer rejected the utterance or confidence was too low.";
        }

        if (!speechDetected)
        {
            return "No speech was detected on the microphone during this PTT hold.";
        }

        if (recognitionCompleted && !pttSpeechReceived)
        {
            return "Recognition completed without a usable phrase match.";
        }

        return lastDiagnosticDetail ?? "Recognizer did not return a final phrase before the grace timeout.";
    }

    private void ResetPttSessionFlags()
    {
        pttSpeechReceived = false;
        recognitionRejected = false;
        recognitionCompleted = false;
        speechDetected = false;
        lastDiagnosticDetail = null;
    }

    private void CancelPttGraceTimer()
    {
        pttGraceCts?.Cancel();
        pttGraceCts = null;
    }

    private bool TryConsumeCooldown(out string? rejectionReason)
    {
        rejectionReason = null;
        if (lastQueryAt is null)
        {
            lastQueryAt = DateTimeOffset.UtcNow;
            return true;
        }

        var elapsed = DateTimeOffset.UtcNow - lastQueryAt.Value;
        if (elapsed < options.QueryCooldown)
        {
            var remaining = options.QueryCooldown - elapsed;
            rejectionReason = $"Voice query cooldown ({remaining.TotalSeconds:0}s).";
            return false;
        }

        lastQueryAt = DateTimeOffset.UtcNow;
        return true;
    }

    private static string NormalizeQuery(string text) =>
        text.ReplaceLineEndings(" ").Trim();

    private void RaiseDiagnostic(string stage, string message, string? detail = null)
    {
        DiagnosticRaised?.Invoke(this, new SpeechRecognitionDiagnosticEventArgs(stage, message, detail));
    }

    private void RaiseStateChanged() => StateChanged?.Invoke(this, EventArgs.Empty);
}
