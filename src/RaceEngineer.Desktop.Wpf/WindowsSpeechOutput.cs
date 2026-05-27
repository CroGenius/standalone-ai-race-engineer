using RaceEngineer.Core.Voice;
using System.Speech.Synthesis;

namespace RaceEngineer.Desktop.Wpf;

public sealed class WindowsSpeechOutput : IVoiceOutput, IDisposable
{
    private readonly SpeechSynthesizer synthesizer = new();

    public WindowsSpeechOutput()
    {
        synthesizer.Rate = 1;
        synthesizer.Volume = 90;
    }

    public void Speak(string text)
    {
        synthesizer.SpeakAsyncCancelAll();
        synthesizer.SpeakAsync(text);
    }

    public void Dispose()
    {
        synthesizer.Dispose();
    }

    public static IVoiceOutput CreateOrFallback(Action<string> reportWarning)
    {
        try
        {
            return new WindowsSpeechOutput();
        }
        catch (Exception exception)
        {
            reportWarning($"Voice/TTS unavailable. Speech output disabled. {exception.Message}");
            return new RecordingVoiceOutput();
        }
    }
}
