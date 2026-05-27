namespace RaceEngineer.Core.Voice;

public sealed class VoiceInputOptions
{
    public TimeSpan QueryCooldown { get; init; } = TimeSpan.FromSeconds(3);
    public bool ConfirmationsEnabled { get; init; } = true;
    public float MinimumConfidence { get; init; } = 0.35f;
    public TimeSpan PttResultGracePeriod { get; init; } = TimeSpan.FromSeconds(2);
}

public sealed class VoiceInputQueryEventArgs(string Text, float Confidence) : EventArgs
{
    public string Text { get; } = Text;
    public float Confidence { get; } = Confidence;
}

public sealed class VoiceInputService : IDisposable
{
    private readonly ISpeechRecognitionProvider provider;
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

    public VoiceInputService(ISpeechRecognitionProvider provider, VoiceInputOptions? options = null)
    {
        this.provider = provider;
        if (options is not null)
        {
            this.options = options;
        }

        this.provider.SpeechRecognized += OnSpeechRecognized;
        this.provider.StatusChanged += OnProviderStatusChanged;
        this.provider.DiagnosticRaised += OnProviderDiagnostic;
        ProviderStatus = provider.IsAvailable
            ? provider.AvailabilityDetail
            : $"Unavailable: {provider.AvailabilityDetail}";
    }

    public event EventHandler<VoiceInputQueryEventArgs>? QueryRecognized;
    public event EventHandler<SpeechRecognitionDiagnosticEventArgs>? DiagnosticRaised;
    public event EventHandler? StateChanged;

    public bool VoiceInputEnabled { get; private set; }
    public bool InputMuted { get; private set; }
    public bool IsListening { get; private set; }
    public bool PushToTalkActive { get; private set; }
    public bool ConfirmationsEnabled => options.ConfirmationsEnabled;
    public string LastRecognizedText { get; private set; } = "";
    public string RecognizedSpeechPreview => string.IsNullOrWhiteSpace(LastRecognizedText)
        ? "Recognized speech will appear here."
        : LastRecognizedText;
    public string PushToTalkIndicator => PushToTalkActive ? "PTT Active" : "PTT Idle";
    public string ListeningIndicator => IsListening ? "Listening..." : "Mic idle";
    public string StatusText { get; private set; } = "Voice input disabled";
    public string ProviderName => provider.ProviderName;
    public string ProviderStatus { get; private set; }

    public void Configure(VoiceInputOptions newOptions) => options = newOptions;

    public void SetEnabled(bool enabled)
    {
        VoiceInputEnabled = enabled;
        if (!enabled)
        {
            EndPushToTalk();
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
            StatusText = "Voice input ready";
        }

        RaiseStateChanged();
    }

    public void SetInputMuted(bool muted)
    {
        InputMuted = muted;
        if (muted)
        {
            EndPushToTalk();
            StatusText = "Voice input muted";
        }
        else if (VoiceInputEnabled && provider.IsAvailable)
        {
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

        CancelPttGraceTimer();
        ResetPttSessionFlags();
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

        if (!TryConsumeCooldown(out rejectionReason))
        {
            StatusText = rejectionReason ?? "Voice query cooldown active.";
            RaiseStateChanged();
            return false;
        }

        LastRecognizedText = normalized;
        QueryRecognized?.Invoke(this, new VoiceInputQueryEventArgs(normalized, 1f));
        StatusText = "Voice query accepted.";
        RaiseStateChanged();
        return true;
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
        EndPushToTalk();
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

        if (result.Confidence < options.MinimumConfidence)
        {
            StatusText = "Speech confidence too low.";
            RaiseDiagnostic(
                "Recognition rejected",
                "Speech confidence was below threshold.",
                $"Confidence={result.Confidence:0.00} Minimum={options.MinimumConfidence:0.00}");
            RaiseStateChanged();
            return;
        }

        var normalized = NormalizeQuery(result.Text);
        if (normalized.Length == 0)
        {
            RaiseDiagnostic("Recognition rejected", "Recognized speech was empty.");
            return;
        }

        pttSpeechReceived = true;
        CancelPttGraceTimer();
        acceptingPttResults = false;
        IsListening = false;
        LastRecognizedText = normalized;

        if (!TryConsumeCooldown(out var rejectionReason))
        {
            StatusText = rejectionReason ?? "Voice query cooldown active.";
            RaiseDiagnostic("Recognition rejected", StatusText, rejectionReason);
            RaiseStateChanged();
            return;
        }

        QueryRecognized?.Invoke(this, new VoiceInputQueryEventArgs(normalized, result.Confidence));
        StatusText = "Voice query accepted.";
        RaiseStateChanged();
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

        lastDiagnosticDetail = e.Detail ?? e.Message;
        RaiseDiagnostic(e.Stage, e.Message, e.Detail);
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
                if (token.IsCancellationRequested || pttSpeechReceived)
                {
                    return;
                }

                acceptingPttResults = false;
                IsListening = false;
                StatusText = "No speech recognized";
                var reason = BuildNoSpeechReason();
                RaiseDiagnostic("No speech timeout", "No speech recognized.", reason);
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

    private static string NormalizeQuery(string text)
    {
        return text.ReplaceLineEndings(" ").Trim();
    }

    private void RaiseDiagnostic(string stage, string message, string? detail = null)
    {
        DiagnosticRaised?.Invoke(this, new SpeechRecognitionDiagnosticEventArgs(stage, message, detail));
    }

    private void RaiseStateChanged() => StateChanged?.Invoke(this, EventArgs.Empty);
}
