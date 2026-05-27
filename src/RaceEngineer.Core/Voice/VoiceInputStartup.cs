namespace RaceEngineer.Core.Voice;

public static class VoiceInputStartup
{
    public static VoiceInputService CreateService(
        ISpeechRecognitionProvider provider,
        VoiceInputOptions? options = null,
        bool enabled = false)
    {
        var service = new VoiceInputService(provider, options);
        service.SetEnabled(enabled);
        return service;
    }

    public static VoiceInputService CreateUnavailableService(
        string reason,
        VoiceInputOptions? options = null,
        Action<string>? reportWarning = null)
    {
        reportWarning?.Invoke(reason);
        return CreateService(new UnavailableSpeechRecognitionProvider(reason), options, enabled: false);
    }
}
