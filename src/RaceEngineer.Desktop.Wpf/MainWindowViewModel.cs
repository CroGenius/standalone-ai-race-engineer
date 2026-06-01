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
using RaceEngineer.Core.Coaching.Ai;
using RaceEngineer.Core.Events;
using RaceEngineer.Core.Knowledge;
using RaceEngineer.Core.Profile;
using RaceEngineer.Core.RaceAwareness;
using RaceEngineer.Core.Session;
using RaceEngineer.Core.SessionContext;
using RaceEngineer.Core.Storage;
using RaceEngineer.Core.Strategy;
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
    private HybridCoachEngine coachEngine = HybridCoachEngine.FromSettings(AppSettings.Default);
    private readonly TelemetryAnalyticsService analyticsService = new();
    private readonly LapIntelligenceService lapIntelligenceService = new();
    private readonly TyreIntelligenceService tyreIntelligenceService = new();
    private readonly DriverPerformanceIntelligenceService driverPerformanceService = new();
    private readonly TelemetryTraceBuilder traceBuilder = new();
    private readonly CoachEvidenceBuilder evidenceBuilder = new();
    private readonly VoiceService voiceService;
    private VoiceInputService voiceInputService;
    private bool voiceInputRuntimeInitialized;
    private string? voiceInputRuntimeError;
    private bool voiceInputUnavailableReported;
    private readonly CalloutManager calloutManager = new();
    private readonly StrategyEngine strategyEngine = new();
    private readonly StrategyCalloutManager strategyCalloutManager = new();
    private readonly SessionContextClassifier sessionContextClassifier = new();
    private PushToTalkHotkey pushToTalkHotkey;
    private readonly StorageService storageService;
    private readonly StoredResearchService researchService;
    private TrackResearchService trackResearchService;
    private WebResearchService webResearchService;
    private readonly SessionState session = new();
    private SessionState? reviewSession;
    private IReadOnlyList<TelemetrySnapshot> reviewSnapshots = [];
    private readonly List<TelemetrySnapshot> liveTraceSnapshots = [];
    private bool isReviewMode;
    private IReadOnlyList<string> reviewSessionNotes = [];
    private string? reviewSessionTrack;
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
    private string tyreIntelligenceTitle = "Tyre Intelligence (Live Session)";
    private string tyreGripConfidence = "-";
    private string tyreWarmupState = "-";
    private string tyreReadiness = "-";
    private string tyreOverheatingRisk = "-";
    private string strategyPanelTitle = "Strategy (Live Session)";
    private string strategyFuelRisk = "-";
    private string strategyLapsRemaining = "-";
    private string strategyPitRecommendation = "-";
    private string strategyTyreRisk = "-";
    private string strategySummary = "-";
    private TelemetryTimeline traceTimeline = TelemetryTimeline.Empty;
    private double timelineCursorProgress;
    private SessionTelemetryAnalytics sessionAnalytics = SessionTelemetryAnalytics.Empty;
    private SessionLapIntelligence sessionLapIntelligence = SessionLapIntelligence.Empty;
    private SessionTyreIntelligence sessionTyreIntelligence = SessionTyreIntelligence.Unavailable("No tyre analysis yet.");
    private SessionDriverPerformance sessionDriverPerformance = SessionDriverPerformance.Unavailable(SessionDriverPerformance.NeedCleanLapMessage);
    private SessionStrategy sessionStrategy = SessionStrategy.Empty;
    private LiveRaceContext liveRaceContext = LiveRaceContext.Unavailable("initial");
    private TrackMemoryRecord? trackMemoryRecord;
    private TrackMemoryComparison? trackMemoryComparison;
    private readonly TrackMemoryService trackMemoryService = new();
    private readonly SessionMemoryService sessionMemoryService = new();
    private SessionMemorySummary? previousStoredSessionMemory;
    private IReadOnlyList<SessionMemorySummary> recentStoredSessionMemories = [];
    private SessionDebrief? currentSessionDebrief;
    private string sessionMemoryLastSummaryLabel = "No stored session summary yet.";
    private string sessionMemoryKnownWeaknessesLabel = "-";
    private string sessionDebriefPreview = "Generate a debrief from the current session data.";
    private TrackGuide? cachedTrackGuide;
    private string? lastEnsuredTrackGuideKey;
    private string trackResearchDetectedTrackLabel = "unavailable";
    private string trackGuideAvailableLabel = "no";
    private string trackGuideLastFetchedLabel = "-";
    private string trackGuideSourceCountLabel = "0";
    private string trackGuideBrakingZonesLabel = "-";
    private string trackGuideTractionZonesLabel = "-";
    private string trackGuideKeyCornersLabel = "-";
    private string trackGuideSetupNotesLabel = "-";
    private WebResearchBundle cachedWebResearch = WebResearchBundle.Empty();
    private string webResearchDetectedTrackLabel = "unavailable";
    private string webResearchDetectedCarClassLabel = "unavailable";
    private string webResearchAvailableLabel = "no";
    private string webResearchLastFetchedLabel = "-";
    private string webResearchSourceCountLabel = "0";
    private string webResearchProviderLabel = "-";
    private string webResearchCacheStatusLabel = "-";
    private string webResearchItemsFetchedLabel = "0";
    private string webResearchFetchStatusLabel = "-";
    private string webResearchLastFetchResultLabel = "-";
    private string webResearchErrorMessageLabel = "-";
    private OpponentIntelligenceRecommendation? currentOpponentIntelligence;
    private DriverCoachingRecommendation? currentDriverCoaching;
    private string driverCoachingPanelTitle = "Driver Coaching (Live Session)";
    private string driverCoachingTrendLabel = "-";
    private string driverCoachingBiggestWeaknessLabel = "-";
    private string driverCoachingStrongestAreaLabel = "-";
    private string driverCoachingConsistencyLabel = "-";
    private string driverCoachingPreviousDeltaLabel = "-";
    private string driverCoachingTopTargetsLabel = "-";
    private string opponentPositionLabel = "unavailable";
    private string opponentCarAheadLabel = "-";
    private string opponentCarBehindLabel = "-";
    private string opponentGapAheadLabel = "-";
    private string opponentGapBehindLabel = "-";
    private string opponentTrendLabel = "-";
    private string opponentBattleStatusLabel = "-";
    private string opponentConfidenceLabel = "-";
    private StrategyKnowledgeRecommendation? currentStrategyKnowledge;
    private TrackCarKnowledgeRecommendation? currentTrackCarKnowledge;
    private string trackIntelligenceTrackLabel = "-";
    private string trackIntelligenceCarClassLabel = "-";
    private string trackIntelligenceBrakeDemandLabel = "-";
    private string trackIntelligenceTyreDemandLabel = "-";
    private string trackIntelligenceFuelExpectationLabel = "-";
    private string trackIntelligenceSetupFocusLabel = "-";
    private string trackIntelligenceOvertakingZonesLabel = "-";
    private string strategyKnowledgeTrackLabel = "unavailable";
    private string strategyKnowledgeCarClassLabel = "unavailable";
    private string strategyKnowledgeRecommendedFuelLabel = "-";
    private string strategyKnowledgeConfidenceLabel = "-";
    private string strategyKnowledgeSourceLabel = "unavailable";
    private string raceAwarenessPanelTitle = "Race Awareness (Live Session)";
    private string raceTrackLabel = "-";
    private string raceCarLabel = "-";
    private string raceSessionTypeLabel = "-";
    private string racePositionLabel = "-";
    private string raceGapAheadLabel = "-";
    private string raceGapBehindLabel = "-";
    private string raceContextConfidenceLabel = "-";
    private string raceFieldDiagnosticsLabel = "-";
    private string opponentTelemetryPresentLabel = "present: (none); missing: gap_ahead_s, gap_behind_s, car_ahead, car_behind, total_cars, position";
    private string opponentTelemetryResolvedLabel = "resolved: (none)";
    private string opponentTelemetryCandidatesLabel = "candidates: (none)";
    private string telemetryOpponentFieldsLabel = "present: (none); missing: gap_ahead_s, gap_behind_s, car_ahead, car_behind, total_cars, position";
    private string telemetryOpponentResolvedLabel = "resolved: (none)";
    private string telemetryOpponentCandidatesLabel = "candidates: (none)";
    private string? lastRawTelemetryJson;
    private string raceMemoryPreviousBestLabel = "-";
    private string raceMemoryPreviousAverageLabel = "-";
    private string performancePanelTitle = "Performance Intelligence (Live Session)";
    private string performanceBiggestLossLabel = "-";
    private string performanceMainWeaknessLabel = "-";
    private string performanceBrakingQualityLabel = "-";
    private string performanceThrottleQualityLabel = "-";
    private string performanceConsistencyLabel = "-";
    private string performanceCurrentVsBestLabel = "-";
    private string performanceStoredBaselineLabel = "-";
    private SessionContextAssessment sessionContextAssessment = SessionContextAssessment.InitialLive;
    private string sessionModeLabel = SessionContextAssessment.InitialLive.SessionModeLabel;
    private string strategyConfidenceLabel = SessionContextAssessment.InitialLive.StrategyConfidenceLabel;
    private AppSettings appSettings = AppSettings.Default;
    private ProfilePreferencesService profilePreferencesService;
    private UserPreferencesBundle userPreferences = UserPreferencesBundle.FromAppSettings(AppSettings.Default);
    private string prefDriverName = "";
    private string prefPreferredLanguage = "auto";
    private string prefPreferredUnits = "metric";
    private string prefExperienceLevel = "intermediate";
    private string prefDrivingStyle = "balanced";
    private string prefResponseLength = "normal";
    private string prefCalloutAggressiveness = "normal";
    private bool prefVoiceEnabledDefault;
    private string prefSpeechRecognitionProvider = "auto";
    private string prefPushToTalkHotkey = "F6";
    private bool prefVoiceInputConfirmationsEnabled = true;
    private bool prefTranscriptConfirmationEnabled;
    private string prefMicrophoneDeviceNumber = "-1";
    private string micCalibrationStatus = "Mic test not run yet.";
    private bool prefEvidenceBulletsEnabled = true;
    private bool prefQuietModeEnabled;
    private bool prefMinimalEngineerEnabled;
    private string prefFuelSafetyMarginLaps = "1.0";
    private string prefPitAggressiveness = "normal";
    private string prefTyreRiskSensitivity = "normal";
    private string prefPitStrategyPreference = "balanced";

    public MainWindowViewModel()
    {
        startupWarnings.AddRange(App.PendingStartupWarnings);
        var settingsResult = AppSettings.Load(Path.Combine(AppContext.BaseDirectory, "appsettings.json"));
        startupWarnings.AddRange(settingsResult.Warnings);
        appSettings = settingsResult.Settings;
        var settings = appSettings;
        try
        {
            coachEngine = HybridCoachEngine.FromSettings(settings);
        }
        catch (Exception exception)
        {
            startupWarnings.Add($"AI coach initialization failed. Deterministic coaching only. {exception.Message}");
            coachEngine = HybridCoachEngine.FromSettings(AppSettings.Default with { AiEngineerEnabled = false, AiProvider = "disabled" });
        }

        try
        {
            Directory.CreateDirectory(settings.CaptureFolder);
            Directory.CreateDirectory(settings.ReplayFolder);
        }
        catch (Exception exception)
        {
            startupWarnings.Add($"Debug folders could not be created. {exception.Message}");
        }

        receiver = new TelemetryReceiver(settings.UdpBindIp, settings.UdpPort);
        diagnostics = new TelemetryDiagnostics(settings.UdpBindIp, settings.UdpPort);
        rawPacketCapture = new RawPacketCapture(Path.Combine(settings.CaptureFolder, "raw-packets.jsonl"));
        voiceService = new VoiceService(CreateVoiceOutputSafely());
        voiceService.SetVoiceEnabled(settings.VoiceEnabledDefault);
        if (settings.VoiceEnabledDefault)
        {
            voiceService.SetMuted(false);
        }
        voiceInputService = CreatePlaceholderVoiceInputService(settings);
        WireVoiceInputServiceEvents(voiceInputService);
        CoachQueryDiagnosticLog.TraceRaised += OnCoachQueryTraceRaised;
        CoachQueryDiagnosticLog.RuntimeTraceRaised += OnCoachQueryRuntimeTraceRaised;
        CoachQueryDiagnosticLog.AiTraceRaised += OnAiTraceRaised;
        TrackGuideZoneMappingDiagnosticLog.TraceRaised += OnZoneMappingTraceRaised;
        TrackGuideZoneMappingDiagnosticLog.AuditRaised += OnZoneMappingAuditRaised;
        TrackGuideZoneMappingDiagnosticLog.ResolutionRaised += OnZoneMappingResolutionRaised;
        pushToTalkHotkey = ParsePushToTalkHotkeySafely(settings.PushToTalkHotkey);
        storageService = new StorageService(settings.DatabasePath);
        profilePreferencesService = new ProfilePreferencesService(storageService);
        researchService = new StoredResearchService(storageService);
        try
        {
            trackResearchService = TrackResearchService.FromSettings(storageService, settings);
            webResearchService = WebResearchService.FromSettings(storageService, settings);
        }
        catch (Exception exception)
        {
            startupWarnings.Add($"Track guide service unavailable. {exception.Message}");
            trackResearchService = TrackResearchService.FromSettings(
                storageService,
                AppSettings.Default with { TrackResearchEnabled = false, TrackResearchProvider = "disabled" });
            webResearchService = WebResearchService.FromSettings(
                storageService,
                AppSettings.Default with { WebResearchEnabled = false });
        }
        sessionLabel = $"Session {session.SessionId}";
        SendChatCommand = new RelayCommand(SendChat, () => !string.IsNullOrWhiteSpace(ChatInput));
        SavePrepCommand = new RelayCommand(() => _ = SavePrepAsync());
        LoadPrepCommand = new RelayCommand(() => _ = LoadPrepAsync());
        SavePreferencesCommand = new RelayCommand(() => _ = SavePreferencesAsync());
        SavePostSessionNoteCommand = new RelayCommand(SavePostSessionNote, () => !string.IsNullOrWhiteSpace(PostSessionNote));
        RefreshSessionsCommand = new RelayCommand(() => _ = RefreshSessionsAsync());
        LoadSelectedSessionCommand = new RelayCommand(() => _ = LoadSelectedSessionAsync(), () => SelectedSession is not null);
        ExitReviewModeCommand = new RelayCommand(ExitReviewMode, () => IsReviewMode);
        ExportSessionJsonCommand = new RelayCommand(() => _ = ExportSelectedSessionJsonAsync(), () => SelectedSession is not null);
        ExportCoachingMarkdownCommand = new RelayCommand(() => _ = ExportSelectedSessionMarkdownAsync(), () => SelectedSession is not null);
        TogglePushToTalkCommand = new RelayCommand(ToggleVoiceInputMute);
        ToggleTtsCommand = new RelayCommand(ToggleTts);
        ToggleVoiceCommand = new RelayCommand(ToggleVoice);
        TestVoiceCommand = new RelayCommand(TestVoice);
        TestMicCommand = new RelayCommand(() => _ = RunMicCalibrationTestAsync());
        ConfirmTranscriptCommand = new RelayCommand(ConfirmPendingTranscript, () => voiceInputService.HasPendingTranscript);
        RejectTranscriptCommand = new RelayCommand(RejectPendingTranscript, () => voiceInputService.HasPendingTranscript);
        ToggleRawCaptureCommand = new RelayCommand(ToggleRawCapture);
        ReplayCaptureCommand = new RelayCommand(ReplayCapture);
        SaveKnowledgeCommand = new RelayCommand(() => _ = SaveKnowledgeAsync(), () => !string.IsNullOrWhiteSpace(KnowledgeTitle) && !string.IsNullOrWhiteSpace(KnowledgeContent));
        SearchKnowledgeCommand = new RelayCommand(() => _ = SearchKnowledgeAsync());
        ImportKnowledgeCommand = new RelayCommand(() => _ = ImportKnowledgeAsync());
        DeleteKnowledgeCommand = new RelayCommand(() => _ = DeleteKnowledgeAsync(), () => SelectedKnowledgeSource is not null);
        SaveMemorySummaryCommand = new RelayCommand(() => _ = SaveMemorySummaryAsync());
        GenerateDebriefCommand = new RelayCommand(GenerateSessionDebrief);
        FetchTrackGuideCommand = new RelayCommand(() => _ = FetchTrackGuideAsync());
        FetchTrackResearchCommand = new RelayCommand(() => _ = FetchTrackResearchAsync());
        FetchTrackCarStrategyResearchCommand = new RelayCommand(() => _ = FetchTrackCarStrategyResearchAsync());
        RefreshWebResearchCommand = new RelayCommand(() => _ = RefreshWebResearchAsync());
        receiver.PacketProcessed += OnPacketProcessed;
        receiver.SnapshotReceived += OnSnapshotReceived;
        MicrophoneDevices.Add(new MicrophoneDeviceOption
        {
            DeviceNumber = -1,
            DisplayName = "System default (Communications)"
        });

        _ = StartAsyncSafe();
    }

    private async Task StartAsyncSafe()
    {
        try
        {
            await StartAsync();
        }
        catch (Exception exception)
        {
            ReportStartupWarning($"Startup initialization failed. Core telemetry UI remains available. {exception.Message}");
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public ObservableCollection<string> ChatMessages { get; } = [];
    public ObservableCollection<MicrophoneDeviceOption> MicrophoneDevices { get; } = [];
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

    public string TyreIntelligenceTitle => tyreIntelligenceTitle;
    public string TyreGripConfidence => tyreGripConfidence;
    public string TyreWarmupState => tyreWarmupState;
    public string TyreReadiness => tyreReadiness;
    public string TyreOverheatingRisk => tyreOverheatingRisk;

    public string RaceAwarenessPanelTitle => raceAwarenessPanelTitle;
    public string RaceTrackLabel => raceTrackLabel;
    public string RaceCarLabel => raceCarLabel;
    public string RaceSessionTypeLabel => raceSessionTypeLabel;
    public string RacePositionLabel => racePositionLabel;
    public string RaceGapAheadLabel => raceGapAheadLabel;
    public string RaceGapBehindLabel => raceGapBehindLabel;
    public string RaceContextConfidenceLabel => raceContextConfidenceLabel;
    public string RaceFieldDiagnosticsLabel => raceFieldDiagnosticsLabel;
    public string OpponentTelemetryPresentLabel => opponentTelemetryPresentLabel;
    public string OpponentTelemetryResolvedLabel => opponentTelemetryResolvedLabel;
    public string OpponentTelemetryCandidatesLabel => opponentTelemetryCandidatesLabel;
    public string TelemetryOpponentFieldsLabel => telemetryOpponentFieldsLabel;
    public string TelemetryOpponentResolvedLabel => telemetryOpponentResolvedLabel;
    public string TelemetryOpponentCandidatesLabel => telemetryOpponentCandidatesLabel;
    public string RaceMemoryPreviousBestLabel => raceMemoryPreviousBestLabel;
    public string RaceMemoryPreviousAverageLabel => raceMemoryPreviousAverageLabel;

    public string OpponentPositionLabel => opponentPositionLabel;
    public string OpponentCarAheadLabel => opponentCarAheadLabel;
    public string OpponentCarBehindLabel => opponentCarBehindLabel;
    public string OpponentGapAheadLabel => opponentGapAheadLabel;
    public string OpponentGapBehindLabel => opponentGapBehindLabel;
    public string OpponentTrendLabel => opponentTrendLabel;
    public string OpponentBattleStatusLabel => opponentBattleStatusLabel;
    public string OpponentConfidenceLabel => opponentConfidenceLabel;

    public string DriverCoachingPanelTitle => driverCoachingPanelTitle;
    public string DriverCoachingTrendLabel => driverCoachingTrendLabel;
    public string DriverCoachingBiggestWeaknessLabel => driverCoachingBiggestWeaknessLabel;
    public string DriverCoachingStrongestAreaLabel => driverCoachingStrongestAreaLabel;
    public string DriverCoachingConsistencyLabel => driverCoachingConsistencyLabel;
    public string DriverCoachingPreviousDeltaLabel => driverCoachingPreviousDeltaLabel;
    public string DriverCoachingTopTargetsLabel => driverCoachingTopTargetsLabel;

    public string SessionMemoryLastSummaryLabel => sessionMemoryLastSummaryLabel;
    public string SessionMemoryKnownWeaknessesLabel => sessionMemoryKnownWeaknessesLabel;
    public string SessionDebriefPreview => sessionDebriefPreview;
    public string TrackResearchDetectedTrackLabel => trackResearchDetectedTrackLabel;
    public string TrackGuideAvailableLabel => trackGuideAvailableLabel;
    public string TrackGuideLastFetchedLabel => trackGuideLastFetchedLabel;
    public string TrackGuideSourceCountLabel => trackGuideSourceCountLabel;
    public string TrackGuideBrakingZonesLabel => trackGuideBrakingZonesLabel;
    public string TrackGuideTractionZonesLabel => trackGuideTractionZonesLabel;
    public string TrackGuideKeyCornersLabel => trackGuideKeyCornersLabel;
    public string TrackGuideSetupNotesLabel => trackGuideSetupNotesLabel;
    public string WebResearchDetectedTrackLabel => webResearchDetectedTrackLabel;
    public string WebResearchDetectedCarClassLabel => webResearchDetectedCarClassLabel;
    public string WebResearchAvailableLabel => webResearchAvailableLabel;
    public string WebResearchLastFetchedLabel => webResearchLastFetchedLabel;
    public string WebResearchSourceCountLabel => webResearchSourceCountLabel;
    public string WebResearchProviderLabel => webResearchProviderLabel;
    public string WebResearchCacheStatusLabel => webResearchCacheStatusLabel;
    public string WebResearchItemsFetchedLabel => webResearchItemsFetchedLabel;
    public string WebResearchFetchStatusLabel => webResearchFetchStatusLabel;
    public string WebResearchLastFetchResultLabel => webResearchLastFetchResultLabel;
    public string WebResearchErrorMessageLabel => webResearchErrorMessageLabel;
    public string StrategyKnowledgeTrackLabel => strategyKnowledgeTrackLabel;
    public string StrategyKnowledgeCarClassLabel => strategyKnowledgeCarClassLabel;
    public string StrategyKnowledgeRecommendedFuelLabel => strategyKnowledgeRecommendedFuelLabel;
    public string StrategyKnowledgeConfidenceLabel => strategyKnowledgeConfidenceLabel;
    public string StrategyKnowledgeSourceLabel => strategyKnowledgeSourceLabel;
    public string TrackIntelligenceTrackLabel => trackIntelligenceTrackLabel;
    public string TrackIntelligenceCarClassLabel => trackIntelligenceCarClassLabel;
    public string TrackIntelligenceBrakeDemandLabel => trackIntelligenceBrakeDemandLabel;
    public string TrackIntelligenceTyreDemandLabel => trackIntelligenceTyreDemandLabel;
    public string TrackIntelligenceFuelExpectationLabel => trackIntelligenceFuelExpectationLabel;
    public string TrackIntelligenceSetupFocusLabel => trackIntelligenceSetupFocusLabel;
    public string TrackIntelligenceOvertakingZonesLabel => trackIntelligenceOvertakingZonesLabel;

    public string PerformancePanelTitle => performancePanelTitle;
    public string PerformanceBiggestLossLabel => performanceBiggestLossLabel;
    public string PerformanceMainWeaknessLabel => performanceMainWeaknessLabel;
    public string PerformanceBrakingQualityLabel => performanceBrakingQualityLabel;
    public string PerformanceThrottleQualityLabel => performanceThrottleQualityLabel;
    public string PerformanceConsistencyLabel => performanceConsistencyLabel;
    public string PerformanceCurrentVsBestLabel => performanceCurrentVsBestLabel;
    public string PerformanceStoredBaselineLabel => performanceStoredBaselineLabel;

    public string StrategyPanelTitle => strategyPanelTitle;
    public string StrategyFuelRisk => strategyFuelRisk;
    public string StrategyLapsRemaining => strategyLapsRemaining;
    public string StrategyPitRecommendation => strategyPitRecommendation;
    public string StrategyTyreRisk => strategyTyreRisk;
    public string StrategySummary => strategySummary;

    public string PrefDriverName
    {
        get => prefDriverName;
        set => SetField(ref prefDriverName, value);
    }

    public string PrefPreferredLanguage
    {
        get => prefPreferredLanguage;
        set => SetField(ref prefPreferredLanguage, value);
    }

    public string PrefPreferredUnits
    {
        get => prefPreferredUnits;
        set => SetField(ref prefPreferredUnits, value);
    }

    public string PrefExperienceLevel
    {
        get => prefExperienceLevel;
        set => SetField(ref prefExperienceLevel, value);
    }

    public string PrefDrivingStyle
    {
        get => prefDrivingStyle;
        set => SetField(ref prefDrivingStyle, value);
    }

    public string PrefResponseLength
    {
        get => prefResponseLength;
        set => SetField(ref prefResponseLength, value);
    }

    public string PrefCalloutAggressiveness
    {
        get => prefCalloutAggressiveness;
        set => SetField(ref prefCalloutAggressiveness, value);
    }

    public bool PrefVoiceEnabledDefault
    {
        get => prefVoiceEnabledDefault;
        set => SetField(ref prefVoiceEnabledDefault, value);
    }

    public string PrefSpeechRecognitionProvider
    {
        get => prefSpeechRecognitionProvider;
        set => SetField(ref prefSpeechRecognitionProvider, value);
    }

    public string PrefPushToTalkHotkey
    {
        get => prefPushToTalkHotkey;
        set => SetField(ref prefPushToTalkHotkey, value);
    }

    public bool PrefVoiceInputConfirmationsEnabled
    {
        get => prefVoiceInputConfirmationsEnabled;
        set => SetField(ref prefVoiceInputConfirmationsEnabled, value);
    }

    public bool PrefTranscriptConfirmationEnabled
    {
        get => prefTranscriptConfirmationEnabled;
        set => SetField(ref prefTranscriptConfirmationEnabled, value);
    }

    public string PrefMicrophoneDeviceNumber
    {
        get => prefMicrophoneDeviceNumber;
        set
        {
            if (!SetField(ref prefMicrophoneDeviceNumber, value))
            {
                return;
            }

            OnPropertyChanged(nameof(SelectedMicrophoneDeviceNumber));
        }
    }

    public int SelectedMicrophoneDeviceNumber
    {
        get => int.TryParse(PrefMicrophoneDeviceNumber, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : -1;
        set => PrefMicrophoneDeviceNumber = value.ToString(CultureInfo.InvariantCulture);
    }

    public string MicCalibrationStatus
    {
        get => micCalibrationStatus;
        private set => SetField(ref micCalibrationStatus, value);
    }

    public bool PrefEvidenceBulletsEnabled
    {
        get => prefEvidenceBulletsEnabled;
        set => SetField(ref prefEvidenceBulletsEnabled, value);
    }

    public bool PrefQuietModeEnabled
    {
        get => prefQuietModeEnabled;
        set => SetField(ref prefQuietModeEnabled, value);
    }

    public bool PrefMinimalEngineerEnabled
    {
        get => prefMinimalEngineerEnabled;
        set => SetField(ref prefMinimalEngineerEnabled, value);
    }

    public string SessionModeLabel => sessionModeLabel;

    public string StrategyConfidenceLabel => strategyConfidenceLabel;

    public string EngineerModeLabel => userPreferences.Coach.MinimalEngineerEnabled
        ? "Minimal"
        : userPreferences.Coach.QuietModeEnabled
            ? "Quiet"
            : "Normal";

    public string AiEngineerStatus => AiEngineerStatusResolver.ResolveLabel(appSettings, AiEngineerRuntime.LastDiagnostic);

    public string PrefFuelSafetyMarginLaps
    {
        get => prefFuelSafetyMarginLaps;
        set => SetField(ref prefFuelSafetyMarginLaps, value);
    }

    public string PrefPitAggressiveness
    {
        get => prefPitAggressiveness;
        set => SetField(ref prefPitAggressiveness, value);
    }

    public string PrefTyreRiskSensitivity
    {
        get => prefTyreRiskSensitivity;
        set => SetField(ref prefTyreRiskSensitivity, value);
    }

    public string PrefPitStrategyPreference
    {
        get => prefPitStrategyPreference;
        set => SetField(ref prefPitStrategyPreference, value);
    }

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
    public ICommand SavePreferencesCommand { get; }
    public ICommand SavePostSessionNoteCommand { get; }
    public ICommand RefreshSessionsCommand { get; }
    public ICommand LoadSelectedSessionCommand { get; }
    public ICommand ExportSessionJsonCommand { get; }
    public ICommand ExportCoachingMarkdownCommand { get; }
    public ICommand TogglePushToTalkCommand { get; }
    public ICommand ToggleTtsCommand { get; }
    public ICommand ToggleVoiceCommand { get; }
    public ICommand TestVoiceCommand { get; }
    public ICommand TestMicCommand { get; }
    public ICommand ConfirmTranscriptCommand { get; }
    public ICommand RejectTranscriptCommand { get; }
    public ICommand ToggleRawCaptureCommand { get; }
    public ICommand ReplayCaptureCommand { get; }
    public ICommand SaveKnowledgeCommand { get; }
    public ICommand SearchKnowledgeCommand { get; }
    public ICommand ImportKnowledgeCommand { get; }
    public ICommand DeleteKnowledgeCommand { get; }
    public ICommand SaveMemorySummaryCommand { get; }
    public ICommand GenerateDebriefCommand { get; }
    public ICommand FetchTrackGuideCommand { get; }
    public ICommand FetchTrackResearchCommand { get; }
    public ICommand FetchTrackCarStrategyResearchCommand { get; }
    public ICommand RefreshWebResearchCommand { get; }

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
    public string VoiceInputMuteLabel => voiceInputService.InputMuted ? "Mic Muted" : "Mic Live";
    public string VoiceState => $"{voiceService.StateText} / {voiceService.MuteText}";
    public string VoiceInputState => voiceInputService.StatusText;
    public string PushToTalkIndicator => voiceInputService.PushToTalkIndicator;
    public string ListeningIndicator => voiceInputService.ListeningIndicator;
    public string RecognizedSpeechPreview => voiceInputService.RecognizedSpeechPreview;
    public string PushToTalkHotkeyLabel => $"Hold {pushToTalkHotkey.DisplayName} or Hold to Talk";
    public string VoiceSuppressionState =>
        $"{calloutManager.LastSuppressionState} | {voiceService.InteractionDiagnostic}";
    public string MicDiagnosticsSummary =>
        $"Mic {MicDeviceName} | Raw {MicRawPeakRms:0} | Conv {MicConvertedPeakRms:0} | Whisper {MicWhisperInputRms:0} | Speech {MicSpeechDetected} | Quality {MicSignalQualityLabel}";
    public float MicCurrentRms => voiceInputService.MicCurrentRms;
    public float MicPeakRms => voiceInputService.MicPeakRms;
    public float MicRawPeakRms => voiceInputService.MicRawPeakRms;
    public float MicConvertedPeakRms => voiceInputService.MicConvertedPeakRms;
    public float MicWhisperInputRms => voiceInputService.MicWhisperInputRms;
    public bool MicSpeechDetected => voiceInputService.MicSpeechDetected;
    public bool MicClipping => voiceInputService.MicClipping;
    public string MicDeviceName => voiceInputService.MicDeviceName;
    public string MicSignalQualityLabel => voiceInputService.MicSignalQualityLabel;
    public bool HasPendingTranscript => voiceInputService.HasPendingTranscript;
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
            AddStartupChat($"Startup: {warning}");
        }

        AddStartupChat($"Startup: Voice input status — {voiceInputService.StatusText}. Provider: {voiceInputService.ProviderStatus}");
        AddStartupChat($"Startup: AI engineer status — {AiEngineerStatus}. Set RACE_ENGINEER_AI_API_KEY for OpenAI.");

        try
        {
            await storageService.InitializeAsync();
            userPreferences = await profilePreferencesService.LoadAsync(appSettings);
            appSettings = ProfilePreferencesService.MergeAppSettings(appSettings, userPreferences);
            try
            {
                trackResearchService = TrackResearchService.FromSettings(storageService, appSettings);
                webResearchService = WebResearchService.FromSettings(storageService, appSettings);
            }
            catch (Exception exception)
            {
                ReportStartupWarning($"Track guide service unavailable. {exception.Message}");
            }

            BindPreferencesToView(userPreferences);
            ApplyUserPreferences();
            await storageService.CreateSessionAsync(session.SessionId, session.StartedAt);
            await RefreshSessionsAsync();
            await RefreshKnowledgeAsync();
        }
        catch (Exception exception)
        {
            ReportStartupWarning($"Database or profile initialization failed. {exception.Message}");
        }

        try
        {
            await receiver.StartAsync();
            diagnostics.SetReceiverRunning(receiver.IsRunning);
        }
        catch (InvalidOperationException exception)
        {
            diagnostics.SetReceiverRunning(false);
            ReportStartupWarning(exception.Message);
            lastParserWarning = exception.Message;
        }

        RaiseDiagnosticsProperties();
        RaiseVoiceProperties();
        try
        {
            RefreshAnalytics();
        }
        catch (Exception exception)
        {
            ReportStartupWarning($"Initial analytics refresh failed. {exception.Message}");
        }
    }

    private void AddStartupChat(string message)
    {
        if (Application.Current?.Dispatcher is { } dispatcher && !dispatcher.CheckAccess())
        {
            dispatcher.Invoke(() => ChatMessages.Add(message));
            return;
        }

        ChatMessages.Add(message);
    }

    private void ReportOptionalPanelFailure(string panelName, Exception exception)
    {
        ReportStartupWarning($"{panelName} panel unavailable: {exception.Message}");
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
            if (!string.IsNullOrWhiteSpace(packet.RawJson))
            {
                lastRawTelemetryJson = packet.RawJson;
            }

            UpdateOpponentTelemetryDiagnostics(packet.Snapshot, packet.RawJson);
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
            var stamped = snapshot.Lap.LapNumber is null
                ? snapshot with { Lap = snapshot.Lap with { LapNumber = session.CurrentLap } }
                : snapshot;
            liveTraceSnapshots.Add(stamped);
            while (liveTraceSnapshots.Count > MaxLiveTraceSnapshots)
            {
                liveTraceSnapshots.RemoveAt(0);
            }
        }

        Application.Current.Dispatcher.Invoke(() =>
        {
            if (!isReviewMode)
            {
                UpdateSessionContext();

                foreach (var item in events)
                {
                    EventLog.Insert(0, EventLogItem.FromEvent(item));
                }

                while (EventLog.Count > 100)
                {
                    EventLog.RemoveAt(EventLog.Count - 1);
                }

                var callout = coachEngine.ChooseLiveCallout(events, sessionContextAssessment);
                var spoken = false;
                var timestamp = snapshot.Timestamp;
                if (voiceService.InteractionGate.IsAutomaticCalloutAllowed(timestamp))
                {
                    foreach (var item in events.OrderBy(VoicePriority))
                    {
                        var voiceCallout = calloutManager.TryCreateCallout(
                            item,
                            sessionContextAssessment,
                            userPreferences.Coach);
                        if (voiceCallout is not null)
                        {
                            spoken = voiceService.SpeakAutomaticCallout(voiceCallout, timestamp);
                            if (spoken)
                            {
                                LastCallout = voiceCallout;
                            }

                            break;
                        }
                    }
                }
                else
                {
                    var reason = voiceService.InteractionGate.AutomaticSuppressionReason(timestamp);
                    calloutManager.NoteExternalSuppression($"Automatic callout suppressed: {reason}.");
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

    private void OnCoachQueryTraceRaised(CoachAnswerTrace trace)
    {
        Application.Current?.Dispatcher.Invoke(() =>
        {
            ChatMessages.Add(
                $"Coach diag: topic={trace.Topic} gate={(trace.GateBlocked ? "blocked" : "open")} source={trace.AnswerSource} evidence={trace.SelectedEvidenceType ?? "none"} fallback={trace.FallbackReason ?? "none"}");
        });
    }

    private void OnCoachQueryRuntimeTraceRaised(CoachQueryRuntimeTrace trace)
    {
        Application.Current?.Dispatcher.Invoke(() =>
        {
            ChatMessages.Add(
                $"Coach trace: raw='{trace.RawTranscript ?? trace.Transcript}' normalized='{trace.NormalizedTranscript ?? trace.Transcript}' topic={trace.Topic} subtopic={trace.RaceAwarenessSubtopic?.ToString() ?? "none"} evidenceCategory={trace.SelectedEvidenceCategory ?? "none"} source={trace.AnswerSource} aiProvider={trace.AiProviderSelected ?? "none"} aiModel={trace.AiModel ?? "none"} aiTimeout={trace.AiTimeoutSeconds?.ToString() ?? "none"} aiLatencyMs={trace.AiLatencyMs?.ToString() ?? "none"} aiFallback={trace.AiFallbackReason ?? "none"} det='{trace.DeterministicAnswer}' ai='{trace.PrimaryAnswer}' ui='{trace.FinalDisplayedText}' build={trace.AssemblyVersion}");
            OnPropertyChanged(nameof(AiEngineerStatus));
        });
    }

    private void OnAiTraceRaised(EngineerAiDiagnosticTrace trace)
    {
        Application.Current?.Dispatcher.Invoke(() =>
        {
            ChatMessages.Add(
                $"AI diag: provider={trace.ProviderSelected} model={trace.Model} timeout={trace.TimeoutSeconds}s latency={trace.LatencyMs?.ToString() ?? "none"}ms usedAi={trace.UsedAi} fallback={trace.FallbackReason ?? "none"}");
            OnPropertyChanged(nameof(AiEngineerStatus));
        });
    }

    private void OnZoneMappingTraceRaised(TrackGuideZoneMappingTrace trace)
    {
        Application.Current?.Dispatcher.Invoke(() =>
        {
            ChatMessages.Add(
                $"Zone map [{trace.Field}]: '{trace.Original}' -> '{trace.Mapped}' (guide={trace.GuideTrackName ?? "none"}, source={trace.GuideSource})");
        });
    }

    private void OnZoneMappingAuditRaised(TrackGuideZoneMappingAuditTrace trace)
    {
        Application.Current?.Dispatcher.Invoke(() =>
        {
            ChatMessages.Add(
                $"Zone audit [{trace.Path}]: track={trace.TrackName ?? "none"} guide={trace.GuideTrackName ?? "none"} source={trace.GuideSource} corners={trace.CornerCount} placeholderCorners={trace.UsesZonePlaceholderCorners} mapper={trace.MapperExecuted} stillZone={trace.StillContainsZoneToken} | '{trace.Original}' -> '{trace.Mapped}'");
        });
    }

    private void OnZoneMappingResolutionRaised(TrackGuideResolutionTrace trace)
    {
        Application.Current?.Dispatcher.Invoke(() =>
        {
            ChatMessages.Add(
                $"Track guide resolution: trackName={trace.TrackName ?? "none"} prepTrack={trace.PrepTrack ?? "none"} reviewTrack={trace.ReviewTrack ?? "none"} guideLookup={trace.GuideLookup} guideResolved={trace.GuideResolved ?? "none"} source={trace.GuideSource} trackConfidence={trace.TrackConfidence} guideConfidence={trace.GuideConfidence} status={trace.GuideStatus}");
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
        ChatMessages.Add(isReviewMode ? $"You (review): {message}" : $"You: {message}");
        var pipeline = CoachQueryPipeline.Resolve(
            ActiveSession,
            message,
            coachEngine,
            CurrentCoachContext(),
            BuildCoachEvidence(),
            userPreferences.Coach);
        AppendCoachChatLines(pipeline.FinalWritten);
    }

    private CoachEvidenceBundle BuildCoachEvidence()
    {
        return evidenceBuilder.Build(new CoachEvidenceInput(
            ActiveSession,
            ActiveTraceSnapshots,
            ActiveSession.Events,
            sessionAnalytics,
            sessionLapIntelligence,
            sessionTyreIntelligence,
            sessionDriverPerformance,
            sessionStrategy,
            traceTimeline,
            KnowledgeSources.Select(item => item.Source).ToArray(),
            liveRaceContext,
            trackMemoryRecord,
            trackMemoryComparison,
            previousStoredSessionMemory,
            recentStoredSessionMemories,
            currentStrategyKnowledge,
            currentTrackCarKnowledge,
            cachedWebResearch,
            cachedTrackGuide,
            currentOpponentIntelligence,
            currentDriverCoaching,
            isReviewMode ? reviewSessionTrack : null));
    }

    private void RefreshStrategyKnowledge()
    {
        try
        {
            currentStrategyKnowledge = StrategyKnowledgeService.Build(new StrategyKnowledgeInput(
                liveRaceContext.TrackName ?? PrepTrack,
                liveRaceContext.CarName ?? PrepCar,
                liveRaceContext.CarClass,
                liveRaceContext.SessionType ?? PrepSessionType,
                liveRaceContext.TotalLaps ?? liveRaceContext.LapsRemaining,
                ActiveSession,
                sessionAnalytics,
                sessionStrategy,
                sessionTyreIntelligence,
                trackMemoryRecord,
                previousStoredSessionMemory,
                cachedTrackGuide,
                CurrentPrepPlan(),
                userPreferences.Strategy.FuelSafetyMarginLaps));

            strategyKnowledgeTrackLabel = string.IsNullOrWhiteSpace(currentStrategyKnowledge.TrackName)
                ? "unavailable"
                : currentStrategyKnowledge.TrackName;
            strategyKnowledgeCarClassLabel = string.IsNullOrWhiteSpace(currentStrategyKnowledge.CarClass)
                ? (string.IsNullOrWhiteSpace(currentStrategyKnowledge.CarName) ? "unavailable" : currentStrategyKnowledge.CarName)
                : $"{currentStrategyKnowledge.CarName ?? "unknown car"} / {currentStrategyKnowledge.CarClass}";
            strategyKnowledgeRecommendedFuelLabel = currentStrategyKnowledge.RecommendedStartingFuelLiters is { } fuel
                ? StrategyKnowledgeFormatting.FormatLiters(fuel)
                : currentStrategyKnowledge.ExpectedFuelPerLap is { } burn
                    ? $"{StrategyKnowledgeFormatting.FormatLiters(burn)}/lap"
                    : "-";
            strategyKnowledgeConfidenceLabel = currentStrategyKnowledge.ConfidenceLabel;
            strategyKnowledgeSourceLabel = currentStrategyKnowledge.SourceLabel;
        }
        catch (Exception exception)
        {
            currentStrategyKnowledge = null;
            strategyKnowledgeTrackLabel = "unavailable";
            strategyKnowledgeCarClassLabel = "unavailable";
            strategyKnowledgeRecommendedFuelLabel = "-";
            strategyKnowledgeConfidenceLabel = "-";
            strategyKnowledgeSourceLabel = "unavailable";
            ReportOptionalPanelFailure("Strategy knowledge", exception);
        }
    }

    private void RefreshTrackCarKnowledge()
    {
        try
        {
            var track = TrackGuideZoneMapper.CanonicalizeTrackName(
                reviewSessionTrack
                    ?? liveRaceContext.TrackName
                    ?? PrepTrack);
            currentTrackCarKnowledge = TrackCarKnowledgeService.Build(new TrackCarKnowledgeInput(
                track,
                liveRaceContext.CarName ?? PrepCar,
                liveRaceContext.CarClass ?? TrackCarKnowledgeCatalog.NormalizeCarClass(PrepCar),
                ActiveSession,
                sessionAnalytics,
                trackMemoryRecord,
                previousStoredSessionMemory));

            if (currentTrackCarKnowledge is not { IsAvailable: true })
            {
                trackIntelligenceTrackLabel = string.IsNullOrWhiteSpace(track) ? "unavailable" : track;
                trackIntelligenceCarClassLabel = "unavailable";
                trackIntelligenceBrakeDemandLabel = "-";
                trackIntelligenceTyreDemandLabel = "-";
                trackIntelligenceFuelExpectationLabel = "-";
                trackIntelligenceSetupFocusLabel = "-";
                trackIntelligenceOvertakingZonesLabel = "-";
                return;
            }

            trackIntelligenceTrackLabel = currentTrackCarKnowledge.TrackName;
            trackIntelligenceCarClassLabel = string.IsNullOrWhiteSpace(currentTrackCarKnowledge.CarClass)
                ? currentTrackCarKnowledge.CarName ?? "unknown"
                : currentTrackCarKnowledge.CarClass;
            trackIntelligenceBrakeDemandLabel = ShortenPanelText(currentTrackCarKnowledge.BrakeDemand);
            trackIntelligenceTyreDemandLabel = ShortenPanelText(currentTrackCarKnowledge.TyreWearExpectation);
            trackIntelligenceFuelExpectationLabel = currentTrackCarKnowledge.LiveFuelPerLapLiters is { } live
                ? $"{StrategyKnowledgeFormatting.FormatLiters(live)}/lap (live)"
                : currentTrackCarKnowledge.BaselineFuelPerLapLiters is { } baseline
                    ? $"{StrategyKnowledgeFormatting.FormatLiters(baseline)}/lap (knowledge)"
                    : ShortenPanelText(currentTrackCarKnowledge.FuelUsageExpectation);
            trackIntelligenceSetupFocusLabel = FormatProfileItems(currentTrackCarKnowledge.SetupPriorities);
            trackIntelligenceOvertakingZonesLabel = FormatProfileItems(currentTrackCarKnowledge.OvertakingZones);
        }
        catch (Exception exception)
        {
            currentTrackCarKnowledge = null;
            trackIntelligenceTrackLabel = "unavailable";
            trackIntelligenceCarClassLabel = "-";
            trackIntelligenceBrakeDemandLabel = "-";
            trackIntelligenceTyreDemandLabel = "-";
            trackIntelligenceFuelExpectationLabel = "-";
            trackIntelligenceSetupFocusLabel = "-";
            trackIntelligenceOvertakingZonesLabel = "-";
            ReportOptionalPanelFailure("Track intelligence", exception);
        }
    }

    private static string ShortenPanelText(string value) =>
        value.Length <= 96 ? value : value[..93] + "...";

    private void AppendCoachChatLines(CoachMessage answer)
    {
        var prefix = isReviewMode ? "Coach (review):" : "Coach:";
        var uncertainty = string.IsNullOrWhiteSpace(answer.Uncertainty) ? "" : $" ({answer.Uncertainty})";
        ChatMessages.Add($"{prefix} {answer.Content}{uncertainty}");
        if (answer.EvidencePackets.Count > 0 && userPreferences.Coach.EvidenceBulletsEnabled)
        {
            var bullets = string.Join(
                Environment.NewLine,
                answer.EvidencePackets.Select(packet => $"- {packet.Summary}: {packet.Explanation}"));
            ChatMessages.Add($"{prefix}{Environment.NewLine}Evidence:{Environment.NewLine}{bullets}");
        }
    }

    private async Task SavePreferencesAsync()
    {
        userPreferences = CollectPreferencesFromView();
        await profilePreferencesService.SaveAsync(userPreferences);
        ApplyUserPreferences();
        RefreshAnalytics();
        ChatMessages.Add("Coach: Driver profile and preferences saved.");
    }

    private void BindPreferencesToView(UserPreferencesBundle preferences)
    {
        userPreferences = preferences;
        PrefDriverName = preferences.Driver.DriverName ?? "";
        PrefPreferredLanguage = preferences.Driver.PreferredLanguage;
        PrefPreferredUnits = preferences.Driver.PreferredUnits;
        PrefExperienceLevel = preferences.Driver.ExperienceLevel;
        PrefDrivingStyle = preferences.Driver.DrivingStyle;
        PrefResponseLength = preferences.Coach.ResponseLength;
        PrefCalloutAggressiveness = preferences.Coach.CalloutAggressiveness;
        PrefVoiceEnabledDefault = preferences.Coach.VoiceEnabledDefault;
        PrefSpeechRecognitionProvider = preferences.Coach.SpeechRecognitionProvider;
        PrefPushToTalkHotkey = preferences.Coach.PushToTalkHotkey;
        PrefVoiceInputConfirmationsEnabled = preferences.Coach.VoiceInputConfirmationsEnabled;
        PrefTranscriptConfirmationEnabled = preferences.Coach.TranscriptConfirmationEnabled;
        PrefMicrophoneDeviceNumber = preferences.Coach.MicrophoneDeviceNumber.ToString(CultureInfo.InvariantCulture);
        OnPropertyChanged(nameof(SelectedMicrophoneDeviceNumber));
        PrefEvidenceBulletsEnabled = preferences.Coach.EvidenceBulletsEnabled;
        PrefQuietModeEnabled = preferences.Coach.QuietModeEnabled;
        PrefMinimalEngineerEnabled = preferences.Coach.MinimalEngineerEnabled;
        PrefFuelSafetyMarginLaps = preferences.Strategy.FuelSafetyMarginLaps.ToString("0.0", CultureInfo.InvariantCulture);
        PrefPitAggressiveness = preferences.Strategy.PitRecommendationAggressiveness;
        PrefTyreRiskSensitivity = preferences.Strategy.TyreRiskSensitivity;
        PrefPitStrategyPreference = preferences.Strategy.PitStrategyPreference;
    }

    private UserPreferencesBundle CollectPreferencesFromView()
    {
        var fuelMargin = double.TryParse(PrefFuelSafetyMarginLaps, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsedMargin)
            ? parsedMargin
            : StrategyPreferencesRecord.Default.FuelSafetyMarginLaps;
        return new UserPreferencesBundle(
            UserPreferencesNormalizer.NormalizeDriver(new DriverProfileRecord(
                DriverName: PrefDriverName,
                PreferredLanguage: PrefPreferredLanguage,
                PreferredUnits: PrefPreferredUnits,
                ExperienceLevel: PrefExperienceLevel,
                DrivingStyle: PrefDrivingStyle)),
            UserPreferencesNormalizer.NormalizeCoach(new CoachPreferencesRecord(
                ResponseLength: PrefResponseLength,
                CalloutAggressiveness: PrefCalloutAggressiveness,
                VoiceEnabledDefault: PrefVoiceEnabledDefault,
                SpeechRecognitionProvider: PrefSpeechRecognitionProvider,
                PushToTalkHotkey: PrefPushToTalkHotkey,
                VoiceInputConfirmationsEnabled: PrefVoiceInputConfirmationsEnabled,
                TranscriptConfirmationEnabled: PrefTranscriptConfirmationEnabled,
                MicrophoneDeviceNumber: int.TryParse(PrefMicrophoneDeviceNumber, out var micDevice) ? micDevice : -1,
                EvidenceBulletsEnabled: PrefEvidenceBulletsEnabled,
                QuietModeEnabled: PrefQuietModeEnabled,
                MinimalEngineerEnabled: PrefMinimalEngineerEnabled,
                VoiceInputCooldownSeconds: userPreferences.Coach.VoiceInputCooldownSeconds)),
            UserPreferencesNormalizer.NormalizeStrategy(new StrategyPreferencesRecord(
                FuelSafetyMarginLaps: fuelMargin,
                PitRecommendationAggressiveness: PrefPitAggressiveness,
                TyreRiskSensitivity: PrefTyreRiskSensitivity,
                PitStrategyPreference: PrefPitStrategyPreference)));
    }

    private void ApplyUserPreferences()
    {
        appSettings = ProfilePreferencesService.MergeAppSettings(appSettings, userPreferences);
        calloutManager.ConfigureCalloutAggressiveness(userPreferences.Coach.CalloutAggressiveness);
        strategyCalloutManager.ConfigureCooldown(
            StrategyPreferencesMapper.CalloutCooldown(userPreferences.Strategy, userPreferences.Coach));
        voiceService.SetVoiceEnabled(userPreferences.Coach.VoiceEnabledDefault);
        if (userPreferences.Coach.VoiceEnabledDefault)
        {
            voiceService.SetMuted(false);
        }
        voiceInputService.Configure(BuildVoiceInputOptions(appSettings, userPreferences.Coach));
        voiceInputService.SetEnabled(userPreferences.Coach.VoiceEnabledDefault);
        pushToTalkHotkey = ParsePushToTalkHotkeySafely(userPreferences.Coach.PushToTalkHotkey);
        RaiseVoiceProperties();
        OnPropertyChanged(nameof(EngineerModeLabel));
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
        voiceInputService.Dispose();
        var summaryMarkdown = coachEngine.GeneratePostSessionReport(session, CurrentPrepPlan());
        await storageService.SavePostSessionReportAsync(session.SessionId, summaryMarkdown);
        await storageService.SavePostSessionSummaryAsync(session.SessionId, summaryMarkdown, CurrentSessionSummaryObject());
        await storageService.EndSessionAsync(session.SessionId, DateTimeOffset.UtcNow, summaryMarkdown, CurrentSessionSummaryObject());
        await UpsertTrackMemoryAsync(summaryMarkdown);
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

        reviewSessionTrack = TrackGuideZoneMapper.ResolveReviewSessionTrack(
            bundle.Track,
            SelectedSession.Track,
            bundle.Snapshots);
        var car = bundle.Car ?? "";
        var track = reviewSessionTrack ?? "";
        var startedLabel = bundle.StartedAt.LocalDateTime.ToString("g", CultureInfo.CurrentCulture);
        SessionLabel = $"Review Mode | {startedLabel} | {car} | {track}".Trim(' ', '|');

        loadedPrepPlan = await storageService.LoadRacePrepPlanAsync(bundle.Car, reviewSessionTrack ?? bundle.Track);
        if (!string.IsNullOrWhiteSpace(track))
        {
            PrepTrack = track;
        }

        if (!string.IsNullOrWhiteSpace(car))
        {
            PrepCar = car;
        }

        if (loadedPrepPlan is not null)
        {
            ApplyPrepPlan(loadedPrepPlan);
            if (!string.IsNullOrWhiteSpace(reviewSessionTrack))
            {
                PrepTrack = reviewSessionTrack;
            }
            else if (string.IsNullOrWhiteSpace(PrepTrack) && !string.IsNullOrWhiteSpace(track))
            {
                PrepTrack = track;
            }

            if (string.IsNullOrWhiteSpace(PrepCar) && !string.IsNullOrWhiteSpace(car))
            {
                PrepCar = car;
            }
        }
        else if (!string.IsNullOrWhiteSpace(car) || !string.IsNullOrWhiteSpace(track))
        {
            await RefreshKnowledgeAsync();
        }

        if (cachedTrackGuide is not null
            && !string.IsNullOrWhiteSpace(reviewSessionTrack)
            && !TrackGuideMatcher.Matches(cachedTrackGuide, reviewSessionTrack))
        {
            cachedTrackGuide = null;
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
        await RefreshTrackResearchAsync(track);
        await RefreshStoredSessionMemoryAsync(track, car);
        RefreshAnalytics();
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
        reviewSessionTrack = null;
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

    public void ReportStartupWarning(string message)
    {
        if (Application.Current?.Dispatcher is { } dispatcher)
        {
            dispatcher.Invoke(() => ChatMessages.Add($"Startup: {message}"));
        }
        else
        {
            ChatMessages.Add($"Startup: {message}");
        }

        RaiseVoiceProperties();
    }

    private VoiceInputService CreatePlaceholderVoiceInputService(AppSettings settings)
    {
        var options = BuildVoiceInputOptions(settings, CoachPreferencesRecord.FromAppSettings(settings));
        return VoiceInputStartup.CreateUnavailableService("Voice input loads on first mic use.", options);
    }

    private void WireVoiceInputServiceEvents(VoiceInputService service)
    {
        service.QueryRecognized += OnVoiceQueryRecognized;
        service.TranscriptRejected += OnVoiceTranscriptRejected;
        service.TranscriptPendingConfirmation += OnVoiceTranscriptPendingConfirmation;
        service.DiagnosticRaised += OnVoiceDiagnosticRaised;
        service.StateChanged += (_, _) => RaiseVoiceProperties();
    }

    private void UnwireVoiceInputServiceEvents(VoiceInputService service)
    {
        service.QueryRecognized -= OnVoiceQueryRecognized;
        service.TranscriptRejected -= OnVoiceTranscriptRejected;
        service.TranscriptPendingConfirmation -= OnVoiceTranscriptPendingConfirmation;
        service.DiagnosticRaised -= OnVoiceDiagnosticRaised;
    }

    private bool TryEnsureVoiceInputRuntime()
    {
        if (voiceInputRuntimeInitialized)
        {
            return voiceInputRuntimeError is null;
        }

        voiceInputRuntimeInitialized = true;
        var wasEnabled = voiceInputService.VoiceInputEnabled;
        var wasMuted = voiceInputService.InputMuted;

        if (!NaudioVoiceRuntime.TryCreateVoiceInputService(
                appSettings,
                BuildVoiceInputOptions(appSettings, userPreferences.Coach),
                message => AddStartupChat($"Startup: {message}"),
                out var service,
                out var report,
                out var errorMessage))
        {
            voiceInputRuntimeError = errorMessage ?? NaudioVoiceRuntime.BlockedWasapiMessage;
            if (service is not null)
            {
                ReplaceVoiceInputService(service, wasEnabled, wasMuted);
            }

            if (report is not null)
            {
                LogVoiceInitializationReport(report);
            }

            ReportVoiceInputUnavailable(voiceInputRuntimeError);
            return false;
        }

        ReplaceVoiceInputService(service!, wasEnabled, wasMuted);
        if (report is not null)
        {
            LogVoiceInitializationReport(report);
        }

        TryRefreshMicrophoneDevices();
        RaiseVoiceProperties();
        return true;
    }

    private void ReplaceVoiceInputService(VoiceInputService service, bool wasEnabled, bool wasMuted)
    {
        UnwireVoiceInputServiceEvents(voiceInputService);
        voiceInputService.Dispose();
        voiceInputService = service;
        WireVoiceInputServiceEvents(voiceInputService);
        voiceInputService.Configure(BuildVoiceInputOptions(appSettings, userPreferences.Coach));
        voiceInputService.SetEnabled(wasEnabled);
        voiceInputService.SetInputMuted(wasMuted);
    }

    private void LogVoiceInitializationReport(VoiceInitializationReport report)
    {
        AddStartupChat($"Voice diag: Provider selected — {report.SelectedProvider}");
        AddStartupChat(report.NaudioAvailable
            ? $"Voice diag: Microphones detected — {report.MicrophoneDeviceCount} ({report.MicrophoneSummary})"
            : $"Voice diag: Microphone enumeration unavailable — {report.NaudioError ?? "unknown error"}");
        AddStartupChat($"Voice diag: Active provider — {report.ActiveProviderName} ({(report.ProviderAvailable ? "available" : "unavailable")})");
        if (!string.IsNullOrWhiteSpace(report.ProviderAvailabilityDetail))
        {
            AddStartupChat($"Voice diag: Provider detail — {report.ProviderAvailabilityDetail}");
        }

        if (!string.Equals(report.FailedComponent, "none", StringComparison.OrdinalIgnoreCase))
        {
            AddStartupChat($"Voice diag: Failed component — {report.FailedComponent}");
        }

        if (!string.IsNullOrWhiteSpace(report.InitializationException))
        {
            AddStartupChat($"Voice diag: Initialization exception — {report.InitializationException}");
        }

        AddStartupChat($"Voice diag: {report.SummaryForChat}");
    }

    private void TryRefreshMicrophoneDevices()
    {
        if (!NaudioVoiceRuntime.TryListMicrophoneDevices(out var devices, out var errorMessage))
        {
            if (!string.IsNullOrWhiteSpace(errorMessage))
            {
                ReportVoiceInputUnavailable(errorMessage);
            }

            return;
        }

        MicrophoneDevices.Clear();
        foreach (var device in devices)
        {
            MicrophoneDevices.Add(device);
        }
    }

    private void ReportVoiceInputUnavailable(string message)
    {
        if (voiceInputUnavailableReported)
        {
            return;
        }

        voiceInputUnavailableReported = true;
        AddStartupChat(message);
        RaiseVoiceProperties();
    }

    private IVoiceOutput CreateVoiceOutputSafely()
    {
        try
        {
            return WindowsSpeechOutput.CreateOrFallback(startupWarnings.Add);
        }
        catch (Exception exception)
        {
            startupWarnings.Add($"Voice output unavailable. {exception.Message}");
            return new RecordingVoiceOutput();
        }
    }

    private void ToggleVoiceInputMute()
    {
        var nextMuted = !voiceInputService.InputMuted;
        if (!nextMuted && !TryEnsureVoiceInputRuntime())
        {
            return;
        }

        voiceInputService.SetInputMuted(nextMuted);
        RaiseVoiceProperties();
    }

    public bool MatchesPushToTalkHotkey(KeyEventArgs args) => pushToTalkHotkey.Matches(args);

    public void BeginPushToTalk()
    {
        if (!voiceService.VoiceEnabled)
        {
            return;
        }

        if (!TryEnsureVoiceInputRuntime())
        {
            return;
        }

        voiceInputService.BeginPushToTalk();
        RaiseVoiceProperties();
    }

    private static VoiceInputOptions BuildVoiceInputOptions(AppSettings settings, CoachPreferencesRecord coach)
    {
        return new VoiceInputOptions
        {
            ConfirmationsEnabled = coach.VoiceInputConfirmationsEnabled,
            TranscriptConfirmationEnabled = coach.TranscriptConfirmationEnabled,
            QueryCooldown = TimeSpan.FromSeconds(coach.VoiceInputCooldownSeconds),
            MinimumConfidence = settings.SpeechMinimumConfidence,
            TranscriptGate = TranscriptGateOptions.Default with
            {
                MinimumConfidence = settings.SpeechMinimumConfidence
            }
        };
    }

    private PushToTalkHotkey ParsePushToTalkHotkeySafely(string? configuredHotkey)
    {
        if (PushToTalkHotkey.TryParse(configuredHotkey, out var parsedHotkey))
        {
            return parsedHotkey;
        }

        startupWarnings.Add($"Push-to-talk hotkey '{configuredHotkey ?? ""}' is invalid. Using F6.");
        return PushToTalkHotkey.Default;
    }

    public void EndPushToTalk()
    {
        voiceInputService.EndPushToTalk();
        RaiseVoiceProperties();
    }

    private void OnVoiceDiagnosticRaised(object? sender, SpeechRecognitionDiagnosticEventArgs e)
    {
        if (Application.Current?.Dispatcher is { } dispatcher)
        {
            dispatcher.Invoke(() => AppendVoiceDiagnostic(e));
        }
        else
        {
            AppendVoiceDiagnostic(e);
        }
    }

    private void AppendVoiceDiagnostic(SpeechRecognitionDiagnosticEventArgs e)
    {
        var detail = string.IsNullOrWhiteSpace(e.Detail) ? e.Message : $"{e.Message} — {e.Detail}";
        ChatMessages.Add($"Voice diag [{e.Stage}]: {detail}");
        if (e.Stage.Equals("No speech timeout", StringComparison.OrdinalIgnoreCase))
        {
            ChatMessages.Add($"Coach: {e.Message} {e.Detail}".Trim());
        }

        RaiseVoiceProperties();
    }

    private void OnVoiceTranscriptRejected(object? sender, VoiceInputRejectionEventArgs e)
    {
        Application.Current.Dispatcher.Invoke(() =>
        {
            ChatMessages.Add($"Coach: {e.SpokenMessage}");
            if (!string.IsNullOrWhiteSpace(e.RawTranscript))
            {
                ChatMessages.Add($"Voice diag [rejected]: {e.RawTranscript} — {e.Reason}");
            }

            voiceService.SpeakDirectAnswer(e.SpokenMessage, userPreferences.Coach);
            LastCallout = voiceService.LastSpokenCallout;
            RaiseVoiceProperties();
        });
    }

    private void OnVoiceTranscriptPendingConfirmation(object? sender, VoiceInputPendingTranscriptEventArgs e)
    {
        Application.Current.Dispatcher.Invoke(() =>
        {
            ChatMessages.Add($"Voice pending: {e.Text} (confidence {e.Confidence:0.00}) — confirm to route.");
            RaiseVoiceProperties();
            (ConfirmTranscriptCommand as RelayCommand)?.RaiseCanExecuteChanged();
            (RejectTranscriptCommand as RelayCommand)?.RaiseCanExecuteChanged();
        });
    }

    private void ConfirmPendingTranscript()
    {
        if (voiceInputService.ConfirmPendingTranscript())
        {
            ChatMessages.Add("Voice: transcript confirmed.");
        }

        RaiseVoiceProperties();
        (ConfirmTranscriptCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (RejectTranscriptCommand as RelayCommand)?.RaiseCanExecuteChanged();
    }

    private void RejectPendingTranscript()
    {
        voiceInputService.RejectPendingTranscript();
        RaiseVoiceProperties();
        (ConfirmTranscriptCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (RejectTranscriptCommand as RelayCommand)?.RaiseCanExecuteChanged();
    }

    private void OnVoiceQueryRecognized(object? sender, VoiceInputQueryEventArgs e)
    {
        Application.Current.Dispatcher.Invoke(() => HandleVoiceQuery(e.Text, e.Confidence));
    }

    private void HandleVoiceQuery(string query, float? confidence = null)
    {
        ChatMessages.Add(isReviewMode ? $"You voice (review): {query}" : $"You voice: {query}");
        var transcriptContext = confidence is null
            ? null
            : new CoachQueryTranscriptContext(
                RawTranscript: query,
                NormalizedTranscript: CoachQueryTopicClassifier.NormalizeQuery(query),
                Confidence: confidence);
        var result = voiceService.HandleSpokenQuery(
            ActiveSession,
            query,
            coachEngine,
            CurrentCoachContext(),
            BuildCoachEvidence(),
            confirmQuery: voiceInputService.ConfirmationsEnabled,
            preferences: userPreferences.Coach,
            transcriptContext: transcriptContext);
        AppendCoachChatLines(result.WrittenResponse);
        ChatMessages.Add(result.Spoken
            ? $"Voice diag: spoken — ui='{result.FinalDisplayedText}' summary='{result.GeneratedSummary}' tts='{result.FinalTtsPayload}'"
            : $"Voice diag: not spoken — {result.SpeechDiagnostic} ui='{result.FinalDisplayedText}' summary='{result.GeneratedSummary}'");
        LastCallout = result.Spoken ? result.SpokenResponse : voiceService.LastSpokenCallout;
        RaiseVoiceProperties();
    }

    private void ToggleTts()
    {
        voiceService.SetMuted(!voiceService.EngineerMuted);
        RaiseVoiceProperties();
    }

    private void ToggleVoice()
    {
        var enabled = !voiceService.VoiceEnabled;
        if (enabled && !TryEnsureVoiceInputRuntime())
        {
            return;
        }

        voiceService.SetVoiceEnabled(enabled);
        voiceInputService.SetEnabled(enabled);
        RaiseVoiceProperties();
    }

    private async Task RunMicCalibrationTestAsync()
    {
        if (!TryEnsureVoiceInputRuntime())
        {
            MicCalibrationStatus = voiceInputRuntimeError ?? NaudioVoiceRuntime.BlockedWasapiMessage;
            ChatMessages.Add($"Voice diag [mic test]: {MicCalibrationStatus}");
            RaiseVoiceProperties();
            return;
        }

        MicCalibrationStatus = "Recording 3 seconds... speak normally.";
        ChatMessages.Add("Coach: Mic test started — speak normally for 3 seconds.");
        RaiseVoiceProperties();

        var deviceNumber = SelectedMicrophoneDeviceNumber;
        var (result, errorMessage) = await NaudioVoiceRuntime.TryRunMicCalibrationAsync(
            deviceNumber,
            TimeSpan.FromSeconds(3));

        if (result is null)
        {
            MicCalibrationStatus = errorMessage ?? "Mic test unavailable.";
            ChatMessages.Add($"Voice diag [mic test failed]: {MicCalibrationStatus}");
            RaiseVoiceProperties();
            return;
        }

        MicCalibrationStatus =
            $"Peak {result.PeakRms:0} | Raw {result.RawPeakRms:0} | Conv {result.ConvertedPeakRms:0} | Quality {result.SignalQualityLabel} | {result.AssessmentMessage}";
        ChatMessages.Add($"Voice diag [mic test]: {result.CaptureFormatSummary}");
        ChatMessages.Add($"Voice diag [mic test]: Raw={result.RawPeakRms:0} Conv={result.ConvertedPeakRms:0} Peak={result.PeakRms:0} Quality={result.SignalQualityLabel} — {result.AssessmentMessage}");
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
        var track = EmptyToNull(liveRaceContext.TrackName) ?? EmptyToNull(PrepTrack);
        var sources = await storageService.ListKnowledgeSourcesAsync(
            EmptyToNull(PrepCar),
            track,
            EmptyToNull(PrepSessionType),
            null);
        SetKnowledgeSources(sources);
    }

    private void SetKnowledgeSources(IReadOnlyList<KnowledgeSource> sources)
    {
        if (Application.Current?.Dispatcher is not { } dispatcher)
        {
            KnowledgeSources.Clear();
            foreach (var source in sources)
            {
                KnowledgeSources.Add(new KnowledgeSourceItem(source));
            }

            return;
        }

        dispatcher.Invoke(() =>
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
            isReviewMode ? reviewSession?.LastLap?.LapNumber : session.CurrentLap,
            timelineCursorProgress));
        OnPropertyChanged(nameof(TraceTimeline));
    }

    private void RefreshAnalytics()
    {
        UpdateSessionContext();
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

        tyreIntelligenceTitle = isReviewMode ? "Tyre Intelligence (Review Session)" : "Tyre Intelligence (Live Session)";
        sessionTyreIntelligence = tyreIntelligenceService.Analyze(new TyreIntelligenceInput(
            ActiveSession,
            ActiveTraceSnapshots.Count >= 2 ? ActiveTraceSnapshots : null,
            ActiveSession.Events,
            sessionContextAssessment.Activity,
            sessionContextAssessment.Phase,
            metrics,
            intelligence));
        tyreGripConfidence = sessionTyreIntelligence.HasReliableData
            ? $"{sessionTyreIntelligence.GripConfidenceLabel} — {sessionTyreIntelligence.CoachingMessage}"
            : sessionTyreIntelligence.Availability;
        tyreWarmupState = sessionTyreIntelligence.HasReliableData
            ? sessionTyreIntelligence.WarmupState.ToString()
            : "-";
        tyreReadiness = sessionTyreIntelligence.HasReliableData
            ? sessionTyreIntelligence.Readiness.ToString()
            : "-";
        tyreOverheatingRisk = sessionTyreIntelligence.HasReliableData
            ? sessionTyreIntelligence.OverheatingRisk
            : "-";

        performancePanelTitle = isReviewMode ? "Performance Intelligence (Review Session)" : "Performance Intelligence (Live Session)";
        try
        {
            RefreshRaceAwareness();
        }
        catch (Exception exception)
        {
            ReportOptionalPanelFailure("Race awareness", exception);
        }

        try
        {
            sessionDriverPerformance = driverPerformanceService.Analyze(new DriverPerformanceInput(
                ActiveSession,
                ActiveTraceSnapshots.Count >= 4 ? ActiveTraceSnapshots : null,
                ActiveSession.Events,
                metrics,
                intelligence,
                trackMemoryRecord,
                trackMemoryComparison,
                ActiveSession.LastLap?.LapNumber));
            var performance = TrackGuideZoneMapper.MapPerformance(
                sessionDriverPerformance,
                ResolveActiveTrackGuide());
            performanceBiggestLossLabel = performance.Availability != "Available"
                ? performance.Availability
                : performance.BiggestTimeLoss is { } loss
                    ? $"{loss.Zone.Label}: {string.Join(", ", loss.Behaviors)} ({loss.EstimatedLossSeconds?.ToString("+0.000;-0.000;0.000", CultureInfo.InvariantCulture) ?? "n/a"}s)"
                    : "No clear zone loss yet.";
            performanceMainWeaknessLabel = performance.MainWeakness ?? performance.Availability;
            performanceBrakingQualityLabel = performance.BrakingQuality.Detail;
            performanceThrottleQualityLabel = performance.ThrottleQuality.Detail;
            performanceConsistencyLabel = performance.Consistency.Detail;
            performanceCurrentVsBestLabel = performance.CurrentVsBestDeltaSeconds is { } delta
                ? $"Δ {delta:+0.000;-0.000;0.000}s vs session best"
                : performance.Availability;
            performanceStoredBaselineLabel = performance.StoredBaselineComparison
                ?? trackMemoryComparison?.Summary
                ?? "no stored baseline";
        }
        catch (Exception exception)
        {
            sessionDriverPerformance = SessionDriverPerformance.Unavailable(SessionDriverPerformance.NeedCleanLapMessage);
            performanceBiggestLossLabel = "Performance analysis unavailable.";
            performanceMainWeaknessLabel = exception.Message;
            performanceBrakingQualityLabel = "-";
            performanceThrottleQualityLabel = "-";
            performanceConsistencyLabel = "-";
            performanceCurrentVsBestLabel = "-";
            performanceStoredBaselineLabel = "no stored baseline";
            ReportOptionalPanelFailure("Performance intelligence", exception);
        }

        try
        {
            RefreshDriverCoaching();
        }
        catch (Exception exception)
        {
            currentDriverCoaching = DriverCoachingRecommendation.Unavailable;
            driverCoachingTrendLabel = DriverCoachingRecommendation.Unavailable.ProgressTrendSummary;
            driverCoachingBiggestWeaknessLabel = DriverCoachingRecommendation.Unavailable.Availability;
            driverCoachingStrongestAreaLabel = "-";
            driverCoachingConsistencyLabel = "-";
            driverCoachingPreviousDeltaLabel = "no previous session data";
            driverCoachingTopTargetsLabel = "-";
            ReportOptionalPanelFailure("Driver coaching", exception);
        }

        strategyPanelTitle = isReviewMode ? "Strategy (Review Session)" : "Strategy (Live Session)";
        try
        {
            var rawStrategy = strategyEngine.Analyze(new StrategyInput(
                ActiveSession,
                metrics,
                intelligence,
                CurrentPrepPlan(),
                ActiveSession.Events,
                userPreferences.Strategy));
            sessionStrategy = StrategyGate.Apply(rawStrategy, sessionContextAssessment, ActiveSession);
            strategyConfidenceLabel = sessionStrategy.StrategyConfidenceLabel;
            var strategy = sessionStrategy;
            strategyFuelRisk = strategy.Fuel.RiskLevel.ToString();
            strategyLapsRemaining = strategy.Fuel.LapsRemaining?.ToString("0.0", CultureInfo.InvariantCulture)
                ?? strategy.Fuel.Availability;
            strategyPitRecommendation = strategy.Pit.Recommendation == PitRecommendation.Unknown
                ? strategy.Pit.Availability
                : $"{strategy.Pit.Recommendation} — {strategy.Pit.RecommendationReason}";
            strategyTyreRisk = strategy.TyreRisk.RiskLevel == TyreRiskLevel.Unknown
                ? strategy.TyreRisk.Availability
                : $"{strategy.TyreRisk.RiskLevel} ({strategy.TyreRisk.RiskScore0To100:0}/100)";
            strategySummary = strategy.Summary;
            RefreshStrategyKnowledge();
            RefreshTrackCarKnowledge();
        }
        catch (Exception exception)
        {
            sessionStrategy = SessionStrategy.Empty;
            strategyFuelRisk = "-";
            strategyLapsRemaining = "-";
            strategyPitRecommendation = "-";
            strategyTyreRisk = "-";
            strategySummary = "Strategy analysis unavailable.";
            strategyKnowledgeTrackLabel = "unavailable";
            strategyKnowledgeCarClassLabel = "unavailable";
            strategyKnowledgeRecommendedFuelLabel = "-";
            strategyKnowledgeConfidenceLabel = "-";
            strategyKnowledgeSourceLabel = "unavailable";
            ReportOptionalPanelFailure("Strategy", exception);
        }

        _ = RefreshTrackMemorySafeAsync();

        RaiseAnalyticsProperties();

        if (!isReviewMode)
        {
            TrySpeakStrategyCallout();
        }
    }

    private void TrySpeakStrategyCallout()
    {
        var timestamp = DateTimeOffset.UtcNow;
        if (!voiceService.InteractionGate.IsAutomaticCalloutAllowed(timestamp))
        {
            var reason = voiceService.InteractionGate.AutomaticSuppressionReason(timestamp);
            strategyCalloutManager.NoteExternalSuppression($"Strategy callout suppressed: {reason}.");
            return;
        }

        var callout = strategyCalloutManager.TryCreateCallout(
            sessionStrategy,
            timestamp,
            sessionContextAssessment,
            userPreferences.Coach);
        if (callout is null)
        {
            return;
        }

        if (voiceService.SpeakAutomaticCallout(callout, timestamp))
        {
            LastCallout = callout;
        }
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
        OnPropertyChanged(nameof(TyreIntelligenceTitle));
        OnPropertyChanged(nameof(TyreGripConfidence));
        OnPropertyChanged(nameof(TyreWarmupState));
        OnPropertyChanged(nameof(TyreReadiness));
        OnPropertyChanged(nameof(TyreOverheatingRisk));
        OnPropertyChanged(nameof(RaceAwarenessPanelTitle));
        OnPropertyChanged(nameof(RaceTrackLabel));
        OnPropertyChanged(nameof(RaceCarLabel));
        OnPropertyChanged(nameof(RaceSessionTypeLabel));
        OnPropertyChanged(nameof(RacePositionLabel));
        OnPropertyChanged(nameof(RaceGapAheadLabel));
        OnPropertyChanged(nameof(RaceGapBehindLabel));
        OnPropertyChanged(nameof(RaceContextConfidenceLabel));
        OnPropertyChanged(nameof(RaceFieldDiagnosticsLabel));
        OnPropertyChanged(nameof(OpponentTelemetryPresentLabel));
        OnPropertyChanged(nameof(OpponentTelemetryResolvedLabel));
        OnPropertyChanged(nameof(OpponentTelemetryCandidatesLabel));
        OnPropertyChanged(nameof(TelemetryOpponentFieldsLabel));
        OnPropertyChanged(nameof(TelemetryOpponentResolvedLabel));
        OnPropertyChanged(nameof(TelemetryOpponentCandidatesLabel));
        OnPropertyChanged(nameof(RaceMemoryPreviousBestLabel));
        OnPropertyChanged(nameof(RaceMemoryPreviousAverageLabel));
        OnPropertyChanged(nameof(OpponentPositionLabel));
        OnPropertyChanged(nameof(OpponentCarAheadLabel));
        OnPropertyChanged(nameof(OpponentCarBehindLabel));
        OnPropertyChanged(nameof(OpponentGapAheadLabel));
        OnPropertyChanged(nameof(OpponentGapBehindLabel));
        OnPropertyChanged(nameof(OpponentTrendLabel));
        OnPropertyChanged(nameof(OpponentBattleStatusLabel));
        OnPropertyChanged(nameof(OpponentConfidenceLabel));
        OnPropertyChanged(nameof(DriverCoachingPanelTitle));
        OnPropertyChanged(nameof(DriverCoachingTrendLabel));
        OnPropertyChanged(nameof(DriverCoachingBiggestWeaknessLabel));
        OnPropertyChanged(nameof(DriverCoachingStrongestAreaLabel));
        OnPropertyChanged(nameof(DriverCoachingConsistencyLabel));
        OnPropertyChanged(nameof(DriverCoachingPreviousDeltaLabel));
        OnPropertyChanged(nameof(DriverCoachingTopTargetsLabel));
        OnPropertyChanged(nameof(SessionMemoryLastSummaryLabel));
        OnPropertyChanged(nameof(SessionMemoryKnownWeaknessesLabel));
        OnPropertyChanged(nameof(SessionDebriefPreview));
        OnPropertyChanged(nameof(TrackResearchDetectedTrackLabel));
        OnPropertyChanged(nameof(TrackGuideAvailableLabel));
        OnPropertyChanged(nameof(TrackGuideLastFetchedLabel));
        OnPropertyChanged(nameof(TrackGuideSourceCountLabel));
        OnPropertyChanged(nameof(TrackGuideBrakingZonesLabel));
        OnPropertyChanged(nameof(TrackGuideTractionZonesLabel));
        OnPropertyChanged(nameof(TrackGuideKeyCornersLabel));
        OnPropertyChanged(nameof(TrackGuideSetupNotesLabel));
        OnPropertyChanged(nameof(WebResearchDetectedTrackLabel));
        OnPropertyChanged(nameof(WebResearchDetectedCarClassLabel));
        OnPropertyChanged(nameof(WebResearchAvailableLabel));
        OnPropertyChanged(nameof(WebResearchLastFetchedLabel));
        OnPropertyChanged(nameof(WebResearchSourceCountLabel));
        OnPropertyChanged(nameof(WebResearchProviderLabel));
        OnPropertyChanged(nameof(WebResearchCacheStatusLabel));
        OnPropertyChanged(nameof(WebResearchItemsFetchedLabel));
        OnPropertyChanged(nameof(WebResearchFetchStatusLabel));
        OnPropertyChanged(nameof(WebResearchLastFetchResultLabel));
        OnPropertyChanged(nameof(WebResearchErrorMessageLabel));
        OnPropertyChanged(nameof(PerformancePanelTitle));
        OnPropertyChanged(nameof(PerformanceBiggestLossLabel));
        OnPropertyChanged(nameof(PerformanceMainWeaknessLabel));
        OnPropertyChanged(nameof(PerformanceBrakingQualityLabel));
        OnPropertyChanged(nameof(PerformanceThrottleQualityLabel));
        OnPropertyChanged(nameof(PerformanceConsistencyLabel));
        OnPropertyChanged(nameof(PerformanceCurrentVsBestLabel));
        OnPropertyChanged(nameof(PerformanceStoredBaselineLabel));
        OnPropertyChanged(nameof(StrategyPanelTitle));
        OnPropertyChanged(nameof(StrategyFuelRisk));
        OnPropertyChanged(nameof(StrategyLapsRemaining));
        OnPropertyChanged(nameof(StrategyPitRecommendation));
        OnPropertyChanged(nameof(StrategyTyreRisk));
        OnPropertyChanged(nameof(StrategySummary));
        OnPropertyChanged(nameof(StrategyKnowledgeTrackLabel));
        OnPropertyChanged(nameof(StrategyKnowledgeCarClassLabel));
        OnPropertyChanged(nameof(StrategyKnowledgeRecommendedFuelLabel));
        OnPropertyChanged(nameof(StrategyKnowledgeConfidenceLabel));
        OnPropertyChanged(nameof(StrategyKnowledgeSourceLabel));
        OnPropertyChanged(nameof(TrackIntelligenceTrackLabel));
        OnPropertyChanged(nameof(TrackIntelligenceCarClassLabel));
        OnPropertyChanged(nameof(TrackIntelligenceBrakeDemandLabel));
        OnPropertyChanged(nameof(TrackIntelligenceTyreDemandLabel));
        OnPropertyChanged(nameof(TrackIntelligenceFuelExpectationLabel));
        OnPropertyChanged(nameof(TrackIntelligenceSetupFocusLabel));
        OnPropertyChanged(nameof(TrackIntelligenceOvertakingZonesLabel));
        OnPropertyChanged(nameof(SessionModeLabel));
        OnPropertyChanged(nameof(StrategyConfidenceLabel));
        OnPropertyChanged(nameof(EngineerModeLabel));
    }

    private void RefreshRaceAwareness()
    {
        raceAwarenessPanelTitle = isReviewMode ? "Race Awareness (Review Session)" : "Race Awareness (Live Session)";
        liveRaceContext = RaceContextService.Build(
            ActiveSession,
            ActiveSession.LatestSnapshot,
            CurrentPrepPlan(),
            sessionContextAssessment);
        var telemetryDiagnostics = ActiveSession.LatestSnapshot is not null
            ? RaceContextService.BuildTelemetryDiagnostics(ActiveSession.LatestSnapshot)
            : liveRaceContext.Diagnostics;

        raceTrackLabel = liveRaceContext.TrackName ?? (string.IsNullOrWhiteSpace(PrepTrack) ? "unavailable" : PrepTrack);
        raceCarLabel = liveRaceContext.CarName ?? (string.IsNullOrWhiteSpace(PrepCar) ? "unavailable" : PrepCar);
        raceSessionTypeLabel = liveRaceContext.SessionType ?? "unavailable";
        racePositionLabel = liveRaceContext.Position is { } position
            ? liveRaceContext.TotalCars is { } total ? $"P{position}/{total}" : $"P{position}"
            : "unavailable";
        raceGapAheadLabel = liveRaceContext.GapAheadSeconds is { } gapAhead
            ? $"{gapAhead:0.000}s{(string.IsNullOrWhiteSpace(liveRaceContext.CarAhead) ? "" : $" ({liveRaceContext.CarAhead})")}"
            : "unavailable";
        raceGapBehindLabel = liveRaceContext.GapBehindSeconds is { } gapBehind
            ? $"{gapBehind:0.000}s{(string.IsNullOrWhiteSpace(liveRaceContext.CarBehind) ? "" : $" ({liveRaceContext.CarBehind})")}"
            : "unavailable";
        raceContextConfidenceLabel = liveRaceContext.Confidence.ToString();
        raceFieldDiagnosticsLabel = telemetryDiagnostics.Summary;
        UpdateOpponentTelemetryDiagnostics(ActiveSession.LatestSnapshot, lastRawTelemetryJson);
        trackMemoryComparison = trackMemoryService.Compare(trackMemoryRecord, ActiveSession, sessionAnalytics);
        raceMemoryPreviousBestLabel = trackMemoryRecord?.BestLapSeconds is { } best
            ? TrackMemoryService.FormatLapTime(best)
            : "no stored history";
        raceMemoryPreviousAverageLabel = trackMemoryRecord?.AverageCleanLapSeconds is { } average
            ? TrackMemoryService.FormatLapTime(average)
            : "no stored history";
        try
        {
            RefreshOpponentIntelligence();
        }
        catch (Exception exception)
        {
            opponentPositionLabel = "unavailable";
            opponentCarAheadLabel = "-";
            opponentCarBehindLabel = "-";
            opponentGapAheadLabel = "-";
            opponentGapBehindLabel = "-";
            opponentTrendLabel = OpponentIntelligenceAnswerBuilder.UnavailableMessage;
            opponentBattleStatusLabel = "unavailable";
            opponentConfidenceLabel = "-";
            currentOpponentIntelligence = null;
            ReportOptionalPanelFailure("Opponent intelligence", exception);
        }
    }

    private void RefreshOpponentIntelligence()
    {
        currentOpponentIntelligence = OpponentIntelligenceService.Build(new OpponentIntelligenceInput(
            liveRaceContext,
            ActiveTraceSnapshots.Count > 0 ? ActiveTraceSnapshots.TakeLast(40).ToArray() : null,
            cachedTrackGuide,
            sessionStrategy,
            trackMemoryRecord));

        if (currentOpponentIntelligence is not { HasOpponentData: true } intelligence || intelligence.Current is null)
        {
            opponentPositionLabel = liveRaceContext.Position is { } position
                ? liveRaceContext.TotalCars is { } total ? $"P{position}/{total}" : $"P{position}"
                : "unavailable";
            opponentCarAheadLabel = string.IsNullOrWhiteSpace(liveRaceContext.CarAhead) ? "-" : liveRaceContext.CarAhead;
            opponentCarBehindLabel = string.IsNullOrWhiteSpace(liveRaceContext.CarBehind) ? "-" : liveRaceContext.CarBehind;
            opponentGapAheadLabel = liveRaceContext.GapAheadSeconds is { } ahead ? $"{ahead:0.000}s" : "-";
            opponentGapBehindLabel = liveRaceContext.GapBehindSeconds is { } behind ? $"{behind:0.000}s" : "-";
            opponentTrendLabel = OpponentIntelligenceAnswerBuilder.UnavailableMessage;
            opponentBattleStatusLabel = "unavailable";
            opponentConfidenceLabel = "low";
            return;
        }

        var current = intelligence.Current;
        opponentPositionLabel = current.Position is { } pos
            ? current.TotalCars is { } totalCars ? $"P{pos}/{totalCars}" : $"P{pos}"
            : "unavailable";
        opponentCarAheadLabel = string.IsNullOrWhiteSpace(current.CarAhead) ? "-" : current.CarAhead;
        opponentCarBehindLabel = string.IsNullOrWhiteSpace(current.CarBehind) ? "-" : current.CarBehind;
        opponentGapAheadLabel = current.GapAheadSeconds is { } gapAhead ? $"{gapAhead:0.000}s" : "-";
        opponentGapBehindLabel = current.GapBehindSeconds is { } gapBehind ? $"{gapBehind:0.000}s" : "-";
        opponentTrendLabel = intelligence.GapTrend.Summary;
        opponentBattleStatusLabel = intelligence.Battle.StatusLabel;
        opponentConfidenceLabel = intelligence.Battle.Confidence.ToString();
    }

    private void RefreshDriverCoaching()
    {
        driverCoachingPanelTitle = isReviewMode ? "Driver Coaching (Review Session)" : "Driver Coaching (Live Session)";
        currentDriverCoaching = DriverCoachingIntelligenceService.Build(new DriverCoachingInput(
            sessionDriverPerformance,
            sessionAnalytics,
            sessionLapIntelligence,
            trackMemoryComparison,
            previousStoredSessionMemory,
            recentStoredSessionMemories,
            ResolveActiveTrackGuide(),
            trackMemoryRecord,
            liveRaceContext.CarClass));

        if (currentDriverCoaching is not { HasData: true })
        {
            var unavailable = currentDriverCoaching;
            driverCoachingTrendLabel = unavailable?.ProgressTrendSummary ?? SessionDriverPerformance.NeedCleanLapMessage;
            driverCoachingBiggestWeaknessLabel = unavailable?.BiggestWeakness ?? unavailable?.Availability ?? SessionDriverPerformance.NeedCleanLapMessage;
            driverCoachingStrongestAreaLabel = "-";
            driverCoachingConsistencyLabel = unavailable?.ConsistencySummary ?? "-";
            driverCoachingPreviousDeltaLabel = unavailable?.PreviousSessionDeltaSummary ?? "no previous session data";
            driverCoachingTopTargetsLabel = "-";
            return;
        }

        var coaching = currentDriverCoaching!;
        driverCoachingTrendLabel = coaching.ProgressTrendSummary;
        driverCoachingBiggestWeaknessLabel = coaching.BiggestWeakness ?? coaching.WeakestArea ?? "-";
        driverCoachingStrongestAreaLabel = coaching.StrongestArea ?? "-";
        driverCoachingConsistencyLabel = coaching.ConsistencySummary ?? "-";
        driverCoachingPreviousDeltaLabel = coaching.PreviousSessionDeltaSummary ?? "no previous session data";
        driverCoachingTopTargetsLabel = coaching.TopCoachingTargets.Count == 0
            ? "-"
            : string.Join(" | ", coaching.TopCoachingTargets);
    }

    private async Task RefreshTrackMemorySafeAsync()
    {
        try
        {
            await RefreshTrackMemoryAsync();
        }
        catch (Exception exception)
        {
            ReportOptionalPanelFailure("Session memory", exception);
        }
    }

    private async Task RefreshTrackMemoryAsync()
    {
        var track = liveRaceContext.TrackName ?? PrepTrack;
        var car = liveRaceContext.CarName ?? PrepCar;
        if (!string.IsNullOrWhiteSpace(track))
        {
            await RefreshTrackResearchAsync(track);
        }

        if (string.IsNullOrWhiteSpace(track) || string.IsNullOrWhiteSpace(car))
        {
            return;
        }

        trackMemoryRecord = await trackMemoryService.LoadAsync(storageService, track, car);
        trackMemoryComparison = trackMemoryService.Compare(trackMemoryRecord, ActiveSession, sessionAnalytics);
        await RefreshStoredSessionMemoryAsync(track, car);
        raceMemoryPreviousBestLabel = trackMemoryRecord?.BestLapSeconds is { } best
            ? TrackMemoryService.FormatLapTime(best)
            : "no stored history";
        raceMemoryPreviousAverageLabel = trackMemoryRecord?.AverageCleanLapSeconds is { } average
            ? TrackMemoryService.FormatLapTime(average)
            : "no stored history";
        Application.Current?.Dispatcher.Invoke(RaiseAnalyticsProperties);
        RefreshStrategyKnowledge();
        RefreshTrackCarKnowledge();
        Application.Current?.Dispatcher.Invoke(RaiseAnalyticsProperties);
    }

    private async Task UpsertTrackMemoryAsync(string summaryMarkdown)
    {
        var track = liveRaceContext.TrackName ?? PrepTrack;
        var car = liveRaceContext.CarName ?? PrepCar;
        if (string.IsNullOrWhiteSpace(track) || string.IsNullOrWhiteSpace(car))
        {
            return;
        }

        var input = new SessionMemoryBuildInput(
            track,
            car,
            liveRaceContext.SessionType ?? PrepSessionType,
            session,
            sessionAnalytics,
            sessionLapIntelligence,
            sessionTyreIntelligence,
            sessionStrategy,
            sessionDriverPerformance,
            currentOpponentIntelligence,
            currentDriverCoaching);
        trackMemoryRecord = (await sessionMemoryService.PersistSessionAsync(
            storageService,
            trackMemoryService,
            input,
            summaryMarkdown)).TrackMemory;
        trackMemoryComparison = trackMemoryService.Compare(trackMemoryRecord, session, sessionAnalytics);
        await RefreshStoredSessionMemoryAsync(track, car);
        await RefreshTrackResearchAsync(track);
    }

    private async Task RefreshTrackResearchAsync(string track)
    {
        try
        {
            trackResearchDetectedTrackLabel = string.IsNullOrWhiteSpace(track) ? "unavailable" : track;
            if (string.IsNullOrWhiteSpace(track))
            {
                cachedTrackGuide = null;
                UpdateTrackResearchLabels();
                await RefreshWebResearchCacheAsync(track);
                Application.Current?.Dispatcher.Invoke(RaiseAnalyticsProperties);
                return;
            }

            var cached = await trackResearchService.GetCachedGuideAsync(track);
            cachedTrackGuide = cached.Guide;
            var trackKey = TrackGuide.NormalizeTrackKey(track);
            if (trackResearchService.Options.Enabled
                && !string.Equals(lastEnsuredTrackGuideKey, trackKey, StringComparison.Ordinal))
            {
                var researchContext = new TrackResearchContext(sessionContextAssessment.Activity, isReviewMode);
                if (!researchContext.IsActiveDriving || trackResearchService.Options.AllowTrackResearchDuringDriving)
                {
                    var ensured = await trackResearchService.EnsureGuideCachedAsync(track, researchContext);
                    if (ensured.Guide is not null)
                    {
                        cachedTrackGuide = ensured.Guide;
                        lastEnsuredTrackGuideKey = trackKey;
                        await RefreshKnowledgeAsync();
                    }
                }
            }

            UpdateTrackResearchLabels();
            await RefreshWebResearchCacheAsync(track);
            Application.Current?.Dispatcher.Invoke(RaiseAnalyticsProperties);
        }
        catch (Exception exception)
        {
            cachedTrackGuide = null;
            UpdateTrackResearchLabels();
            ReportOptionalPanelFailure("Track guide", exception);
            Application.Current?.Dispatcher.Invoke(RaiseAnalyticsProperties);
        }
    }

    private void UpdateTrackResearchLabels()
    {
        trackGuideAvailableLabel = cachedTrackGuide is null ? "no" : "yes";
        trackGuideLastFetchedLabel = cachedTrackGuide?.LastUpdatedAt.ToLocalTime().ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture) ?? "-";
        trackGuideSourceCountLabel = cachedTrackGuide?.SourceCount.ToString(CultureInfo.InvariantCulture) ?? "0";
        trackGuideBrakingZonesLabel = FormatProfileItems(cachedTrackGuide?.MajorBrakingZones ?? []);
        trackGuideTractionZonesLabel = FormatProfileItems(cachedTrackGuide?.TractionZones ?? []);
        trackGuideKeyCornersLabel = cachedTrackGuide is null
            ? "-"
            : TrackGuideFormatter.FormatCornerList(cachedTrackGuide.Corners ?? []);
        trackGuideSetupNotesLabel = FormatProfileItems(cachedTrackGuide?.SetupPriorities ?? []);
    }

    private async Task FetchTrackGuideAsync()
    {
        var track = liveRaceContext.TrackName ?? PrepTrack;
        if (string.IsNullOrWhiteSpace(track))
        {
            ChatMessages.Add("Coach: Track must be detected before fetching a track guide.");
            return;
        }

        var result = await trackResearchService.FetchGuideAsync(track);
        if (result.Guide is not null)
        {
            cachedTrackGuide = result.Guide;
            lastEnsuredTrackGuideKey = TrackGuide.NormalizeTrackKey(track);
            UpdateTrackResearchLabels();
            await RefreshKnowledgeAsync();
        }

        ChatMessages.Add($"Coach: {result.Message ?? "Track guide refresh completed."}");
        Application.Current?.Dispatcher.Invoke(RaiseAnalyticsProperties);
    }

    private async Task RefreshWebResearchCacheAsync(string? track)
    {
        try
        {
            var car = liveRaceContext.CarName ?? PrepCar;
            var carClass = liveRaceContext.CarClass ?? TrackCarKnowledgeCatalog.NormalizeCarClass(PrepCar);
            webResearchDetectedTrackLabel = string.IsNullOrWhiteSpace(track) ? "unavailable" : track;
            webResearchDetectedCarClassLabel = string.IsNullOrWhiteSpace(carClass)
                ? (string.IsNullOrWhiteSpace(car) ? "unavailable" : car)
                : $"{car ?? "unknown car"} / {carClass}";

            if (string.IsNullOrWhiteSpace(track))
            {
                cachedWebResearch = WebResearchBundle.Empty(track, car, carClass);
                UpdateWebResearchLabels();
                return;
            }

            var researchContext = new WebResearchContext(sessionContextAssessment.Activity, isReviewMode);
            cachedWebResearch = await webResearchService.EnsureCachedAsync(
                track,
                car,
                carClass,
                researchContext);
            UpdateWebResearchLabels();
        }
        catch (Exception exception)
        {
            cachedWebResearch = WebResearchBundle.Empty(track);
            UpdateWebResearchLabels();
            ReportOptionalPanelFailure("Web research", exception);
        }
    }

    private void UpdateWebResearchLabels()
    {
        webResearchAvailableLabel = cachedWebResearch.HasResearch ? "Yes" : "No";
        webResearchLastFetchedLabel = cachedWebResearch.LastFetchedAt?.ToLocalTime().ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture) ?? "-";
        webResearchSourceCountLabel = cachedWebResearch.SourceCount.ToString(CultureInfo.InvariantCulture);
    }

    private void ApplyWebResearchFetchDiagnostics(string operation, WebResearchFetchResult result)
    {
        webResearchProviderLabel = result.ProviderName;
        webResearchCacheStatusLabel = result.CacheStatus;
        webResearchItemsFetchedLabel = result.ItemsReturned.ToString(CultureInfo.InvariantCulture);
        webResearchFetchStatusLabel = result.Status;
        webResearchLastFetchResultLabel = $"{operation}: {result.Message}";
        webResearchErrorMessageLabel = string.IsNullOrWhiteSpace(result.ErrorMessage) ? "-" : result.ErrorMessage;
    }

    private void LogWebResearchFetchToChat(
        string operation,
        string track,
        string? car,
        string? carClass,
        WebResearchFetchResult result,
        WebResearchBundle bundle)
    {
        ChatMessages.Add("Research fetch started...");
        ChatMessages.Add($"Provider={result.ProviderName}");
        ChatMessages.Add($"Track={track}");
        ChatMessages.Add($"Car={car ?? "unknown"}");
        ChatMessages.Add($"Class={carClass ?? "unknown"}");
        ChatMessages.Add($"Cache={result.CacheStatus}");
        ChatMessages.Add($"Items={result.ItemsReturned}");
        ChatMessages.Add($"Status={result.Status}");
        if (!string.IsNullOrWhiteSpace(result.ErrorMessage))
        {
            ChatMessages.Add($"Reason={result.ErrorMessage}");
        }

        ChatMessages.Add(result.Status.Equals("success", StringComparison.OrdinalIgnoreCase)
            ? $"Research {operation.ToLowerInvariant()} complete. Cache updated. Bundle items={bundle.Items.Count}, source count={bundle.SourceCount}."
            : $"Research {operation.ToLowerInvariant()} finished without new cache items. {result.Message}");
    }

    private async Task ApplyWebResearchFetchAsync(string operation, Func<Task<WebResearchFetchResult>> fetch)
    {
        var track = liveRaceContext.TrackName ?? PrepTrack;
        var car = liveRaceContext.CarName ?? PrepCar;
        var carClass = liveRaceContext.CarClass ?? TrackCarKnowledgeCatalog.NormalizeCarClass(PrepCar);
        if (string.IsNullOrWhiteSpace(track))
        {
            var failure = WebResearchFetchResult.Failed(
                "Track must be detected before fetching research.",
                webResearchService.Provider.Name);
            ApplyWebResearchFetchDiagnostics(operation, failure);
            ChatMessages.Add("Research fetch started...");
            ChatMessages.Add($"Status={failure.Status}");
            ChatMessages.Add($"Reason={failure.ErrorMessage}");
            Application.Current?.Dispatcher.Invoke(RaiseAnalyticsProperties);
            return;
        }

        var result = await fetch();
        cachedWebResearch = await webResearchService.LoadBundleAsync(track, car, carClass);
        webResearchDetectedTrackLabel = track;
        webResearchDetectedCarClassLabel = string.IsNullOrWhiteSpace(carClass)
            ? (string.IsNullOrWhiteSpace(car) ? "unavailable" : car)
            : $"{car ?? "unknown car"} / {carClass}";
        ApplyWebResearchFetchDiagnostics(operation, result);
        UpdateWebResearchLabels();
        LogWebResearchFetchToChat(operation, track, car, carClass, result, cachedWebResearch);
        await RefreshKnowledgeAsync();
        Application.Current?.Dispatcher.Invoke(RaiseAnalyticsProperties);
    }

    private Task FetchTrackResearchAsync() =>
        ApplyWebResearchFetchAsync(
            "Fetch Track Research",
            () => webResearchService.FetchTrackResearchAsync(
                liveRaceContext.TrackName ?? PrepTrack,
                liveRaceContext.CarName ?? PrepCar,
                liveRaceContext.CarClass ?? TrackCarKnowledgeCatalog.NormalizeCarClass(PrepCar)));

    private Task FetchTrackCarStrategyResearchAsync() =>
        ApplyWebResearchFetchAsync(
            "Fetch Track+Car Strategy",
            () => webResearchService.FetchTrackCarStrategyAsync(
                liveRaceContext.TrackName ?? PrepTrack,
                liveRaceContext.CarName ?? PrepCar,
                liveRaceContext.CarClass ?? TrackCarKnowledgeCatalog.NormalizeCarClass(PrepCar)));

    private Task RefreshWebResearchAsync() =>
        ApplyWebResearchFetchAsync(
            "Refresh Research",
            () => webResearchService.RefreshResearchAsync(
                liveRaceContext.TrackName ?? PrepTrack,
                liveRaceContext.CarName ?? PrepCar,
                liveRaceContext.CarClass ?? TrackCarKnowledgeCatalog.NormalizeCarClass(PrepCar)));

    private void UpdateSessionContext()
    {
        sessionContextAssessment = sessionContextClassifier.Classify(new SessionContextInput(
            ActiveSession,
            ActiveTraceSnapshots.Count > 0 ? ActiveTraceSnapshots.TakeLast(12).ToArray() : null,
            CurrentPrepPlan(),
            isReviewMode));
        sessionModeLabel = sessionContextAssessment.SessionModeLabel;
    }

    private void RaiseVoiceProperties()
    {
        OnPropertyChanged(nameof(VoiceLabel));
        OnPropertyChanged(nameof(TtsLabel));
        OnPropertyChanged(nameof(VoiceInputMuteLabel));
        OnPropertyChanged(nameof(VoiceState));
        OnPropertyChanged(nameof(VoiceInputState));
        OnPropertyChanged(nameof(PushToTalkIndicator));
        OnPropertyChanged(nameof(ListeningIndicator));
        OnPropertyChanged(nameof(RecognizedSpeechPreview));
        OnPropertyChanged(nameof(PushToTalkHotkeyLabel));
        OnPropertyChanged(nameof(VoiceSuppressionState));
        OnPropertyChanged(nameof(MicDiagnosticsSummary));
        OnPropertyChanged(nameof(MicCurrentRms));
        OnPropertyChanged(nameof(MicPeakRms));
        OnPropertyChanged(nameof(MicRawPeakRms));
        OnPropertyChanged(nameof(MicConvertedPeakRms));
        OnPropertyChanged(nameof(MicWhisperInputRms));
        OnPropertyChanged(nameof(MicSpeechDetected));
        OnPropertyChanged(nameof(MicClipping));
        OnPropertyChanged(nameof(MicDeviceName));
        OnPropertyChanged(nameof(MicSignalQualityLabel));
        OnPropertyChanged(nameof(MicCalibrationStatus));
        OnPropertyChanged(nameof(HasPendingTranscript));
        (ConfirmTranscriptCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (RejectTranscriptCommand as RelayCommand)?.RaiseCanExecuteChanged();
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
        var guide = ResolveActiveTrackGuide();
        var mappedDriverCoaching = currentDriverCoaching is null
            ? null
            : TrackGuideZoneMapper.MapDriverCoachingRecommendation(
                currentDriverCoaching,
                guide,
                "DriverCoaching.Context");
        var mappedDriverPerformance = sessionDriverPerformance is null
            ? null
            : TrackGuideZoneMapper.MapPerformance(sessionDriverPerformance, guide);

        return new CoachContext(
            reviewSessionNotes.Count > 0 ? reviewSessionNotes : null,
            LoadedSessionSummary,
            CurrentPrepPlan(),
            KnowledgeSources.Select(item => item.Source).ToArray(),
            HasExternalResearchSources(),
            userPreferences.Coach,
            sessionContextAssessment,
            sessionTyreIntelligence,
            sessionAnalytics,
            mappedDriverPerformance,
            sessionStrategy,
            ActiveTraceSnapshots.Count > 0 ? ActiveTraceSnapshots.TakeLast(40).ToArray() : null,
            liveRaceContext,
            trackMemoryRecord,
            trackMemoryComparison,
            previousStoredSessionMemory,
            recentStoredSessionMemories,
            cachedTrackGuide,
            currentStrategyKnowledge,
            currentTrackCarKnowledge,
            cachedWebResearch,
            currentOpponentIntelligence,
            mappedDriverCoaching,
            isReviewMode ? reviewSessionTrack : null);
    }

    private bool HasExternalResearchSources() =>
        cachedTrackGuide is not null
        || cachedWebResearch.HasResearch
        || KnowledgeSources.Any(item => item.Source.SourceType == KnowledgeSourceTypes.Web);

    private SessionMemoryBuildInput? BuildSessionMemoryInput()
    {
        var track = liveRaceContext.TrackName ?? PrepTrack;
        var car = liveRaceContext.CarName ?? PrepCar;
        if (string.IsNullOrWhiteSpace(track) || string.IsNullOrWhiteSpace(car))
        {
            return null;
        }

        return new SessionMemoryBuildInput(
            track,
            car,
            string.IsNullOrWhiteSpace(liveRaceContext.SessionType) ? PrepSessionType : liveRaceContext.SessionType,
            session,
            sessionAnalytics,
            sessionLapIntelligence,
            sessionTyreIntelligence,
            sessionStrategy,
            sessionDriverPerformance,
            currentOpponentIntelligence,
            currentDriverCoaching,
            cachedTrackGuide);
    }

    private async Task SaveMemorySummaryAsync()
    {
        var input = BuildSessionMemoryInput();
        if (input is null)
        {
            ChatMessages.Add("Coach: Track and car must be known before saving session memory.");
            return;
        }

        var (trackMemory, summary) = await sessionMemoryService.PersistSessionAsync(
            storageService,
            trackMemoryService,
            input);
        trackMemoryRecord = trackMemory;
        trackMemoryComparison = trackMemoryService.Compare(trackMemoryRecord, ActiveSession, sessionAnalytics);
        await RefreshStoredSessionMemoryAsync(input.TrackName, input.CarName);
        ChatMessages.Add($"Coach: Stored session memory saved for {summary.TrackName} / {summary.CarName}.");
    }

    private void GenerateSessionDebrief()
    {
        var input = BuildSessionMemoryInput();
        if (input is null)
        {
            sessionDebriefPreview = "Track and car must be known before generating a debrief.";
            OnPropertyChanged(nameof(SessionDebriefPreview));
            ChatMessages.Add("Coach: Track and car must be known before generating a debrief.");
            return;
        }

        var summary = sessionMemoryService.BuildSummary(input);
        currentSessionDebrief = sessionMemoryService.GenerateDebrief(summary, input);
        sessionDebriefPreview = currentSessionDebrief.Markdown;
        OnPropertyChanged(nameof(SessionDebriefPreview));
        ChatMessages.Add("Coach: Session debrief generated from current session data.");
        ChatMessages.Add(currentSessionDebrief.Markdown);
    }

    private async Task RefreshStoredSessionMemoryAsync(string track, string car)
    {
        try
        {
            recentStoredSessionMemories = await sessionMemoryService.LoadRecentAsync(
                storageService,
                track,
                car,
                5,
                session.SessionId);
            previousStoredSessionMemory = recentStoredSessionMemories.FirstOrDefault();
            sessionMemoryLastSummaryLabel = previousStoredSessionMemory?.OneLineSummary ?? "No stored session summary yet.";
            sessionMemoryKnownWeaknessesLabel = previousStoredSessionMemory is null
                ? "-"
                : FormatProfileItems(BuildSessionMemoryWeaknessLines(previousStoredSessionMemory, ResolveActiveTrackGuide(previousStoredSessionMemory.TrackName)));
            Application.Current?.Dispatcher.Invoke(RaiseAnalyticsProperties);
        }
        catch (Exception exception)
        {
            recentStoredSessionMemories = [];
            previousStoredSessionMemory = null;
            sessionMemoryLastSummaryLabel = "Stored session memory unavailable.";
            sessionMemoryKnownWeaknessesLabel = "-";
            ReportOptionalPanelFailure("Session memory", exception);
            Application.Current?.Dispatcher.Invoke(RaiseAnalyticsProperties);
        }
    }

    private static IReadOnlyList<string> BuildSessionMemoryWeaknessLines(
        SessionMemorySummary summary,
        TrackGuide? guide) =>
        (summary.MainTimeLossZones ?? [])
            .Concat(summary.BrakingWeaknesses ?? [])
            .Concat(summary.ThrottleWeaknesses ?? [])
            .Concat(summary.ImprovementTargets ?? [])
            .Concat(summary.RepeatedWeaknesses ?? [])
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(4)
            .Select(item => TrackGuideZoneMapper.MapZoneReferences(item, guide))
            .ToArray();

    private TrackGuide? ResolveActiveTrackGuide(string? trackName = null)
    {
        var resolvedTrack = TrackGuideZoneMapper.CanonicalizeTrackName(
            trackName
                ?? reviewSessionTrack
                ?? liveRaceContext.TrackName
                ?? PrepTrack
                ?? previousStoredSessionMemory?.TrackName
                ?? TrackGuideZoneMapper.ResolveTrackNameFromSnapshots(ActiveTraceSnapshots)
                ?? ActiveSession.LatestSnapshot?.RaceAwareness?.TrackName
                ?? ActiveSession.LatestSnapshot?.RaceAwareness?.CircuitId);

        return TrackGuideZoneMapper.ResolveGuide(
            cachedTrackGuide,
            KnowledgeSources.Select(item => item.Source),
            resolvedTrack);
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
        OnPropertyChanged(nameof(TelemetryOpponentFieldsLabel));
        OnPropertyChanged(nameof(TelemetryOpponentResolvedLabel));
        OnPropertyChanged(nameof(TelemetryOpponentCandidatesLabel));
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
        OnPropertyChanged(nameof(TelemetryOpponentFieldsLabel));
        OnPropertyChanged(nameof(TelemetryOpponentResolvedLabel));
        OnPropertyChanged(nameof(TelemetryOpponentCandidatesLabel));
    }

    private void UpdateOpponentTelemetryDiagnostics(TelemetrySnapshot? snapshot, string? rawJson)
    {
        var report = RaceContextService.BuildOpponentTelemetryDiagnostics(snapshot, rawJson);
        var presentLabel = report.PresentFields.Count == 0
            ? "(none)"
            : string.Join(", ", report.PresentFields);
        var missingLabel = report.MissingFields.Count == 0
            ? "(none)"
            : string.Join(", ", report.MissingFields);
        opponentTelemetryPresentLabel = $"present: {presentLabel}; missing: {missingLabel}";
        opponentTelemetryResolvedLabel = report.ResolvedPropertyNames.Count == 0
            ? "resolved: (none)"
            : "resolved: " + string.Join("; ", report.ResolvedPropertyNames.Select(pair => $"{pair.Key}<-{pair.Value}"));
        opponentTelemetryCandidatesLabel = report.CandidateRawFields.Count == 0
            ? "candidates: (none)"
            : "candidates: " + string.Join("; ", report.CandidateRawFields.Take(8).Select(candidate => $"{candidate.JsonPath}={candidate.SampleValue}"));
        telemetryOpponentFieldsLabel = opponentTelemetryPresentLabel;
        telemetryOpponentResolvedLabel = opponentTelemetryResolvedLabel;
        telemetryOpponentCandidatesLabel = opponentTelemetryCandidatesLabel;
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
