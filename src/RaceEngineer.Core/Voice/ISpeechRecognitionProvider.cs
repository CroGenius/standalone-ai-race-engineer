namespace RaceEngineer.Core.Voice;

public sealed record SpeechRecognizedResult(string Text, float Confidence);

public sealed class SpeechRecognitionStatusChangedEventArgs(string State, string? Detail = null) : EventArgs
{
    public string State { get; } = State;
    public string? Detail { get; } = Detail;
}

public interface ISpeechRecognitionProvider : IDisposable
{
    string ProviderName { get; }
    bool IsAvailable { get; }
    string AvailabilityDetail { get; }
    event EventHandler<SpeechRecognizedResult>? SpeechRecognized;
    event EventHandler<SpeechRecognitionStatusChangedEventArgs>? StatusChanged;
    void StartListening();
    void StopListening();
}
