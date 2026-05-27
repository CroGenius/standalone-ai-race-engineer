using System.Globalization;
using System.Speech.Recognition;
using RaceEngineer.Core.Coaching;
using RaceEngineer.Core.Voice;

namespace RaceEngineer.Desktop.Wpf;

public sealed class WindowsSpeechRecognitionProvider : ISpeechRecognitionProvider
{
    private SpeechRecognitionEngine? engine;
    private bool recognizeAsyncActive;
    private bool stopRequested;
    private readonly IReadOnlyList<SpeechRecognitionDiagnosticEventArgs>? initializationDiagnostics;
    private bool initializationDiagnosticsEmitted;

    private WindowsSpeechRecognitionProvider(
        SpeechRecognitionEngine engine,
        string availabilityDetail,
        IReadOnlyList<SpeechRecognitionDiagnosticEventArgs>? initializationDiagnostics = null)
    {
        this.engine = engine;
        AvailabilityDetail = availabilityDetail;
        this.initializationDiagnostics = initializationDiagnostics;
        WireEngineEvents(this.engine);
    }

    public string ProviderName => "Windows Speech Recognition";
    public bool IsAvailable { get; private init; } = true;
    public bool IsListening => recognizeAsyncActive;
    public string AvailabilityDetail { get; }

    public event EventHandler<SpeechRecognizedResult>? SpeechRecognized;
    public event EventHandler<SpeechRecognitionStatusChangedEventArgs>? StatusChanged;
    public event EventHandler<SpeechRecognitionDiagnosticEventArgs>? DiagnosticRaised;

    public static ISpeechRecognitionProvider CreateOrFallback(
        Action<string> reportWarning,
        string? speechRecognitionCulture = null)
    {
        var configuredCulture = speechRecognitionCulture;
        return new LazySpeechRecognitionProvider(() => CreateEngineProvider(reportWarning, configuredCulture));
    }

    internal static ISpeechRecognitionProvider CreateEngineProvider(
        Action<string> reportWarning,
        string? speechRecognitionCulture = null)
    {
        var initDiagnostics = new List<SpeechRecognitionDiagnosticEventArgs>();
        try
        {
            var installed = SpeechRecognitionEngine.InstalledRecognizers();
            if (installed.Count == 0)
            {
                reportWarning("Speech recognition unavailable. No Windows recognizers are installed.");
                return new UnavailableSpeechRecognitionProvider("No Windows speech recognizers are installed.");
            }

            var cultureResolution = SpeechRecognitionCultureResolver.Resolve(speechRecognitionCulture);
            if (cultureResolution.WarningMessage is not null)
            {
                reportWarning(cultureResolution.WarningMessage);
            }

            var recognizer = ResolveRecognizer(installed, cultureResolution.Culture, out var fallbackDetail);
            if (fallbackDetail is not null)
            {
                reportWarning(fallbackDetail);
            }

            initDiagnostics.Add(new SpeechRecognitionDiagnosticEventArgs(
                "Recognizer culture selected",
                $"Selected recognizer culture {recognizer.Culture.Name}.",
                cultureResolution.Detail));

            var engine = new SpeechRecognitionEngine(recognizer);
            initDiagnostics.Add(new SpeechRecognitionDiagnosticEventArgs(
                "Recognizer initialized",
                "SpeechRecognitionEngine created.",
                recognizer.Culture.Name));

            engine.SetInputToDefaultAudioDevice();
            initDiagnostics.Add(new SpeechRecognitionDiagnosticEventArgs(
                "Microphone attached",
                "Default audio input device attached."));

            LoadRecognitionGrammar(engine, recognizer.Culture, initDiagnostics);
            var detail = $"Recognizer {recognizer.Culture.Name}. {cultureResolution.Detail}";
            return new WindowsSpeechRecognitionProvider(engine, detail, initDiagnostics);
        }
        catch (Exception exception)
        {
            reportWarning($"Speech recognition unavailable. Voice input disabled. {exception.Message}");
            return new UnavailableSpeechRecognitionProvider(exception.Message);
        }
    }

    public void StartListening()
    {
        EmitPendingInitializationDiagnostics();

        if (!IsAvailable || engine is null)
        {
            RaiseDiagnostic("Recognition failed", "Recognizer engine is unavailable.");
            return;
        }

        if (recognizeAsyncActive)
        {
            RaiseDiagnostic("Recognition started", "RecognizeAsync already active.");
            return;
        }

        stopRequested = false;

        try
        {
            engine.RecognizeAsync(RecognizeMode.Multiple);
            recognizeAsyncActive = true;
            RaiseDiagnostic(
                "Recognition started",
                "RecognizeAsync(Multiple) started.",
                engine.RecognizerInfo.Culture.Name);
            StatusChanged?.Invoke(this, new SpeechRecognitionStatusChangedEventArgs(
                "Listening",
                $"Windows microphone active ({engine.RecognizerInfo.Culture.Name})."));
        }
        catch (Exception exception)
        {
            recognizeAsyncActive = false;
            RaiseDiagnostic("Exception", "RecognizeAsync failed to start.", exception.Message);
            StatusChanged?.Invoke(this, new SpeechRecognitionStatusChangedEventArgs("Error", exception.Message));
        }
    }

    public void StopListening()
    {
        if (engine is null || !recognizeAsyncActive)
        {
            return;
        }

        stopRequested = true;
        RaiseDiagnostic("PTT release", "Requesting graceful RecognizeAsyncStop after current utterance.");

        try
        {
            engine.RecognizeAsyncStop();
            RaiseDiagnostic(
                "Recognition stopped",
                "RecognizeAsyncStop requested; waiting for utterance completion.");
        }
        catch (Exception exception)
        {
            RaiseDiagnostic("Exception", "RecognizeAsyncStop failed.", exception.Message);
            StatusChanged?.Invoke(this, new SpeechRecognitionStatusChangedEventArgs("Error", exception.Message));
        }
    }

    public void Dispose()
    {
        if (engine is null)
        {
            return;
        }

        UnwireEngineEvents(engine);
        if (recognizeAsyncActive)
        {
            try
            {
                engine.RecognizeAsyncCancel();
            }
            catch (Exception)
            {
                // Best effort on shutdown.
            }
        }

        engine.Dispose();
        engine = null;
        recognizeAsyncActive = false;
    }

    private void WireEngineEvents(SpeechRecognitionEngine recognitionEngine)
    {
        recognitionEngine.SpeechRecognized += OnSpeechRecognized;
        recognitionEngine.SpeechRecognitionRejected += OnSpeechRejected;
        recognitionEngine.SpeechDetected += OnSpeechDetected;
        recognitionEngine.SpeechHypothesized += OnSpeechHypothesized;
        recognitionEngine.RecognizeCompleted += OnRecognizeCompleted;
        recognitionEngine.AudioSignalProblemOccurred += OnAudioSignalProblemOccurred;
    }

    private void UnwireEngineEvents(SpeechRecognitionEngine recognitionEngine)
    {
        recognitionEngine.SpeechRecognized -= OnSpeechRecognized;
        recognitionEngine.SpeechRecognitionRejected -= OnSpeechRejected;
        recognitionEngine.SpeechDetected -= OnSpeechDetected;
        recognitionEngine.SpeechHypothesized -= OnSpeechHypothesized;
        recognitionEngine.RecognizeCompleted -= OnRecognizeCompleted;
        recognitionEngine.AudioSignalProblemOccurred -= OnAudioSignalProblemOccurred;
    }

    private static RecognizerInfo ResolveRecognizer(
        IReadOnlyList<RecognizerInfo> installed,
        CultureInfo requestedCulture,
        out string? fallbackWarning)
    {
        fallbackWarning = null;
        var exact = installed.FirstOrDefault(item =>
            item.Culture.Name.Equals(requestedCulture.Name, StringComparison.OrdinalIgnoreCase));
        if (exact is not null)
        {
            return exact;
        }

        var languageMatch = installed.FirstOrDefault(item =>
            item.Culture.TwoLetterISOLanguageName.Equals(
                requestedCulture.TwoLetterISOLanguageName,
                StringComparison.OrdinalIgnoreCase));
        if (languageMatch is not null)
        {
            fallbackWarning =
                $"Speech recognition culture {requestedCulture.Name} is unavailable. Using {languageMatch.Culture.Name}.";
            return languageMatch;
        }

        fallbackWarning =
            $"Speech recognition culture {requestedCulture.Name} is unavailable. Using {installed[0].Culture.Name}.";
        return installed[0];
    }

    private static void LoadRecognitionGrammar(
        SpeechRecognitionEngine engine,
        CultureInfo culture,
        ICollection<SpeechRecognitionDiagnosticEventArgs> initDiagnostics)
    {
        try
        {
            engine.LoadGrammar(new DictationGrammar());
        }
        catch (Exception exception)
        {
            initDiagnostics.Add(new SpeechRecognitionDiagnosticEventArgs(
                "Recognizer initialized",
                "Dictation grammar unavailable; using coach phrase grammar only.",
                exception.Message));
        }

        engine.LoadGrammar(BuildCoachGrammar(culture));
    }

    private static Grammar BuildCoachGrammar(CultureInfo culture)
    {
        var phrases = CoachQueryPhrases.AllRecognitionPhrases.ToArray();
        var choices = new Choices(phrases);
        return new Grammar(new GrammarBuilder(choices) { Culture = culture });
    }

    private void OnSpeechRecognized(object? sender, SpeechRecognizedEventArgs e)
    {
        var text = e.Result?.Text?.Trim() ?? "";
        if (text.Length == 0)
        {
            RaiseDiagnostic("Recognition rejected", "SpeechRecognized returned empty text.");
            return;
        }

        var confidence = e.Result?.Confidence ?? 0f;
        RaiseDiagnostic("Recognition completed", "Final speech recognized.", $"Text='{text}' Confidence={confidence:0.00}");
        SpeechRecognized?.Invoke(this, new SpeechRecognizedResult(text, confidence));
    }

    private void OnSpeechRejected(object? sender, SpeechRecognitionRejectedEventArgs e)
    {
        RaiseDiagnostic(
            "Recognition rejected",
            "Speech was not recognized clearly enough.",
            e.Result?.Confidence.ToString("0.00", CultureInfo.InvariantCulture));
        StatusChanged?.Invoke(this, new SpeechRecognitionStatusChangedEventArgs(
            "Rejected",
            "Speech was not recognized clearly enough."));
    }

    private void OnSpeechDetected(object? sender, SpeechDetectedEventArgs e)
    {
        RaiseDiagnostic("Speech detected", "Audio speech detected.");
    }

    private void OnSpeechHypothesized(object? sender, SpeechHypothesizedEventArgs e)
    {
        var text = e.Result?.Text?.Trim();
        if (string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        RaiseDiagnostic("Speech hypothesis", text);
    }

    private void OnRecognizeCompleted(object? sender, RecognizeCompletedEventArgs e)
    {
        recognizeAsyncActive = false;

        if (e.Error is not null)
        {
            RaiseDiagnostic("Exception", "RecognizeCompleted reported an error.", e.Error.Message);
            StatusChanged?.Invoke(this, new SpeechRecognitionStatusChangedEventArgs("Error", e.Error.Message));
            return;
        }

        if (e.Cancelled)
        {
            RaiseDiagnostic(
                "Recognition canceled",
                stopRequested
                    ? "Recognition canceled after RecognizeAsyncStop/RecognizeAsyncCancel."
                    : "Recognition canceled.");
            StatusChanged?.Invoke(this, new SpeechRecognitionStatusChangedEventArgs(
                "Canceled",
                "Recognition operation canceled."));
            return;
        }

        RaiseDiagnostic("Recognition completed", "RecognizeCompleted fired.");
        if (stopRequested)
        {
            StatusChanged?.Invoke(this, new SpeechRecognitionStatusChangedEventArgs("Idle", "Windows microphone stopped."));
        }
    }

    private void OnAudioSignalProblemOccurred(object? sender, AudioSignalProblemOccurredEventArgs e)
    {
        RaiseDiagnostic("Exception", "Audio signal problem detected.", e.AudioSignalProblem.ToString());
    }

    private void EmitPendingInitializationDiagnostics()
    {
        if (initializationDiagnosticsEmitted || initializationDiagnostics is null)
        {
            return;
        }

        initializationDiagnosticsEmitted = true;
        EmitDiagnostics(initializationDiagnostics);
    }

    private void EmitDiagnostics(IReadOnlyList<SpeechRecognitionDiagnosticEventArgs>? diagnostics)
    {
        if (diagnostics is null)
        {
            return;
        }

        foreach (var diagnostic in diagnostics)
        {
            DiagnosticRaised?.Invoke(this, diagnostic);
        }
    }

    private void RaiseDiagnostic(string stage, string message, string? detail = null)
    {
        DiagnosticRaised?.Invoke(this, new SpeechRecognitionDiagnosticEventArgs(stage, message, detail));
    }
}
