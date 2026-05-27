namespace RaceEngineer.Core.Voice;

public sealed class SpeechRecognitionDiagnosticEventArgs(string Stage, string Message, string? Detail = null) : EventArgs
{
    public string Stage { get; } = Stage;
    public string Message { get; } = Message;
    public string? Detail { get; } = Detail;
}
