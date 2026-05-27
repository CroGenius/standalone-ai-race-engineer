namespace RaceEngineer.Core.Voice;

public sealed class VoiceInputOptions
{
    public TimeSpan QueryCooldown { get; init; } = TimeSpan.FromSeconds(3);
    public bool ConfirmationsEnabled { get; init; } = true;
    public float MinimumConfidence { get; init; } = 0.35f;
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

    public VoiceInputService(ISpeechRecognitionProvider provider, VoiceInputOptions? options = null)
    {
        this.provider = provider;
        if (options is not null)
        {
            this.options = options;
        }

        this.provider.SpeechRecognized += OnSpeechRecognized;
        this.provider.StatusChanged += OnProviderStatusChanged;
        ProviderStatus = provider.IsAvailable
            ? provider.AvailabilityDetail
            : $"Unavailable: {provider.AvailabilityDetail}";
    }

    public event EventHandler<VoiceInputQueryEventArgs>? QueryRecognized;
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
        if (!VoiceInputEnabled || InputMuted || !provider.IsAvailable || PushToTalkActive)
        {
            return;
        }

        PushToTalkActive = true;
        StatusText = "Listening for speech...";
        provider.StartListening();
        IsListening = true;
        RaiseStateChanged();
    }

    public void EndPushToTalk()
    {
        if (!PushToTalkActive && !IsListening)
        {
            return;
        }

        PushToTalkActive = false;
        provider.StopListening();
        IsListening = false;
        if (VoiceInputEnabled && !InputMuted && provider.IsAvailable)
        {
            StatusText = "Voice input ready";
        }

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
        provider.SpeechRecognized -= OnSpeechRecognized;
        provider.StatusChanged -= OnProviderStatusChanged;
        EndPushToTalk();
        provider.Dispose();
    }

    private void OnSpeechRecognized(object? sender, SpeechRecognizedResult result)
    {
        if (!VoiceInputEnabled || InputMuted || !PushToTalkActive)
        {
            return;
        }

        if (result.Confidence < options.MinimumConfidence)
        {
            StatusText = "Speech confidence too low.";
            RaiseStateChanged();
            return;
        }

        var normalized = NormalizeQuery(result.Text);
        if (normalized.Length == 0)
        {
            return;
        }

        LastRecognizedText = normalized;
        if (!TryConsumeCooldown(out var rejectionReason))
        {
            StatusText = rejectionReason ?? "Voice query cooldown active.";
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

    private void RaiseStateChanged() => StateChanged?.Invoke(this, EventArgs.Empty);
}
