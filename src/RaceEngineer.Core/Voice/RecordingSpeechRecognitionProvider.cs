namespace RaceEngineer.Core.Voice;

public sealed class RecordingSpeechRecognitionProvider : ISpeechRecognitionProvider
{
    private readonly List<string> recognizedTexts = [];
    private bool listening;
    private bool deliverAfterStop;

    public string ProviderName => "Recording Speech Recognition";
    public bool IsAvailable { get; private set; } = true;
    public bool IsListening => listening;
    public string AvailabilityDetail { get; private set; } = "Deterministic test provider.";
    public IReadOnlyList<string> RecognizedTexts => recognizedTexts;

    public event EventHandler<SpeechRecognizedResult>? SpeechRecognized;
    public event EventHandler<SpeechRecognitionStatusChangedEventArgs>? StatusChanged;
    public event EventHandler<SpeechRecognitionDiagnosticEventArgs>? DiagnosticRaised;

    public void SetAvailable(bool available, string detail)
    {
        IsAvailable = available;
        AvailabilityDetail = detail;
    }

    public void StartListening()
    {
        if (!IsAvailable)
        {
            DiagnosticRaised?.Invoke(
                this,
                new SpeechRecognitionDiagnosticEventArgs("Unavailable", "Recording provider unavailable."));
            return;
        }

        listening = true;
        deliverAfterStop = false;
        DiagnosticRaised?.Invoke(this, new SpeechRecognitionDiagnosticEventArgs("Recognition started", "Recording provider active."));
        StatusChanged?.Invoke(this, new SpeechRecognitionStatusChangedEventArgs("Listening", "Recording provider active."));
    }

    public void StopListening()
    {
        listening = false;
        deliverAfterStop = true;
        DiagnosticRaised?.Invoke(this, new SpeechRecognitionDiagnosticEventArgs("Recognition stopped", "Recording provider stopped."));
        StatusChanged?.Invoke(this, new SpeechRecognitionStatusChangedEventArgs("Idle", "Recording provider stopped."));
    }

    public void SimulateRecognition(string text, float confidence = 0.92f, SpeechCaptureMetrics? captureMetrics = null)
    {
        if (!listening && !deliverAfterStop)
        {
            return;
        }

        deliverAfterStop = false;
        recognizedTexts.Add(text);
        DiagnosticRaised?.Invoke(this, new SpeechRecognitionDiagnosticEventArgs("Recognition completed", "Simulated speech recognized.", text));
        SpeechRecognized?.Invoke(this, new SpeechRecognizedResult(text, confidence, captureMetrics));
    }

    public void Dispose()
    {
        listening = false;
        deliverAfterStop = false;
    }
}
