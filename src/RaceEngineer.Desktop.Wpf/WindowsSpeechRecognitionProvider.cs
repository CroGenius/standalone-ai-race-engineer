using System.Globalization;
using System.Speech.Recognition;
using RaceEngineer.Core.Voice;

namespace RaceEngineer.Desktop.Wpf;

public sealed class WindowsSpeechRecognitionProvider : ISpeechRecognitionProvider
{
    private SpeechRecognitionEngine? engine;
    private bool listening;

    private WindowsSpeechRecognitionProvider(SpeechRecognitionEngine engine)
    {
        this.engine = engine;
        this.engine.SpeechRecognized += OnSpeechRecognized;
        this.engine.SpeechRecognitionRejected += OnSpeechRejected;
    }

    public string ProviderName => "Windows Speech Recognition";
    public bool IsAvailable { get; private init; } = true;
    public string AvailabilityDetail { get; private init; } = "Local Windows speech recognizer.";

    public event EventHandler<SpeechRecognizedResult>? SpeechRecognized;
    public event EventHandler<SpeechRecognitionStatusChangedEventArgs>? StatusChanged;

    public void StartListening()
    {
        if (!IsAvailable || engine is null || listening)
        {
            return;
        }

        listening = true;
        engine.RecognizeAsync(RecognizeMode.Multiple);
        StatusChanged?.Invoke(this, new SpeechRecognitionStatusChangedEventArgs("Listening", "Windows microphone active."));
    }

    public void StopListening()
    {
        if (engine is null || !listening)
        {
            return;
        }

        listening = false;
        engine.RecognizeAsyncStop();
        StatusChanged?.Invoke(this, new SpeechRecognitionStatusChangedEventArgs("Idle", "Windows microphone stopped."));
    }

    public void Dispose()
    {
        if (engine is null)
        {
            return;
        }

        engine.SpeechRecognized -= OnSpeechRecognized;
        engine.SpeechRecognitionRejected -= OnSpeechRejected;
        StopListening();
        engine.Dispose();
        engine = null;
    }

    public static ISpeechRecognitionProvider CreateOrFallback(Action<string> reportWarning)
    {
        try
        {
            var installed = SpeechRecognitionEngine.InstalledRecognizers();
            if (installed.Count == 0)
            {
                reportWarning("Speech recognition unavailable. No Windows recognizers are installed.");
                return CreateUnavailableProvider("No Windows speech recognizers are installed.");
            }

            var engine = new SpeechRecognitionEngine(installed[0]);
            engine.SetInputToDefaultAudioDevice();
            LoadRecognitionGrammar(engine);
            return new WindowsSpeechRecognitionProvider(engine);
        }
        catch (Exception exception)
        {
            reportWarning($"Speech recognition unavailable. Voice input disabled. {exception.Message}");
            return CreateUnavailableProvider(exception.Message);
        }
    }

    private static void LoadRecognitionGrammar(SpeechRecognitionEngine engine)
    {
        try
        {
            engine.LoadGrammar(new DictationGrammar());
        }
        catch (Exception)
        {
            engine.LoadGrammar(BuildCoachGrammar());
        }
    }

    private static Grammar BuildCoachGrammar()
    {
        var choices = new Choices(
            "fuel status",
            "tyre status",
            "tire status",
            "brake status",
            "last lap",
            "best lap",
            "recent mistakes",
            "next lap focus",
            "where am I losing time",
            "how is my braking",
            "how is my throttle",
            "what should I improve",
            "tell me about this track",
            "setup notes",
            "fuel plan",
            "copy",
            "radio check");
        return new Grammar(new GrammarBuilder(choices) { Culture = CultureInfo.CurrentCulture });
    }

    private static ISpeechRecognitionProvider CreateUnavailableProvider(string detail)
    {
        return new UnavailableSpeechRecognitionProvider(detail);
    }

    private void OnSpeechRecognized(object? sender, SpeechRecognizedEventArgs e)
    {
        var text = e.Result?.Text?.Trim() ?? "";
        if (text.Length == 0)
        {
            return;
        }

        var confidence = e.Result?.Confidence ?? 0f;
        SpeechRecognized?.Invoke(this, new SpeechRecognizedResult(text, confidence));
    }

    private void OnSpeechRejected(object? sender, SpeechRecognitionRejectedEventArgs e)
    {
        StatusChanged?.Invoke(this, new SpeechRecognitionStatusChangedEventArgs(
            "Rejected",
            "Speech was not recognized clearly enough."));
    }

    #pragma warning disable CS0067
    private sealed class UnavailableSpeechRecognitionProvider : ISpeechRecognitionProvider
    {
        public UnavailableSpeechRecognitionProvider(string detail)
        {
            AvailabilityDetail = detail;
            StatusChanged?.Invoke(this, new SpeechRecognitionStatusChangedEventArgs("Unavailable", detail));
        }

        public string ProviderName => "Windows Speech Recognition";
        public bool IsAvailable => false;
        public string AvailabilityDetail { get; }
        public event EventHandler<SpeechRecognizedResult>? SpeechRecognized;
        public event EventHandler<SpeechRecognitionStatusChangedEventArgs>? StatusChanged;
        public void StartListening() { }
        public void StopListening() { }
        public void Dispose() { }
    }
    #pragma warning restore CS0067
}
