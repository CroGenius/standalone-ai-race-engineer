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
using RaceEngineer.Core.Coaching;
using RaceEngineer.Core.Events;
using RaceEngineer.Core.Knowledge;
using RaceEngineer.Core.Session;
using RaceEngineer.Core.Storage;
using RaceEngineer.Core.Telemetry;
using RaceEngineer.Core.Voice;

namespace RaceEngineer.Desktop.Wpf;

public sealed class MainWindowViewModel : INotifyPropertyChanged
{
    private const int SnapshotSampleSeconds = 1;
    private readonly TelemetryReceiver receiver;
    private readonly TelemetryDiagnostics diagnostics;
    private readonly RawPacketCapture rawPacketCapture;
    private readonly PacketReplayTool replayTool = new();
    private readonly EventEngine eventEngine = new();
    private readonly CoachEngine coachEngine = new();
    private readonly VoiceService voiceService;
    private readonly CalloutManager calloutManager = new();
    private readonly StorageService storageService;
    private readonly StoredResearchService researchService;
    private readonly SessionState session = new();
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
        SessionLabel = $"Session {session.SessionId}";
        SendChatCommand = new RelayCommand(SendChat, () => !string.IsNullOrWhiteSpace(ChatInput));
        SavePrepCommand = new RelayCommand(() => _ = SavePrepAsync());
        LoadPrepCommand = new RelayCommand(() => _ = LoadPrepAsync());
        SavePostSessionNoteCommand = new RelayCommand(SavePostSessionNote, () => !string.IsNullOrWhiteSpace(PostSessionNote));
        RefreshSessionsCommand = new RelayCommand(() => _ = RefreshSessionsAsync());
        LoadSelectedSessionCommand = new RelayCommand(() => _ = LoadSelectedSessionAsync(), () => SelectedSession is not null);
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
    public string SessionLabel { get; }
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

    public string TelemetryStatus => session.TelemetryOnline ? "Telemetry online" : "Telemetry offline";
    public Brush TelemetryStatusBrush => session.TelemetryOnline ? Brushes.LightGreen : Brushes.Orange;
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
    public string Speed => Format(session.LatestSnapshot?.Car.SpeedKmh, "0.0 km/h");
    public string Rpm => Format(session.LatestSnapshot?.Car.Rpm, "0");
    public string Gear => session.LatestSnapshot?.Car.Gear?.ToString(CultureInfo.InvariantCulture) ?? "-";
    public string Throttle => Format(session.LatestSnapshot?.Inputs.Throttle, "0.00");
    public string Brake => Format(session.LatestSnapshot?.Inputs.Brake, "0.00");
    public string Steering => Format(session.LatestSnapshot?.Inputs.Steering, "0.00");
    public string LapTime => Format(session.LatestSnapshot?.Lap.LapTimeS, "0.000 s");
    public string LapProgress => Format(session.LatestSnapshot?.Lap.LapProgress, "0.000");
    public string Fuel => Format(session.LatestSnapshot?.Condition.Fuel, "0.0");
    public string Position => session.LatestSnapshot?.Race.Position?.ToString(CultureInfo.InvariantCulture) ?? "-";
    public string VoiceLabel => voiceService.VoiceEnabled ? "Voice On" : "Voice Off";
    public string TtsLabel => voiceService.EngineerMuted ? "Engineer Muted" : "Engineer Audible";
    public string VoiceState => $"{voiceService.StateText} / {voiceService.MuteText}";
    public string VoiceSuppressionState => calloutManager.LastSuppressionState;
    public string CurrentLap => session.CurrentLap.ToString(CultureInfo.InvariantCulture);
    public string LastLapTime => FormatDuration(session.LastLap?.Duration);
    public string BestLapTime => FormatDuration(session.BestLap?.Duration);
    public string CompletedLapsCount => session.CompletedLaps.Count.ToString(CultureInfo.InvariantCulture);
    public string StintTime => FormatDuration(session.StintDuration);
    public string EstimatedLapsRemaining => Format(session.EstimatedLapsRemaining, "0.0");
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
            var counts = session.RecentEvents
                .GroupBy(item => item.Type)
                .OrderByDescending(group => group.Count())
                .Select(group => $"{group.Key}: {group.Count()}");
            return session.RecentEvents.Count == 0
                ? "Complete a run to generate recurring mistakes, tyre/brake/fuel summary, and an improvement plan."
                : string.Join(Environment.NewLine, counts);
        }
    }

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
    }

    private void OnPacketProcessed(object? sender, TelemetryPacketResult packet)
    {
        Application.Current.Dispatcher.Invoke(() =>
        {
            packetsReceivedCount++;
            lastPacketTimestamp = packet.Timestamp;
            diagnostics.Record(packet);
            rawPacketCapture.Record(packet);

            if (packet.IsValid)
            {
                validPacketsCount++;
            }
            else
            {
                invalidPacketsCount++;
                lastParserWarning = packet.Warning ?? "Packet rejected.";
                EventLog.Insert(0, EventLogItem.ParserWarning(lastParserWarning));
            }

            while (EventLog.Count > 100)
            {
                EventLog.RemoveAt(EventLog.Count - 1);
            }

            RaisePacketDebugProperties();
            RaiseDiagnosticsProperties();
        });
    }

    private void OnSnapshotReceived(object? sender, TelemetrySnapshot snapshot)
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

        Application.Current.Dispatcher.Invoke(() =>
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

            RaiseTelemetryProperties();
            RaiseVoiceProperties();
        });
    }

    private void SendChat()
    {
        var message = ChatInput.Trim();
        if (message.Length == 0)
        {
            return;
        }

        ChatInput = "";
        ChatMessages.Add($"You: {message}");
        var answer = coachEngine.Answer(session, message, CurrentCoachContext());
        var uncertainty = string.IsNullOrWhiteSpace(answer.Uncertainty) ? "" : $" ({answer.Uncertainty})";
        ChatMessages.Add($"Coach: {answer.Content}{uncertainty}");
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
        ChatMessages.Add("Coach: Post-session note saved.");
        _ = storageService.AddNoteAsync(session.SessionId, "post_session", note);
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

        LoadedSessionSummary = await storageService.LoadSessionSummaryMarkdownAsync(SelectedSession.SessionId);
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
            var result = voiceService.HandleSpokenQuery(session, query, coachEngine, CurrentCoachContext());
            ChatMessages.Add($"Coach: {result.WrittenResponse.Content}");
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

        var result = replayTool.Replay(rawPacketCapture.FilePath);
        ChatMessages.Add($"Replay: {result.PacketsRead} packets, {result.ValidPackets} valid, {result.InvalidPackets} invalid, {result.EventsProduced} events.");
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
            null,
            LoadedSessionSummary,
            CurrentPrepPlan(),
            KnowledgeSources.Select(item => item.Source).ToArray(),
            false);
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
