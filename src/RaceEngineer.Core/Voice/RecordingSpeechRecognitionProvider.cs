namespace RaceEngineer.Core.Voice;

public sealed class RecordingSpeechRecognitionProvider : ISpeechRecognitionProvider
{
    private readonly List<string> recognizedTexts = [];
    private bool listening;

    public string ProviderName => "Recording Speech Recognition";
    public bool IsAvailable { get; private set; } = true;
    public string AvailabilityDetail { get; private set; } = "Deterministic test provider.";
    public IReadOnlyList<string> RecognizedTexts => recognizedTexts;

    public event EventHandler<SpeechRecognizedResult>? SpeechRecognized;
    public event EventHandler<SpeechRecognitionStatusChangedEventArgs>? StatusChanged;

    public void SetAvailable(bool available, string detail)
    {
        IsAvailable = available;
        AvailabilityDetail = detail;
    }

    public void StartListening()
    {
        if (!IsAvailable)
        {
            return;
        }

        listening = true;
        StatusChanged?.Invoke(this, new SpeechRecognitionStatusChangedEventArgs("Listening", "Recording provider active."));
    }

    public void StopListening()
    {
        listening = false;
        StatusChanged?.Invoke(this, new SpeechRecognitionStatusChangedEventArgs("Idle", "Recording provider stopped."));
    }

    public void SimulateRecognition(string text, float confidence = 0.92f)
    {
        if (!listening)
        {
            return;
        }

        recognizedTexts.Add(text);
        SpeechRecognized?.Invoke(this, new SpeechRecognizedResult(text, confidence));
    }

    public void Dispose()
    {
        listening = false;
    }
}
