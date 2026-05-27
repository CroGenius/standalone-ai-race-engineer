namespace RaceEngineer.Core.Voice;

public sealed class UnavailableSpeechRecognitionProvider : ISpeechRecognitionProvider
{
    public UnavailableSpeechRecognitionProvider(string detail)
    {
        AvailabilityDetail = detail;
    }

    public string ProviderName => "Speech Recognition";
    public bool IsAvailable => false;
    public bool IsListening => false;
    public string AvailabilityDetail { get; }

#pragma warning disable CS0067
    public event EventHandler<SpeechRecognizedResult>? SpeechRecognized;
    public event EventHandler<SpeechRecognitionStatusChangedEventArgs>? StatusChanged;
    public event EventHandler<SpeechRecognitionDiagnosticEventArgs>? DiagnosticRaised;
#pragma warning restore CS0067

    public void StartListening()
    {
        DiagnosticRaised?.Invoke(
            this,
            new SpeechRecognitionDiagnosticEventArgs("Unavailable", "Speech recognition is unavailable.", AvailabilityDetail));
    }

    public void StopListening()
    {
    }

    public void Dispose()
    {
    }
}
