namespace RaceEngineer.Core.Voice;

public sealed record VoiceInitializationReport(
    string SelectedProvider,
    bool NaudioAvailable,
    string? NaudioError,
    int MicrophoneDeviceCount,
    string MicrophoneSummary,
    string ActiveProviderName,
    bool ProviderAvailable,
    string ProviderAvailabilityDetail,
    string FailedComponent,
    string? InitializationException)
{
    public string SummaryForChat =>
        ProviderAvailable
            ? $"Voice input ready via {ActiveProviderName}."
            : string.IsNullOrWhiteSpace(InitializationException)
                ? $"Voice input unavailable ({FailedComponent})."
                : $"Voice input unavailable ({FailedComponent}): {InitializationException}";
}
