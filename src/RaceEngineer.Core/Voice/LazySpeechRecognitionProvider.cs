namespace RaceEngineer.Core.Voice;

public sealed class LazySpeechRecognitionProvider : ISpeechRecognitionProvider
{
    private readonly Func<ISpeechRecognitionProvider> factory;
    private readonly object sync = new();
    private ISpeechRecognitionProvider? inner;
    private bool initializationAttempted;
    private string availabilityDetail = "Speech recognition not initialized yet.";

    public LazySpeechRecognitionProvider(Func<ISpeechRecognitionProvider> factory)
    {
        this.factory = factory;
    }

    public string ProviderName => inner?.ProviderName ?? "Speech Recognition";
    public bool IsAvailable => inner?.IsAvailable ?? false;
    public bool IsListening => inner?.IsListening ?? false;
    public string AvailabilityDetail => inner?.AvailabilityDetail ?? availabilityDetail;

    public event EventHandler<SpeechRecognizedResult>? SpeechRecognized;
    public event EventHandler<SpeechRecognitionStatusChangedEventArgs>? StatusChanged;
    public event EventHandler<SpeechRecognitionDiagnosticEventArgs>? DiagnosticRaised;

    public void StartListening()
    {
        EnsureInner().StartListening();
    }

    public void StopListening()
    {
        if (inner is null)
        {
            return;
        }

        inner.StopListening();
    }

    public void Dispose()
    {
        if (inner is null)
        {
            return;
        }

        inner.SpeechRecognized -= ForwardSpeechRecognized;
        inner.StatusChanged -= ForwardStatusChanged;
        inner.DiagnosticRaised -= ForwardDiagnostic;
        inner.Dispose();
        inner = null;
    }

    private ISpeechRecognitionProvider EnsureInner()
    {
        if (inner is not null)
        {
            return inner;
        }

        lock (sync)
        {
            if (inner is not null)
            {
                return inner;
            }

            if (!initializationAttempted)
            {
                initializationAttempted = true;
                try
                {
                    inner = factory();
                    availabilityDetail = inner.AvailabilityDetail;
                }
                catch (Exception exception)
                {
                    inner = new UnavailableSpeechRecognitionProvider(exception.Message);
                    availabilityDetail = inner.AvailabilityDetail;
                }
            }
            else
            {
                inner ??= new UnavailableSpeechRecognitionProvider(availabilityDetail);
            }

            inner.SpeechRecognized += ForwardSpeechRecognized;
            inner.StatusChanged += ForwardStatusChanged;
            inner.DiagnosticRaised += ForwardDiagnostic;
            return inner;
        }
    }

    private void ForwardSpeechRecognized(object? sender, SpeechRecognizedResult result)
    {
        SpeechRecognized?.Invoke(this, result);
    }

    private void ForwardStatusChanged(object? sender, SpeechRecognitionStatusChangedEventArgs e)
    {
        StatusChanged?.Invoke(this, e);
    }

    private void ForwardDiagnostic(object? sender, SpeechRecognitionDiagnosticEventArgs e)
    {
        DiagnosticRaised?.Invoke(this, e);
    }
}
