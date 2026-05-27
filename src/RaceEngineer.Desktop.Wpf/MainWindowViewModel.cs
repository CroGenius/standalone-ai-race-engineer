using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.Win32;
using RaceEngineer.Core;
using RaceEngineer.Core.Analytics;
using RaceEngineer.Core.Coaching;
using RaceEngineer.Core.Events;
using RaceEngineer.Core.Knowledge;
using RaceEngineer.Core.Session;
using RaceEngineer.Core.Storage;
using RaceEngineer.Core.Telemetry;
using RaceEngineer.Core.TelemetryVisualization;
using RaceEngineer.Core.Voice;

namespace RaceEngineer.Desktop.Wpf;

public sealed class MainWindowViewModel : INotifyPropertyChanged
{
    private const int SnapshotSampleSeconds = 1;
    private const int MaxLiveTraceSnapshots = 1200;
    private readonly TelemetryReceiver receiver;
    private readonly TelemetryDiagnostics diagnostics;
    private readonly RawPacketCapture rawPacketCapture;
    private readonly PacketReplayTool replayTool = new();
    private readonly EventEngine eventEngine = new();
    private readonly CoachEngine coachEngine = new();
    private readonly TelemetryAnalyticsService analyticsService = new();
    private readonly LapIntelligenceService lapIntelligenceService = new();
    private readonly TelemetryTraceBuilder traceBuilder = new();
    private readonly CoachEvidenceBuilder evidenceBuilder = new();
    private readonly VoiceService voiceService;
    private readonly CalloutManager calloutManager = new();
    private readonly StorageService storageService;
    private readonly StoredResearchService researchService;
    private readonly SessionState session = new();
    private SessionState? reviewSession;
    private IReadOnlyList<TelemetrySnapshot> reviewSnapshots = [];
    private readonly List<TelemetrySnapshot> liveTraceSnapshots = [];
    private bool isReviewMode;
    private IReadOnlyList<string> reviewSessionNotes = [];
    private string sessionLabel;
    private RacePrepPlan? loadedPrepPlan;
    private string chatInput = "";
    private string postSessionNote = "";
    private string prepCar = "";
    private string prepTrack = "";
    private string prepSessionType = "";
    private string prepTargetStintLength = "";
    private string prepFuelPlan = "";
    private string prepTyrePlan = "";
    private string prepPracticeGoal = "";
    private string prepDriverReminders = "";
    private string prepSetupNotes = "";
    private string prepStrategyNotes = "";
    private string lastCallout = "Grounded callouts will appear here.";
    private int packetsReceivedCount;
    private int validPacketsCount;
    private int invalidPacketsCount;
    private DateTimeOffset? lastPacketTimestamp;
    private DateTimeOffset? lastSnapshotSavedAt;
    private string lastParserWarning = "-";
    private int persistedCompletedLapCount;
    private SessionBrowserItem? selectedSession;
    private string loadedSessionSummary = "Select a previous session to load its summary.";
    private string knowledgeTitle = "";
    private string knowledgeCategory = "track";
    private string knowledgeContent = "";
    private string knowledgeConfidence = "";
    private string knowledgeSearch = "";
    private KnowledgeSourceItem? selectedKnowledgeSource;
    private readonly List<string> startupWarnings = [];
    private string analyticsPanelTitle = "Analytics (Live Session)";
    private string analyticsLapConsistency = "-";
    private string analyticsFuelTrend = "-";
    private string analyticsBrakeStability = "-";
    private string analyticsThrottleSmoothness = "-";
    private string analyticsPaceTrend = "-";
    private string analyticsIncidents = "-";
    private string analyticsBestVsAverage = "-";
    private string analyticsDriverProfile = "-";
    private string lapIntelligenceTitle = "Lap Intelligence (Live Session)";
    private string lapIntelligenceBestLap = "-";
    private string lapIntelligenceTheoreticalBest = "-";
    private string lapIntelligencePaceTrend = "-";
    private string lapIntelligenceSectorGainLoss = "-";
    private string lapIntelligenceStrengths = "-";
    private string lapIntelligenceWeaknesses = "-";
    private TelemetryTimeline traceTimeline = TelemetryTimeline.Empty;
    private double timelineCursorProgress;
    private SessionTelemetryAnalytics sessionAnalytics = SessionTelemetryAnalytics.Empty;
    private SessionLapIntelligence sessionLapIntelligence = SessionLapIntelligence.Empty;

    public MainWindowViewModel()
    {
        var settingsResult = AppSettings.Load(Path.Combine(AppContext.BaseDirectory, "appsettings.json"));
        startupWarnings.AddRange(settingsResult.Warnings);
        var settings = settingsResult.Settings;
        Directory.CreateDirectory(settings.CaptureFolder);
        Directory.CreateDirectory(settings.ReplayFolder);

        receiver = new TelemetryReceiver(settings.UdpBindIp, settings.UdpPort);
        diagnostics = new TelemetryDiagnostics(settings.UdpBindIp, settings.UdpPort);
        rawPacketCapture = new RawPacketCapture(Path.Combine(settings.CaptureFolder, "raw-packets.jsonl"));
        voiceService = new VoiceService(WindowsSpeechOutput.CreateOrFallback(startupWarnings.Add));
        voiceService.SetVoiceEnabled(settings.VoiceEnabledDefault);
        storageService = new StorageService(settings.DatabasePath);
        researchService = new StoredResearchService(storageService);
        sessionLabel = $"Session {session.SessionId}";
        SendChatCommand = new RelayCommand(SendChat, () => !string.IsNullOrWhiteSpace(ChatInput));
        SavePrepCommand = new RelayCommand(() => _ = SavePrepAsync());
        LoadPrepCommand = new RelayCommand(() => _ = LoadPrepAsync());
        SavePostSessionNoteCommand = new RelayCommand(SavePostSessionNote, () => !string.IsNullOrWhiteSpace(PostSessionNote));
        RefreshSessionsCommand = new RelayCommand(() => _ = RefreshSessionsAsync());
        LoadSelectedSessionCommand = new RelayCommand(() => _ = LoadSelectedSessionAsync(), () => SelectedSession is not null);
        ExitReviewModeCommand = new RelayCommand(ExitReviewMode, () => IsReviewMode);
        ExportSessionJsonCommand = new RelayCommand(() => _ = ExportSelectedSessionJsonAsync(), () => SelectedSession is not null);
        ExportCoachingMarkdownCommand = new RelayCommand(() => _ = ExportSelectedSessionMarkdownAsync(), () => SelectedSession is not null);
        TogglePushToTalkCommand = new RelayCommand(TogglePushToTalk);
        ToggleTtsCommand = new RelayCommand(ToggleTts);
        ToggleVoiceCommand = new RelayCommand(ToggleVoice);
        TestVoiceCommand = new RelayCommand(TestVoice);
        ToggleRawCaptureCommand = new RelayCommand(ToggleRawCapture);
        ReplayCaptureCommand = new RelayCommand(ReplayCapture);
        SaveKnowledgeCommand = new RelayCommand(() => _ = SaveKnowledgeAsync(), () => !string.IsNullOrWhiteSpace(KnowledgeTitle) && !string.IsNullOrWhiteSpace(KnowledgeContent));
        SearchKnowledgeCommand = new RelayCommand(() => _ = SearchKnowledgeAsync());
        ImportKnowledgeCommand = new RelayCommand(() => _ = ImportKnowledgeAsync());
        DeleteKnowledgeCommand = new RelayCommand(() => _ = DeleteKnowledgeAsync(), () => SelectedKnowledgeSource is not null);
        receiver.PacketProcessed += OnPacketProcessed;
        receiver.SnapshotReceived += OnSnapshotReceived;
        _ = StartAsync();
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public ObservableCollection<string> ChatMessages { get; } = [];
    public ObservableCollection<EventLogItem> EventLog { get; } = [];
    public ObservableCollection<SessionBrowserItem> Sessions { get; } = [];
    public ObservableCollection<KnowledgeSourceItem> KnowledgeSources { get; } = [];
    public string SessionLabel
    {
        get => sessionLabel;
        private set => SetField(ref sessionLabel, value);
    }

    public bool IsReviewMode => isReviewMode;

    public string ReviewModeBanner => isReviewMode
        ? "Review Mode — Loaded Session (read-only; live telemetry continues in background)"
        : "";

    public string AnalyticsPanelTitle => analyticsPanelTitle;
    public string AnalyticsLapConsistency => analyticsLapConsistency;
    public string AnalyticsFuelTrend => analyticsFuelTrend;
    public string AnalyticsBrakeStability => analyticsBrakeStability;
    public string AnalyticsThrottleSmoothness => analyticsThrottleSmoothness;
    public string AnalyticsPaceTrend => analyticsPaceTrend;
    public string AnalyticsIncidents => analyticsIncidents;
    public string AnalyticsBestVsAverage => analyticsBestVsAverage;
    public string AnalyticsDriverProfile => analyticsDriverProfile;

    public string LapIntelligenceTitle => lapIntelligenceTitle;
    public string LapIntelligenceBestLap => lapIntelligenceBestLap;
    public string LapIntelligenceTheoreticalBest => lapIntelligenceTheoreticalBest;
    public string LapIntelligencePaceTrend => lapIntelligencePaceTrend;
    public string LapIntelligenceSectorGainLoss => lapIntelligenceSectorGainLoss;
    public string LapIntelligenceStrengths => lapIntelligenceStrengths;
    public string LapIntelligenceWeaknesses => lapIntelligenceWeaknesses;

    public TelemetryTimeline TraceTimeline => traceTimeline;

    public double TimelineCursorProgress
    {
        get => timelineCursorProgress;
        set => SetField(ref timelineCursorProgress, Math.Clamp(value, 0, 1));
    }

    public ICommand ExitReviewModeCommand { get; }
    public ICommand SendChatCommand { get; }
    public ICommand SavePrepCommand { get; }
    public ICommand LoadPrepCommand { get; }
    public ICommand SavePostSessionNoteCommand { get; }
    public ICommand RefreshSessionsCommand { get; }
    public ICommand LoadSelectedSessionCommand { get; }
    public ICommand ExportSessionJsonCommand { get; }
    public ICommand ExportCoachingMarkdownCommand { get; }
    public ICommand TogglePushToTalkCommand { get; }
    public ICommand ToggleTtsCommand { get; }
    public ICommand ToggleVoiceCommand { get; }
    public ICommand TestVoiceCommand { get; }
    public ICommand ToggleRawCaptureCommand { get; }
    public ICommand ReplayCaptureCommand { get; }
    public ICommand SaveKnowledgeCommand { get; }
    public ICommand SearchKnowledgeCommand { get; }
    public ICommand ImportKnowledgeCommand { get; }
    public ICommand DeleteKnowledgeCommand { get; }

    public string TelemetryStatus => isReviewMode
        ? "Review mode"
        : ActiveSession.TelemetryOnline ? "Telemetry online" : "Telemetry offline";
    public Brush TelemetryStatusBrush => isReviewMode
        ? Brushes.DeepSkyBlue
        : ActiveSession.TelemetryOnline ? Brushes.LightGreen : Brushes.Orange;
    public int PacketsReceivedCount => packetsReceivedCount;
    public int ValidPacketsCount => validPacketsCount;
    public int InvalidPacketsCount => invalidPacketsCount;
    public string LastPacketTimestamp => lastPacketTimestamp?.ToLocalTime().ToString("HH:mm:ss.fff", CultureInfo.InvariantCulture) ?? "-";
    public string LastParserWarning => lastParserWarning;
    public string UdpBind => $"{diagnostics.BindAddress}:{diagnostics.Port}";
    public string ReceiverState => diagnostics.ReceiverRunning ? "Running" : "Stopped";
    public string PacketsPerSecond => diagnostics.PacketsPerSecond.ToString("0", CultureInfo.InvariantCulture);
    public string ValidPacketsPerSecond => diagnostics.ValidPacketsPerSecond.ToString("0", CultureInfo.InvariantCulture);
    public string InvalidPacketsPerSecond => diagnostics.InvalidPacketsPerSecond.ToString("0", CultureInfo.InvariantCulture);
    public string LastValidPacketAge => diagnostics.LastValidPacketAge.HasValue ? FormatDuration(diagnostics.LastValidPacketAge.Value) : "-";
    public string LastInvalidReason => diagnostics.LastInvalidReason ?? "-";
    public string CurrentSchemaSeen => diagnostics.CurrentSchema is null ? "-" : $"{diagnostics.CurrentSchema} v{diagnostics.CurrentSchemaVersion?.ToString(CultureInfo.InvariantCulture) ?? "?"}";
    public string RawCaptureState => rawPacketCapture.Enabled ? "Capture On" : "Capture Off";
    public string RawCapturePath => rawPacketCapture.FilePath;
    public string Speed => Format(ActiveSession.LatestSnapshot?.Car.SpeedKmh, "0.0 km/h");
    public string Rpm => Format(ActiveSession.LatestSnapshot?.Car.Rpm, "0");
    public string Gear => ActiveSession.LatestSnapshot?.Car.Gear?.ToString(CultureInfo.InvariantCulture) ?? "-";
    public string Throttle => Format(ActiveSession.LatestSnapshot?.Inputs.Throttle, "0.00");
    public string Brake => Format(ActiveSession.LatestSnapshot?.Inputs.Brake, "0.00");
    public string Steering => Format(ActiveSession.LatestSnapshot?.Inputs.Steering, "0.00");
    public string LapTime => Format(ActiveSession.LatestSnapshot?.Lap.LapTimeS, "0.000 s");
    public string LapProgress => Format(ActiveSession.LatestSnapshot?.Lap.LapProgress, "0.000");
    public string Fuel => Format(ActiveSession.LatestSnapshot?.Condition.Fuel, "0.0");
    public string Position => ActiveSession.LatestSnapshot?.Race.Position?.ToString(CultureInfo.InvariantCulture) ?? "-";
    public string VoiceLabel => voiceService.VoiceEnabled ? "Voice On" : "Voice Off";
    public string TtsLabel => voiceService.EngineerMuted ? "Engineer Muted" : "Engineer Audible";
    public string VoiceState => $"{voiceService.StateText} / {voiceService.MuteText}";
    public string VoiceSuppressionState => calloutManager.LastSuppressionState;
    public string CurrentLap => ActiveSession.CurrentLap.ToString(CultureInfo.InvariantCulture);
    public string LastLapTime => FormatDuration(ActiveSession.LastLap?.Duration);
    public string BestLapTime => FormatDuration(ActiveSession.BestLap?.Duration);
    public string CompletedLapsCount => ActiveSession.CompletedLaps.Count.ToString(CultureInfo.InvariantCulture);
    public string StintTime => FormatDuration(ActiveSession.StintDuration);
    public string EstimatedLapsRemaining => Format(ActiveSession.EstimatedLapsRemaining, "0.0");
    public string LoadedSessionSummary
    {
        get => loadedSessionSummary;
        private set => SetField(ref loadedSessionSummary, value);
    }

    public SessionBrowserItem? SelectedSession
    {
        get => selectedSession;
        set
        {
            if (SetField(ref selectedSession, value))
            {
                RaiseSessionBrowserCommands();
            }
        }
    }

    public string ChatInput
    {
        get => chatInput;
        set
        {
            if (SetField(ref chatInput, value))
            {
                (SendChatCommand as RelayCommand)?.RaiseCanExecuteChanged();
            }
        }
    }

    public string PrepCar { get => prepCar; set => SetField(ref prepCar, value); }
    public string PrepTrack { get => prepTrack; set => SetField(ref prepTrack, value); }
    public string PrepSessionType { get => prepSessionType; set => SetField(ref prepSessionType, value); }
    public string PrepTargetStintLength { get => prepTargetStintLength; set => SetField(ref prepTargetStintLength, value); }
    public string PrepFuelPlan { get => prepFuelPlan; set => SetField(ref prepFuelPlan, value); }
    public string PrepTyrePlan { get => prepTyrePlan; set => SetField(ref prepTyrePlan, value); }
    public string PrepPracticeGoal { get => prepPracticeGoal; set => SetField(ref prepPracticeGoal, value); }
    public string PrepDriverReminders { get => prepDriverReminders; set => SetField(ref prepDriverReminders, value); }
    public string PrepSetupNotes { get => prepSetupNotes; set => SetField(ref prepSetupNotes, value); }
    public string PrepStrategyNotes { get => prepStrategyNotes; set => SetField(ref prepStrategyNotes, value); }
    public string KnowledgeTitle
    {
        get => knowledgeTitle;
        set
        {
            if (SetField(ref knowledgeTitle, value))
            {
                (SaveKnowledgeCommand as RelayCommand)?.RaiseCanExecuteChanged();
            }
        }
    }

    public string KnowledgeCategory { get => knowledgeCategory; set => SetField(ref knowledgeCategory, value); }

    public string KnowledgeContent
    {
        get => knowledgeContent;
        set
        {
            if (SetField(ref knowledgeContent, value))
            {
                (SaveKnowledgeCommand as RelayCommand)?.RaiseCanExecuteChanged();
            }
        }
    }

    public string KnowledgeConfidence { get => knowledgeConfidence; set => SetField(ref knowledgeConfidence, value); }
    public string KnowledgeSearch { get => knowledgeSearch; set => SetField(ref knowledgeSearch, value); }

    public KnowledgeSourceItem? SelectedKnowledgeSource
    {
        get => selectedKnowledgeSource;
        set
        {
            if (SetField(ref selectedKnowledgeSource, value))
            {
                (DeleteKnowledgeCommand as RelayCommand)?.RaiseCanExecuteChanged();
            }
        }
    }

    public string PostSessionNote
    {
        get => postSessionNote;
        set
        {
            if (SetField(ref postSessionNote, value))
            {
                (SavePostSessionNoteCommand as RelayCommand)?.RaiseCanExecuteChanged();
            }
        }
    }

    public string LastCallout
    {
        get => lastCallout;
        private set => SetField(ref lastCallout, value);
    }

    public string SessionSummary
    {
        get
        {
            var counts = ActiveSession.RecentEvents
                .GroupBy(item => item.Type)
                .OrderByDescending(group => group.Count())
                .Select(group => $"{group.Key}: {group.Count()}");
            return ActiveSession.RecentEvents.Count == 0
                ? isReviewMode
                    ? "No events were recorded for this loaded session."
                    : "Complete a run to generate recurring mistakes, tyre/brake/fuel summary, and an improvement plan."
                : string.Join(Environment.NewLine, counts);
        }
    }

    private SessionState ActiveSession => reviewSession ?? session;

    private async Task StartAsync()
    {
        foreach (var warning in startupWarnings)
        {
            ChatMessages.Add($"Startup: {warning}");
        }

        try
        {
            await storageService.InitializeAsync();
            await storageService.CreateSessionAsync(session.SessionId, session.StartedAt);
            await RefreshSessionsAsync();
            await RefreshKnowledgeAsync();
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or Microsoft.Data.Sqlite.SqliteException)
        {
            ChatMessages.Add($"Startup: Database could not be opened. {exception.Message}");
        }

        try
        {
            await receiver.StartAsync();
            diagnostics.SetReceiverRunning(receiver.IsRunning);
        }
        catch (InvalidOperationException exception)
        {
            diagnostics.SetReceiverRunning(false);
            ChatMessages.Add($"Startup: {exception.Message}");
            lastParserWarning = exception.Message;
        }

        RaiseDiagnosticsProperties();
        RaiseVoiceProperties();
        RefreshAnalytics();
    }

    private void OnPacketProcessed(object? sender, TelemetryPacketResult packet)
    {
        Application.Current.Dispatcher.Invoke(() => ProcessPacketResult(packet, recordCapture: true));
    }

    private void ProcessPacketResult(TelemetryPacketResult packet, bool recordCapture)
    {
        packetsReceivedCount++;
        lastPacketTimestamp = packet.Timestamp;
        diagnostics.Record(packet);
        if (recordCapture)
        {
            rawPacketCapture.Record(packet);
        }

        if (packet.IsValid)
        {
            validPacketsCount++;
        }
        else
        {
            invalidPacketsCount++;
            lastParserWarning = packet.Warning ?? "Packet rejected.";
            if (!isReviewMode)
            {
                EventLog.Insert(0, EventLogItem.ParserWarning(lastParserWarning));
            }
        }

        if (!isReviewMode)
        {
            while (EventLog.Count > 100)
            {
                EventLog.RemoveAt(EventLog.Count - 1);
            }
        }

        RaisePacketDebugProperties();
        RaiseDiagnosticsProperties();
    }

    private void OnSnapshotReceived(object? sender, TelemetrySnapshot snapshot)
    {
        ProcessReceivedSnapshot(snapshot);
    }

    private int ProcessReceivedSnapshot(TelemetrySnapshot snapshot)
    {
        var events = eventEngine.Process(snapshot);
        var completedLapsBefore = session.CompletedLaps.Count;
        session.ApplySnapshot(snapshot, events);
        if (ShouldPersistSnapshot(snapshot, events))
        {
            lastSnapshotSavedAt = snapshot.Timestamp;
            _ = storageService.SaveSnapshotAsync(session.SessionId, snapshot);
        }

        _ = storageService.SaveEventsAsync(session.SessionId, events);
        foreach (var completedLap in session.CompletedLaps.Skip(completedLapsBefore))
        {
            persistedCompletedLapCount++;
            _ = storageService.SaveCompletedLapAsync(session.SessionId, completedLap);
        }

        if (!isReviewMode)
        {
            liveTraceSnapshots.Add(snapshot);
            while (liveTraceSnapshots.Count > MaxLiveTraceSnapshots)
            {
                liveTraceSnapshots.RemoveAt(0);
            }
        }

        Application.Current.Dispatcher.Invoke(() =>
        {
            if (!isReviewMode)
            {
                foreach (var item in events)
                {
                    EventLog.Insert(0, EventLogItem.FromEvent(item));
                }

                while (EventLog.Count > 100)
                {
                    EventLog.RemoveAt(EventLog.Count - 1);
                }

                var callout = coachEngine.ChooseLiveCallout(events);
                var spoken = false;
                foreach (var item in events.OrderBy(VoicePriority))
                {
                    var voiceCallout = calloutManager.TryCreateCallout(item);
                    if (voiceCallout is not null)
                    {
                        spoken = voiceService.Speak(voiceCallout);
                        if (spoken)
                        {
                            LastCallout = voiceCallout;
                        }

                        break;
                    }
                }

                if (!spoken && callout is not null)
                {
                    LastCallout = callout.Content;
                }

                if (snapshot.Lap.LapProgress is { } progress)
                {
                    timelineCursorProgress = progress > 1.0 && progress <= 100.0
                        ? progress / 100.0
                        : Math.Clamp(progress, 0, 1);
                    OnPropertyChanged(nameof(TimelineCursorProgress));
                }

                RaiseTelemetryProperties();
            }

            RaiseVoiceProperties();
        });

        return events.Count;
    }

    private void SendChat()
    {
        var message = ChatInput.Trim();
        if (message.Length == 0)
        {
            return;
        }

        ChatInput = "";
        ChatMessages.Add(isReviewMode ? $"You (review): {message}" : $"You: {message}");
        var evidence = BuildCoachEvidence();
        var answer = coachEngine.Answer(ActiveSession, message, CurrentCoachContext(), evidence);
        AppendCoachChatLines(answer);
    }

    private CoachEvidenceBundle BuildCoachEvidence()
    {
        return evidenceBuilder.Build(new CoachEvidenceInput(
            ActiveSession,
            ActiveTraceSnapshots,
            ActiveSession.Events,
            sessionAnalytics,
            sessionLapIntelligence,
            traceTimeline,
            KnowledgeSources.Select(item => item.Source).ToArray()));
    }

    private void AppendCoachChatLines(CoachMessage answer)
    {
        var prefix = isReviewMode ? "Coach (review):" : "Coach:";
        var uncertainty = string.IsNullOrWhiteSpace(answer.Uncertainty) ? "" : $" ({answer.Uncertainty})";
        ChatMessages.Add($"{prefix} {answer.Content}{uncertainty}");
        if (answer.EvidencePackets.Count > 0)
        {
            var bullets = string.Join(
                Environment.NewLine,
                answer.EvidencePackets.Select(packet => $"- {packet.Summary}: {packet.Explanation}"));
            ChatMessages.Add($"{prefix}{Environment.NewLine}Evidence:{Environment.NewLine}{bullets}");
        }
    }

    private async Task SavePrepAsync()
    {
        loadedPrepPlan = CurrentPrepPlan();
        await storageService.SaveRacePrepPlanAsync(loadedPrepPlan);
        await storageService.CreateSessionAsync(session.SessionId, session.StartedAt, loadedPrepPlan.Car, loadedPrepPlan.Track);
        ChatMessages.Add("Coach: Structured race prep saved.");
    }

    private async Task LoadPrepAsync()
    {
        loadedPrepPlan = await storageService.LoadRacePrepPlanAsync(PrepCar, PrepTrack, PrepSessionType);
        if (loadedPrepPlan is null)
        {
            ChatMessages.Add("Coach: No structured prep found for that car/track.");
            return;
        }

        ApplyPrepPlan(loadedPrepPlan);
        ChatMessages.Add("Coach: Structured race prep loaded.");
    }

    private void SavePostSessionNote()
    {
        var note = PostSessionNote.Trim();
        if (note.Length == 0)
        {
            return;
        }

        PostSessionNote = "";
        var targetSessionId = isReviewMode ? reviewSession!.SessionId : session.SessionId;
        ChatMessages.Add(isReviewMode
            ? "Coach (review): Note saved to the loaded session."
            : "Coach: Post-session note saved.");
        _ = storageService.AddNoteAsync(targetSessionId, "post_session", note);
    }

    public async Task StopAsync()
    {
        receiver.Stop();
        diagnostics.SetReceiverRunning(false);
        var summaryMarkdown = coachEngine.GeneratePostSessionReport(session, CurrentPrepPlan());
        await storageService.SavePostSessionReportAsync(session.SessionId, summaryMarkdown);
        await storageService.SavePostSessionSummaryAsync(session.SessionId, summaryMarkdown, CurrentSessionSummaryObject());
        await storageService.EndSessionAsync(session.SessionId, DateTimeOffset.UtcNow, summaryMarkdown, CurrentSessionSummaryObject());
        await RefreshSessionsAsync();
    }

    private async Task RefreshSessionsAsync()
    {
        var rows = await storageService.ListSessionsAsync();
        Application.Current.Dispatcher.Invoke(() =>
        {
            Sessions.Clear();
            foreach (var row in rows)
            {
                Sessions.Add(new SessionBrowserItem(row));
            }
        });
    }

    private async Task LoadSelectedSessionAsync()
    {
        if (SelectedSession is null)
        {
            return;
        }

        var bundle = await storageService.LoadSessionReviewBundleAsync(SelectedSession.SessionId);
        if (bundle is null)
        {
            LoadedSessionSummary = "Selected session could not be loaded.";
            ChatMessages.Add("Coach: Selected session could not be loaded from storage.");
            return;
        }

        reviewSession = SessionState.FromPersisted(
            bundle.SessionId,
            bundle.StartedAt,
            bundle.Snapshots,
            bundle.Events,
            bundle.CompletedLaps);
        reviewSnapshots = bundle.Snapshots;
        reviewSessionNotes = bundle.Notes.Select(note => $"{note.Kind}: {note.Content}").ToArray();
        isReviewMode = true;
        LoadedSessionSummary = bundle.SummaryMarkdown;

        var car = bundle.Car ?? "";
        var track = bundle.Track ?? "";
        var startedLabel = bundle.StartedAt.LocalDateTime.ToString("g", CultureInfo.CurrentCulture);
        SessionLabel = $"Review Mode | {startedLabel} | {car} | {track}".Trim(' ', '|');

        loadedPrepPlan = await storageService.LoadRacePrepPlanAsync(bundle.Car, bundle.Track);
        if (loadedPrepPlan is not null)
        {
            ApplyPrepPlan(loadedPrepPlan);
        }
        else if (!string.IsNullOrWhiteSpace(car) || !string.IsNullOrWhiteSpace(track))
        {
            PrepCar = car;
            PrepTrack = track;
            await RefreshKnowledgeAsync();
        }

        Application.Current.Dispatcher.Invoke(() =>
        {
            EventLog.Clear();
            foreach (var item in bundle.Events.OrderByDescending(evt => evt.Timestamp))
            {
                EventLog.Insert(0, EventLogItem.FromEvent(item));
            }

            while (EventLog.Count > 100)
            {
                EventLog.RemoveAt(EventLog.Count - 1);
            }
        });

        ChatMessages.Add($"Coach (review): Loaded session from {startedLabel}. Ask about laps, events, fuel, or session summary.");
        RaiseReviewModeProperties();
        RaiseTelemetryProperties();
        (ExitReviewModeCommand as RelayCommand)?.RaiseCanExecuteChanged();
    }

    private void ExitReviewMode()
    {
        if (!isReviewMode)
        {
            return;
        }

        isReviewMode = false;
        reviewSession = null;
        reviewSnapshots = [];
        reviewSessionNotes = [];
        SessionLabel = $"Session {session.SessionId}";
        LoadedSessionSummary = "Select a previous session to load it for review.";
        Application.Current.Dispatcher.Invoke(EventLog.Clear);
        ChatMessages.Add("Coach: Exited review mode. Live session restored.");
        RaiseReviewModeProperties();
        RaiseTelemetryProperties();
        (ExitReviewModeCommand as RelayCommand)?.RaiseCanExecuteChanged();
    }

    private async Task ExportSelectedSessionJsonAsync()
    {
        if (SelectedSession is null)
        {
            return;
        }

        var path = await storageService.ExportSessionJsonAsync(SelectedSession.SessionId);
        LoadedSessionSummary = $"Exported session JSON:\n{path}";
    }

    private async Task ExportSelectedSessionMarkdownAsync()
    {
        if (SelectedSession is null)
        {
            return;
        }

        var path = await storageService.ExportCoachingMarkdownAsync(SelectedSession.SessionId);
        LoadedSessionSummary = $"Exported coaching Markdown:\n{path}";
    }

    private void TogglePushToTalk()
    {
        voiceService.SetPushToTalk(!voiceService.PushToTalkEnabled);
        if (voiceService.PushToTalkEnabled && !string.IsNullOrWhiteSpace(ChatInput))
        {
            var query = ChatInput.Trim();
            ChatInput = "";
            ChatMessages.Add($"You voice: {query}");
            var result = voiceService.HandleSpokenQuery(ActiveSession, query, coachEngine, CurrentCoachContext(), BuildCoachEvidence());
            AppendCoachChatLines(result.WrittenResponse);
            LastCallout = result.SpokenResponse;
        }

        RaiseVoiceProperties();
    }

    private void ToggleTts()
    {
        voiceService.SetMuted(!voiceService.EngineerMuted);
        RaiseVoiceProperties();
    }

    private void ToggleVoice()
    {
        voiceService.SetVoiceEnabled(!voiceService.VoiceEnabled);
        RaiseVoiceProperties();
    }

    private void TestVoice()
    {
        voiceService.TestVoiceOutput();
        LastCallout = voiceService.LastSpokenCallout;
        RaiseVoiceProperties();
    }

    private void ToggleRawCapture()
    {
        rawPacketCapture.SetEnabled(!rawPacketCapture.Enabled);
        RaiseDiagnosticsProperties();
    }

    private void ReplayCapture()
    {
        if (!File.Exists(rawPacketCapture.FilePath))
        {
            ChatMessages.Add("Coach: No raw packet capture file exists yet.");
            return;
        }

        var replayEvents = 0;
        var result = replayTool.Replay(
            rawPacketCapture.FilePath,
            onPacketProcessed: packet => RunOnUiThread(() => ProcessPacketResult(packet, recordCapture: false)),
            onSnapshotReceived: snapshot => replayEvents += ProcessReceivedSnapshot(snapshot));
        ChatMessages.Add($"Replay: {result.PacketsRead} packets, {result.ValidPackets} valid, {result.InvalidPackets} invalid, {replayEvents} events.");
    }

    private static void RunOnUiThread(Action action)
    {
        if (Application.Current.Dispatcher.CheckAccess())
        {
            action();
        }
        else
        {
            Application.Current.Dispatcher.Invoke(action);
        }
    }

    private async Task SaveKnowledgeAsync()
    {
        var source = KnowledgeSource.ManualNote(
            KnowledgeTitle.Trim(),
            KnowledgeContent.Trim(),
            EmptyToNull(PrepCar),
            EmptyToNull(PrepTrack),
            EmptyToNull(PrepSessionType),
            EmptyToNull(KnowledgeCategory),
            EmptyToNull(KnowledgeConfidence));
        await storageService.SaveKnowledgeSourceAsync(source);
        KnowledgeContent = "";
        ChatMessages.Add("Coach: Track/car knowledge saved as an offline source.");
        await RefreshKnowledgeAsync();
    }

    private async Task SearchKnowledgeAsync()
    {
        var sources = await researchService.SearchAsync(KnowledgeSearch);
        SetKnowledgeSources(sources);
        ChatMessages.Add(sources.Count == 0
            ? "Coach: No stored knowledge matched that search. External research is unavailable in offline mode."
            : $"Coach: Found {sources.Count} stored knowledge source(s).");
    }

    private async Task ImportKnowledgeAsync()
    {
        var dialog = new OpenFileDialog
        {
            Filter = "Knowledge files (*.md;*.markdown;*.json;*.txt)|*.md;*.markdown;*.json;*.txt|All files (*.*)|*.*"
        };
        if (dialog.ShowDialog() != true)
        {
            return;
        }

        var source = await storageService.ImportKnowledgeFileAsync(
            dialog.FileName,
            EmptyToNull(PrepCar),
            EmptyToNull(PrepTrack),
            EmptyToNull(PrepSessionType),
            EmptyToNull(KnowledgeCategory));
        ChatMessages.Add($"Coach: Imported knowledge source '{source.Title}'.");
        await RefreshKnowledgeAsync();
    }

    private async Task DeleteKnowledgeAsync()
    {
        if (SelectedKnowledgeSource is null)
        {
            return;
        }

        await storageService.DeleteKnowledgeSourceAsync(SelectedKnowledgeSource.Id);
        ChatMessages.Add("Coach: Deleted selected knowledge source.");
        SelectedKnowledgeSource = null;
        await RefreshKnowledgeAsync();
    }

    private async Task RefreshKnowledgeAsync()
    {
        var sources = await storageService.ListKnowledgeSourcesAsync(
            EmptyToNull(PrepCar),
            EmptyToNull(PrepTrack),
            EmptyToNull(PrepSessionType),
            null);
        SetKnowledgeSources(sources);
    }

    private void SetKnowledgeSources(IReadOnlyList<KnowledgeSource> sources)
    {
        Application.Current.Dispatcher.Invoke(() =>
        {
            KnowledgeSources.Clear();
            foreach (var source in sources)
            {
                KnowledgeSources.Add(new KnowledgeSourceItem(source));
            }
        });
    }

    private void RaiseTelemetryProperties()
    {
        OnPropertyChanged(nameof(TelemetryStatus));
        OnPropertyChanged(nameof(TelemetryStatusBrush));
        OnPropertyChanged(nameof(Speed));
        OnPropertyChanged(nameof(Rpm));
        OnPropertyChanged(nameof(Gear));
        OnPropertyChanged(nameof(Throttle));
        OnPropertyChanged(nameof(Brake));
        OnPropertyChanged(nameof(Steering));
        OnPropertyChanged(nameof(LapTime));
        OnPropertyChanged(nameof(LapProgress));
        OnPropertyChanged(nameof(Fuel));
        OnPropertyChanged(nameof(Position));
        OnPropertyChanged(nameof(SessionSummary));
        OnPropertyChanged(nameof(CurrentLap));
        OnPropertyChanged(nameof(LastLapTime));
        OnPropertyChanged(nameof(BestLapTime));
        OnPropertyChanged(nameof(CompletedLapsCount));
        OnPropertyChanged(nameof(StintTime));
        OnPropertyChanged(nameof(EstimatedLapsRemaining));
        OnPropertyChanged(nameof(VoiceSuppressionState));
        RefreshAnalytics();
        RefreshTraceTimeline();
    }

    private IReadOnlyList<TelemetrySnapshot> ActiveTraceSnapshots =>
        isReviewMode ? reviewSnapshots : liveTraceSnapshots;

    private void RefreshTraceTimeline()
    {
        traceTimeline = traceBuilder.Build(new TelemetryTimelineInput(
            ActiveSession,
            ActiveTraceSnapshots,
            ActiveSession.Events,
            isReviewMode ? reviewSession?.LastLap?.LapNumber : session.LastLap?.LapNumber,
            timelineCursorProgress));
        OnPropertyChanged(nameof(TraceTimeline));
    }

    private void RefreshAnalytics()
    {
        analyticsPanelTitle = isReviewMode ? "Analytics (Review Session)" : "Analytics (Live Session)";
        sessionAnalytics = analyticsService.Analyze(new SessionAnalyticsInput(
            ActiveSession,
            ActiveTraceSnapshots.Count >= 2 ? ActiveTraceSnapshots : null));
        var metrics = sessionAnalytics;

        analyticsLapConsistency = metrics.LapConsistency.Score0To100 is { } consistency
            ? $"{consistency:0} / 100 (σ {metrics.LapConsistency.StandardDeviationSeconds:0.000}s, n={metrics.LapConsistency.SampleCount})"
            : metrics.LapConsistency.Availability;

        analyticsFuelTrend = metrics.FuelTrend.FuelPerLap is { } fuelPerLap
            ? $"{fuelPerLap:0.00}/lap, stint {metrics.FuelTrend.EstimatedStintFuelUsed?.ToString("0.00", CultureInfo.InvariantCulture) ?? "-"}, est. {metrics.FuelTrend.EstimatedLapsRemaining?.ToString("0.0", CultureInfo.InvariantCulture) ?? "-"} laps — {metrics.FuelTrend.TrendLabel}"
            : metrics.FuelTrend.Availability;

        analyticsBrakeStability = metrics.BrakeStability.Score0To100 is { } brakeScore
            ? $"{brakeScore:0} / 100 (unstable {metrics.BrakeStability.UnstableBrakingCount}, abrupt {metrics.BrakeStability.AbruptReleaseCount})"
            : metrics.BrakeStability.Availability;

        analyticsThrottleSmoothness = metrics.ThrottleSmoothness.Score0To100 is { } throttleScore
            ? $"{throttleScore:0} / 100 (hesitation {metrics.ThrottleSmoothness.HesitationCount}, early {metrics.ThrottleSmoothness.EarlyThrottleCount})"
            : metrics.ThrottleSmoothness.Availability;

        analyticsPaceTrend = metrics.PaceTrend.RecentAverageSeconds is { } recent && metrics.PaceTrend.PriorAverageSeconds is { } prior
            ? $"{metrics.PaceTrend.TrendLabel} (Δ {metrics.PaceTrend.DeltaSeconds:+0.000;-0.000;0.000}s over last {metrics.PaceTrend.LapWindow}, {prior:0.000}s → {recent:0.000}s)"
            : metrics.PaceTrend.Availability;

        analyticsIncidents = metrics.Incidents.TotalIncidents == 0
            ? metrics.Incidents.Availability
            : $"{metrics.Incidents.TotalIncidents} total ({metrics.Incidents.IncidentsPerLap:0.00}/lap) — {FormatIncidentCounts(metrics.Incidents.CountsByType)}";

        analyticsBestVsAverage = metrics.BestVsAverage.BestLapSeconds is { } best && metrics.BestVsAverage.AverageLapSeconds is { } average
            ? $"best {best:0.000}s vs avg {average:0.000}s (Δ +{metrics.BestVsAverage.DeltaSeconds:0.000}s)"
            : metrics.BestVsAverage.Availability;

        analyticsDriverProfile = metrics.DriverProfile.Strengths.Count == 0 && metrics.DriverProfile.Weaknesses.Count == 0
            ? "Collect more laps for strengths/weaknesses."
            : $"Strengths: {FormatProfileItems(metrics.DriverProfile.Strengths)} | Weaknesses: {FormatProfileItems(metrics.DriverProfile.Weaknesses)}";

        lapIntelligenceTitle = isReviewMode ? "Lap Intelligence (Review Session)" : "Lap Intelligence (Live Session)";
        sessionLapIntelligence = lapIntelligenceService.Analyze(new LapIntelligenceInput(
            ActiveSession,
            ActiveTraceSnapshots.Count >= 2 ? ActiveTraceSnapshots : null));
        var intelligence = sessionLapIntelligence;

        lapIntelligenceBestLap = intelligence.LapComparison.BestLapSeconds is { } intelBest
            ? $"Lap {intelligence.LapComparison.BestLapNumber}: {intelBest:0.000}s vs selected lap {intelligence.LapComparison.SelectedLapNumber}: {intelligence.LapComparison.SelectedLapSeconds:0.000}s (Δ {intelligence.LapComparison.DeltaSeconds:+0.000;-0.000;0.000}s)"
            : intelligence.LapComparison.Availability;

        lapIntelligenceTheoreticalBest = intelligence.TheoreticalBest.TheoreticalSeconds is { } theoretical
            ? $"Theoretical {theoretical:0.000}s vs actual best {intelligence.TheoreticalBest.ActualBestSeconds:0.000}s (gap {intelligence.TheoreticalBest.DeltaSeconds:0.000}s)"
            : intelligence.TheoreticalBest.Availability;

        lapIntelligencePaceTrend = intelligence.PaceDecay.FirstHalfAverageSeconds is { } firstHalf && intelligence.PaceDecay.SecondHalfAverageSeconds is { } secondHalf
            ? $"{intelligence.PaceDecay.TrendLabel} stint ({firstHalf:0.000}s → {secondHalf:0.000}s, Δ {intelligence.PaceDecay.DeltaSeconds:+0.000;-0.000;0.000}s)"
            : intelligence.PaceDecay.Availability;

        lapIntelligenceSectorGainLoss = intelligence.SectorDeltas.Sectors.Count == 0
            ? intelligence.SectorDeltas.Availability
            : string.Join(" | ", intelligence.SectorDeltas.Sectors.Select(sector =>
                $"{sector.SectorName}: {sector.GainLossLabel} ({sector.DeltaSeconds:+0.000;-0.000;0.000}s)"));

        lapIntelligenceStrengths = intelligence.Strengths.Count == 0
            ? "No strengths identified yet."
            : string.Join(" ", intelligence.Strengths);

        lapIntelligenceWeaknesses = intelligence.Weaknesses.Count == 0
            ? "No weaknesses identified yet."
            : string.Join(" ", intelligence.Weaknesses);

        RaiseAnalyticsProperties();
    }

    private static string FormatIncidentCounts(IReadOnlyDictionary<string, int> counts)
    {
        return counts.Count == 0
            ? "none"
            : string.Join(", ", counts.Select(pair => $"{pair.Key}: {pair.Value}"));
    }

    private static string FormatProfileItems(IReadOnlyList<string> items)
    {
        return items.Count == 0 ? "none" : string.Join("; ", items);
    }

    private void RaiseAnalyticsProperties()
    {
        OnPropertyChanged(nameof(AnalyticsPanelTitle));
        OnPropertyChanged(nameof(AnalyticsLapConsistency));
        OnPropertyChanged(nameof(AnalyticsFuelTrend));
        OnPropertyChanged(nameof(AnalyticsBrakeStability));
        OnPropertyChanged(nameof(AnalyticsThrottleSmoothness));
        OnPropertyChanged(nameof(AnalyticsPaceTrend));
        OnPropertyChanged(nameof(AnalyticsIncidents));
        OnPropertyChanged(nameof(AnalyticsBestVsAverage));
        OnPropertyChanged(nameof(AnalyticsDriverProfile));
        OnPropertyChanged(nameof(LapIntelligenceTitle));
        OnPropertyChanged(nameof(LapIntelligenceBestLap));
        OnPropertyChanged(nameof(LapIntelligenceTheoreticalBest));
        OnPropertyChanged(nameof(LapIntelligencePaceTrend));
        OnPropertyChanged(nameof(LapIntelligenceSectorGainLoss));
        OnPropertyChanged(nameof(LapIntelligenceStrengths));
        OnPropertyChanged(nameof(LapIntelligenceWeaknesses));
    }

    private void RaiseVoiceProperties()
    {
        OnPropertyChanged(nameof(VoiceLabel));
        OnPropertyChanged(nameof(TtsLabel));
        OnPropertyChanged(nameof(VoiceState));
        OnPropertyChanged(nameof(VoiceSuppressionState));
    }

    private static int VoicePriority(TelemetryEvent item)
    {
        return item.Type switch
        {
            EventType.LowFuel => 0,
            EventType.InvalidLapOrFlags => 1,
            EventType.TyreOverheating or EventType.BrakeOverheating => 2,
            EventType.AbruptBrakeRelease or EventType.UnstableBraking or EventType.EarlyThrottleWithSteering or EventType.TractionLoss or EventType.SteeringOveruse => 3,
            _ => 4
        };
    }

    private void RaiseSessionBrowserCommands()
    {
        (LoadSelectedSessionCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (ExportSessionJsonCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (ExportCoachingMarkdownCommand as RelayCommand)?.RaiseCanExecuteChanged();
    }

    private bool ShouldPersistSnapshot(TelemetrySnapshot snapshot, IReadOnlyList<TelemetryEvent> events)
    {
        return lastSnapshotSavedAt is null
            || snapshot.Timestamp - lastSnapshotSavedAt.Value >= TimeSpan.FromSeconds(SnapshotSampleSeconds)
            || events.Count > 0;
    }

    private string BuildCurrentSessionMarkdown()
    {
        return $"""
        # Coaching Summary

        - Session: {session.SessionId}
        - Started: {session.StartedAt.LocalDateTime:g}
        - Stint: {StintTime}
        - Completed laps: {CompletedLapsCount}
        - Best lap: {BestLapTime}
        - Last lap: {LastLapTime}
        - Fuel: {Fuel}
        - Estimated laps remaining: {EstimatedLapsRemaining}
        - Events: {session.RecentEvents.Count}

        ## Event Counts

        {SessionSummary}
        """;
    }

    private RacePrepPlan CurrentPrepPlan()
    {
        return new RacePrepPlan(
            EmptyToNull(PrepCar),
            EmptyToNull(PrepTrack),
            EmptyToNull(PrepSessionType),
            EmptyToNull(PrepTargetStintLength),
            EmptyToNull(PrepFuelPlan),
            EmptyToNull(PrepTyrePlan),
            EmptyToNull(PrepPracticeGoal),
            EmptyToNull(PrepDriverReminders),
            EmptyToNull(PrepSetupNotes),
            EmptyToNull(PrepStrategyNotes));
    }

    private void ApplyPrepPlan(RacePrepPlan plan)
    {
        PrepCar = plan.Car ?? "";
        PrepTrack = plan.Track ?? "";
        PrepSessionType = plan.SessionType ?? "";
        PrepTargetStintLength = plan.TargetStintLength ?? "";
        PrepFuelPlan = plan.FuelPlan ?? "";
        PrepTyrePlan = plan.TyrePlan ?? "";
        PrepPracticeGoal = plan.PracticeGoal ?? "";
        PrepDriverReminders = plan.DriverReminders ?? "";
        PrepSetupNotes = plan.SetupNotes ?? "";
        PrepStrategyNotes = plan.StrategyNotes ?? "";
        _ = RefreshKnowledgeAsync();
    }

    private CoachContext CurrentCoachContext()
    {
        return new CoachContext(
            reviewSessionNotes.Count > 0 ? reviewSessionNotes : null,
            LoadedSessionSummary,
            CurrentPrepPlan(),
            KnowledgeSources.Select(item => item.Source).ToArray(),
            false);
    }

    private void RaiseReviewModeProperties()
    {
        OnPropertyChanged(nameof(IsReviewMode));
        OnPropertyChanged(nameof(ReviewModeBanner));
        OnPropertyChanged(nameof(TelemetryStatus));
        OnPropertyChanged(nameof(TelemetryStatusBrush));
    }

    private object CurrentSessionSummaryObject()
    {
        return new
        {
            session.SessionId,
            session.StartedAt,
            stint_duration_seconds = session.StintDuration.TotalSeconds,
            completed_laps = session.CompletedLaps.Count,
            best_lap_seconds = session.BestLap?.Duration?.TotalSeconds,
            last_lap_seconds = session.LastLap?.Duration?.TotalSeconds,
            latest_fuel = session.LatestFuelLevel,
            estimated_laps_remaining = session.EstimatedLapsRemaining,
            recent_event_count = session.RecentEvents.Count
        };
    }

    private void RaisePacketDebugProperties()
    {
        OnPropertyChanged(nameof(PacketsReceivedCount));
        OnPropertyChanged(nameof(ValidPacketsCount));
        OnPropertyChanged(nameof(InvalidPacketsCount));
        OnPropertyChanged(nameof(LastPacketTimestamp));
        OnPropertyChanged(nameof(LastParserWarning));
    }

    private void RaiseDiagnosticsProperties()
    {
        OnPropertyChanged(nameof(UdpBind));
        OnPropertyChanged(nameof(ReceiverState));
        OnPropertyChanged(nameof(PacketsPerSecond));
        OnPropertyChanged(nameof(ValidPacketsPerSecond));
        OnPropertyChanged(nameof(InvalidPacketsPerSecond));
        OnPropertyChanged(nameof(LastValidPacketAge));
        OnPropertyChanged(nameof(LastInvalidReason));
        OnPropertyChanged(nameof(CurrentSchemaSeen));
        OnPropertyChanged(nameof(RawCaptureState));
        OnPropertyChanged(nameof(RawCapturePath));
    }

    private static string Format(double? value, string format)
    {
        return value.HasValue ? value.Value.ToString(format, CultureInfo.InvariantCulture) : "-";
    }

    private static string? EmptyToNull(string value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static string FormatDuration(TimeSpan? duration)
    {
        if (!duration.HasValue)
        {
            return "-";
        }

        return duration.Value.TotalHours >= 1
            ? duration.Value.ToString(@"h\:mm\:ss", CultureInfo.InvariantCulture)
            : duration.Value.ToString(@"m\:ss\.fff", CultureInfo.InvariantCulture);
    }

    private bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }

        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
