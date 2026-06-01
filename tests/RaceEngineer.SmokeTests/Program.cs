using System.Globalization;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Text;
using RaceEngineer.Core;
using RaceEngineer.Core.Analytics;
using RaceEngineer.Core.Coaching;
using RaceEngineer.Core.Coaching.Ai;
using RaceEngineer.Core.Events;
using RaceEngineer.Core.Knowledge;
using RaceEngineer.Core.Profile;
using RaceEngineer.Core.Session;
using RaceEngineer.Core.SessionContext;
using RaceEngineer.Core.RaceAwareness;
using RaceEngineer.Core.Storage;
using RaceEngineer.Core.Strategy;
using RaceEngineer.Core.Telemetry;
using RaceEngineer.Core.Telemetry.Iracing;
using RaceEngineer.Core.TelemetryVisualization;
using RaceEngineer.Core.Voice;
using RaceEngineer.SmokeTests;

if (args is ["--write-fixture", ..])
{
    var directory = args.Length > 1
        ? args[1]
        : Path.Combine(AppContext.BaseDirectory, "fixtures");
    Console.WriteLine(SampleThreeLapFixture.WriteDefault(directory));
    return;
}

if (args is ["--report-opponent-fields", ..])
{
    var capturePath = args.Length > 1
        ? args[1]
        : SampleThreeLapFixture.ResolvePath();
    OpponentTelemetryCaptureReport.PrintReport(capturePath);
    return;
}

ValidPacketParsesCorrectly();
WrongSchemaIsRejected();
WrongSchemaVersionIsRejected();
MissingTyreFieldBecomesNull();
InvalidJsonIsRejected();
EventEngineEmitsGroundedEvents();
EventEngineDoesNotCrashWithPartialSnapshots();
RepeatedSameEventInsideCooldownEmitsOnlyOnce();
SameEventAfterCooldownEmitsAgain();
MissingOptionalDataPreventsLowConfidenceHeuristic();
SeverityIsNormalized();
EvidenceIncludesThresholdAndActualValue();
LapProgressWrapCreatesLapEvents();
SmallBackwardNoiseDoesNotCreateLap();
BestLapUpdatesCorrectly();
FuelPerLapEstimateRequiresEnoughData();
EventsIncludeLapNumberAfterTrackingIsActive();
CoachStatesUncertaintyWithoutTelemetry();
CoachTyreStatusWithDataReturnsFacts();
CoachTyreStatusWithoutDataSaysUnavailable();
CoachFuelStatusUsesLatestFuelAndEstimate();
CoachRecentMistakesSummarizesEvents();
CoachNextLapFocusChoosesHighestPriorityIssue();
CoachDoesNotInventFakeValuesWhenDataIsNull();
CriticalEventProducesVoiceCallout();
LowPriorityRepeatedVoiceEventIsSuppressed();
MutedVoiceProducesNoSpokenOutput();
SpokenQueryRoutesThroughCoachEngine();
VoiceInputServiceRoutesRecognizedSpeech();
VoiceInputServiceEnforcesQueryCooldown();
VoiceInputConfirmationPrefixesSpokenResponse();
SpokenSummaryConfirmationPreservesAnswerAfterCopyPrefix();
SpokenSummaryGeneratorExtractsActionableSentence();
VoiceInputStartupHandlesLazyProviderFailures();
LazySpeechRecognitionProviderInitializesOnPushToTalk();
SpeechRecognitionCultureResolverSupportsConfiguredAndAutoFallback();
SpeechRecognitionProviderSelectionSupportsConfiguredValues();
WhisperModelLocatorResolvesDefaultAndConfiguredPaths();
WhisperSpeechOptionsNormalizeSettingsValues();
TranscriptGateRejectsWeakSignalAndHallucinations();
MicrophonePcmConverterHandlesFloatStereo48k();
MicrophonePcmConverterHandlesPcm16Stereo44100();
MicrophonePcmConverterInfersUnknown32BitAsFloat();
VoiceInputServiceRejectsGarbageTranscript();
StrategyEngineComputesDeterministicFuelMetrics();
StrategyCalloutManagerSpeaksOnlyOnStateChange();
SessionContextSuppressesStationaryFuelPanic();
StrategyGateSuppressesPracticeStrategyCallouts();
CoachFuelAnswerLeadsWithActualFuelLevel();
VoiceInteractionGateSuppressesAutomaticCalloutsAfterQuestion();
CoachEngineRoutesCroatianTyrePhrase();
CoachEngineRoutesStrategyQuestions();
await ProfilePreferencesPersistAndMergeAppSettings();
CoachResponseFormatterRespectsResponseLength();
CoachEngineRoutesCroatianBrakingPhrase();
CoachStationaryPitBrakingReturnsNoDataYet();
CoachBrakingAnswersAfterValidLap();
SpokenBrakingSummaryWhenNoDataYet();
HybridCoachStationaryPitBrakingDoesNotUseInvalidLapFlags();
PttPipelineStationaryPitBrakingUsesUnifiedGateMessage();
VoiceInputAcceptsRecognitionAfterPttReleaseGrace();
VoiceInputReportsNoSpeechAfterGraceTimeout();
VoiceCalloutContainsNoFakeTelemetryValues();
DefaultSettingsLoad();
InvalidSettingsFallBackSafely();
StoragePathIsCreated();
await ReceiverReportsPortBindFailureClearly();
await PrepSavesAndLoadsByCarTrack();
CoachAnswersFuelPlanFromPrep();
ReportIncludesRecurringEventCounts();
ReportSaysUnavailableForMissingTyreBrakeData();
ReportDoesNotInventLapTimesWithoutCompletedLaps();
await KnowledgeSourcesSaveSearchAndDelete();
CoachAnswersTrackKnowledgeWithSourceLabels();
CoachSeparatesTelemetryFromTrackKnowledge();
CoachSaysExternalResearchUnavailableOffline();
ReplayMatchesLiveParserPath();
DiagnosticsCountValidInvalidRates();
RawCaptureStoresParseResult();
ReplayDoesNotRequireUdpSocket();
await StoragePersistsSessionFactsAndExports();
AnalyticsComputesDeterministicMetrics();
LapIntelligenceComputesDeterministicInsights();
CoachEvidenceBuilderCreatesDeterministicPackets();
HybridCoachFallsBackWhenAiDisabled();
HybridCoachMockAnswersFromEvidence();
HybridCoachFallsBackWhenNoEvidence();
HybridCoachMockAnswersCroatianQuestions();
HybridCoachFallsBackOnAiTimeout();
OpenAiCompatibleProviderGeneratesAnswerWithMockHttp();
AiEngineerStatusResolverReportsConfiguredStates();
EngineerAiProviderFactorySelectsOpenAiProvider();
HybridCoachMockAnswersDirectDriverQuestions();
HybridCoachLiveCalloutsStayDeterministic();
StrictTopicRoutingTyreQuestionDoesNotReturnFuel();
StrictTopicRoutingPositionQuestionDoesNotReturnFuel();
StrictTopicRoutingLapTimeQuestionDoesNotReturnFuel();
FuelQuestionStillReturnsFuel();
CroatianInputProducesEnglishSpokenSummary();
TyreIntelligenceCoachesColdTyreWarmup();
TraceProgressSurvivesLapWrapBleed();
FuelAmountQuestionLeadsWithLiters();
FuelConsumptionQuestionReportsPerLap();
FuelStrategyQuestionReportsRiskAndLapsRemaining();
PushConfidenceDistinctFromTyreQuestion();
FuelStrategyDoesNotMentionBraking();
ThrottleUsesLiveTraceWithoutCompletedLap();
TyreAnswerIncludesAllCornersOrUnavailableRear();
RaceAwarenessPacketParsesPartialFields();
RaceContextServiceReportsMissingOpponentGaps();
OpponentTelemetryDiscoveryMapsCamelCaseFields();
OpponentTelemetryDiscoveryFindsUnmappedCandidates();
ProviderDiagOnlyPacketReportsOpponentGapsUnavailable();
ProviderDiagOnlyGapTrendSaysUnavailable();
OpponentTelemetryCaptureReportReadsJsonl();
await TrackMemoryRetrievalAndHistoricalComparison();
SessionMemorySummaryBuildsFromFixtureLaps();
await SessionMemoryRetrievalByTrackCar();
SessionDebriefContainsStrengthsWeaknessesActions();
CoachEvidenceIncludesStoredSessionMemoryWhenTrackCarMatch();
CoachingQueriesRouteDespiteLowConfidence();
StoredSessionMemoryUsesNaturalLanguage();
CoachingTrackGuidePhrasesPassTranscriptGate();
await CachedTrackGuideLookupByTrack();
await TrackGuideCacheRefresh();
await TrackResearchDisabledProviderDoesNotFetchWeb();
CoachUsesCachedTrackGuideWhenAvailable();
TrackGuideNoGuideFallback();
EngineerAiContextIncludesTrackGuideSummary();
OpponentGapTrendGainingDetection();
OpponentGapTrendLosingDetection();
OpponentGapTrendStableDetection();
OpponentAttackOpportunityDetection();
OpponentDefendRecommendation();
OpponentUnavailableTelemetryFallback();
EngineerAiContextIncludesBattleSummary();
StrategyKnowledgeTenLapFuelFromLiveBurn();
StrategyKnowledgeStoredCarFallback();
StrategyKnowledgeClassBaselineFallback();
StrategyKnowledgeMissingDataUnavailable();
TrackCarKnowledgeFuelForTenLapsUsesKnowledgeBaseline();
TrackCarKnowledgeFuelForTenLapsUsesLiveTelemetry();
TrackCarKnowledgeBrakeDemandUsesStoredKnowledge();
TrackCarKnowledgeAiContextIncludesFacts();
await WebResearchCacheLookupRoundTrip();
await WebResearchCacheMatchesTrackAliasAndCarFilter();
await WebResearchStaleRefreshBehavior();
WebResearchDisabledProviderDoesNotFetch();
WebResearchCoachAnswerCombinesCachedResearchAndTelemetry();
WebResearchAiContextIncludesCachedResearchFacts();
CoachRaceAwarenessMissingGapSaysUnavailable();
CoachTrackIdentityRoutingDoesNotFallbackToPosition();
RaceAwarenessRoutingDiagnosticsCoverTrackAndPosition();
RaceAwarenessValidatorBlocksCrossTopicFallback();
ProviderDiagMapsTrackAndCarIntoRaceAwareness();
TrackCarIdentityRoutingKeepsFieldsSeparate();
TopicRoutingGuardsIdentityBleed();
CoachHistoricalLapComparisonUsesStoredData();
DriverPerformanceIntelligenceComputesDeterministicInsights();
CoachLosingTimeUsesPerformanceIntelligence();
CoachPerformanceNeedsCleanLapWhenInsufficientData();
DriverCoachingDetectsCurrentSessionImprovement();
DriverCoachingComparesPreviousSession();
DriverCoachingExtractsRepeatedWeaknesses();
DriverCoachingBuildsTopThreeTargets();
DriverCoachingNoDataFallback();
TrackGuideZoneMapperMapsMonzaZones();
TrackGuideZoneMapperMapsMonzaZoneSix();
ZoneMapperPrefersCatalogOverCachedZonePlaceholders();
StoredSessionMemoryWatchAnswerMapsRawZones();
CoachQueryPipelineMapsStoredZonesWithoutCachedGuide();
CoachResponsePrioritizerLimitsDefaultAnswer();
CoachResponsePrioritizerDetailedModePreservesFullAnswer();
CoachQueryPipelinePrioritizesCommonCoachQueries();
RaceStrategyIntelligenceEnoughFuel();
RaceStrategyIntelligenceNotEnoughFuel();
RaceStrategyTyreDegradationWarning();
RaceStrategyPitRecommendationAvailable();
RaceStrategyInsufficientDataFallback();
EngineerAiContextContainsStrategyFacts();
ReviewModeTrackResolutionUsesReviewSessionTrack();
TrackGuideCatalogMapsRedBullRingAliases();
TrackGuideDoesNotUseMonzaForUnknownTrack();
TrackGuideRejectsMismatchedCachedGuide();
SessionDebriefMapsStoredRawZones();
LapDeltaSanityRejectsImpossibleTraceDeltas();
DriverCoachingAnswerRemapsStaleContextCoaching();
ZoneMapperPrefersCatalogOverCachedZonePlaceholders();
DriverCoachingUsesTrackGuideCornerNames();
EngineerAiContextIncludesDriverCoachingSummary();
TelemetryTraceBuilderCreatesDeterministicTimeline();
SimHubTelemetryProviderStillParsesExistingFixture();
SimHubProviderCapabilitiesReportMissingOpponentGaps();
CoachGapQuestionExplainsProviderLimitation();
TelemetryProviderFactorySelectsSimHub();
TelemetryProviderFactoryUnknownProviderFallsBackSafely();
TelemetryProviderFactorySelectsIracing();
IracingUnavailableProviderReportsClearDiagnostic();
IracingCapabilitiesIncludeOpponentGapsAndStandings();
IracingMapperMapsFixtureToTelemetrySnapshot();
CoachIracingUnavailableExplainsProviderLimitation();
EndToEndFixtureReplayVerifiesPipeline();
await ReceiverAcceptsOnlyValidPacketsOnDefaultEndpoint();

Console.WriteLine("C# smoke tests passed.");

static void ValidPacketParsesCorrectly()
{
    var valid = SimHubPacketParser.TryParse(
        """{"schema":"acevo_engineer.simhub_datacore","schema_version":1,"speed_kmh":181.5,"gear":4,"throttle":0.72,"lap_progress":0.42,"tyre_temp_c":[90,91,null,89]}""",
        out var snapshot,
        out var warning);

    Assert(valid, $"Valid packet was rejected: {warning}");
    Assert(snapshot is not null, "Valid packet should produce a snapshot.");
    Assert(snapshot!.Car.SpeedKmh == 181.5, "Speed did not parse correctly.");
    Assert(snapshot.Car.Gear == 4, "Gear did not parse correctly.");
    Assert(snapshot.Inputs.Throttle == 0.72, "Throttle did not parse correctly.");
    Assert(snapshot.Condition.TyreTempC?.Count == 4, "Tyre temperature array did not parse correctly.");
}

static void WrongSchemaIsRejected()
{
    var valid = SimHubPacketParser.TryParse(
        """{"schema":"other.schema","schema_version":1,"speed_kmh":181.5}""",
        out var snapshot,
        out var warning);

    Assert(!valid, "Wrong schema should be rejected.");
    Assert(snapshot is null, "Wrong schema should not produce a snapshot.");
    Assert(warning?.Contains("Unsupported schema", StringComparison.Ordinal) == true, "Wrong schema should produce a parser warning.");
}

static void WrongSchemaVersionIsRejected()
{
    var valid = SimHubPacketParser.TryParse(
        """{"schema":"acevo_engineer.simhub_datacore","schema_version":2,"speed_kmh":181.5}""",
        out var snapshot,
        out var warning);

    Assert(!valid, "Wrong schema_version should be rejected.");
    Assert(snapshot is null, "Wrong schema_version should not produce a snapshot.");
    Assert(warning?.Contains("Unsupported schema_version", StringComparison.Ordinal) == true, "Wrong schema_version should produce a parser warning.");
}

static void MissingTyreFieldBecomesNull()
{
    var valid = SimHubPacketParser.TryParse(
        """{"schema":"acevo_engineer.simhub_datacore","schema_version":1,"speed_kmh":"","gear":"bad"}""",
        out var snapshot,
        out var warning);

    Assert(valid, $"Packet with missing optional fields should be valid: {warning}");
    Assert(snapshot is not null, "Valid packet should produce a snapshot.");
    Assert(snapshot!.Car.SpeedKmh is null, "Missing/blank speed should be null.");
    Assert(snapshot.Car.Gear is null, "Invalid gear should be null.");
    Assert(snapshot.Inputs.Throttle is null, "Missing throttle should be null.");
    Assert(snapshot.Condition.TyreTempC is null, "Missing tyre temperatures should be null.");
}

static void InvalidJsonIsRejected()
{
    var valid = SimHubPacketParser.TryParse("""{"schema":"acevo_engineer.simhub_datacore","schema_version":1""", out var snapshot, out var warning);

    Assert(!valid, "Invalid JSON should be rejected.");
    Assert(snapshot is null, "Invalid JSON should not produce a snapshot.");
    Assert(warning?.Contains("Invalid JSON", StringComparison.Ordinal) == true, "Invalid JSON should produce a parser warning.");
}

static void EventEngineEmitsGroundedEvents()
{
    var engine = new EventEngine();
    engine.Process(SimHubPacketParser.Parse("""{"schema":"acevo_engineer.simhub_datacore","schema_version":1,"brake":0.9,"speed_kmh":140,"lap_progress":0.2}"""));
    var events = engine.Process(SimHubPacketParser.Parse("""{"schema":"acevo_engineer.simhub_datacore","schema_version":1,"brake":0.2,"speed_kmh":138,"lap_progress":0.21,"fuel":2.4,"tyre_temp_c":[101,106,99,98],"brake_temp_c":[700,860,720,730]}"""));

    Assert(events.Any(item => item.Type == EventType.AbruptBrakeRelease), "Abrupt brake release event was not emitted.");
    Assert(events.Any(item => item.Type == EventType.LowFuel), "Low fuel event was not emitted.");
    Assert(events.Any(item => item.Type == EventType.TyreOverheating), "Tyre overheating event was not emitted.");
    Assert(events.Any(item => item.Type == EventType.BrakeOverheating), "Brake overheating event was not emitted.");
    Assert(events.All(item => item.Evidence.Count > 0), "Events should carry evidence.");
}

static void EventEngineDoesNotCrashWithPartialSnapshots()
{
    var engine = new EventEngine();
    var snapshot = SimHubPacketParser.Parse("""{"schema":"acevo_engineer.simhub_datacore","schema_version":1}""");
    var events = engine.Process(snapshot);

    Assert(events.Count == 0, "Partial snapshot should not invent events.");
}

static void RepeatedSameEventInsideCooldownEmitsOnlyOnce()
{
    var engine = new EventEngine();
    var start = DateTimeOffset.UtcNow;

    var first = engine.Process(PacketAt("""{"lap_progress":0.20,"brake":0.9,"speed_kmh":140}""", start));
    var second = engine.Process(PacketAt("""{"lap_progress":0.21,"brake":0.91,"speed_kmh":141}""", start.AddSeconds(1)));

    Assert(first.Count(item => item.Type == EventType.HeavyBraking) == 1, "First heavy braking event should emit.");
    Assert(second.All(item => item.Type != EventType.HeavyBraking), "Heavy braking inside cooldown should be suppressed.");
    Assert(engine.GetSuppressedCount(EventType.HeavyBraking) == 1, "Suppressed count should update inside cooldown.");
}

static void SameEventAfterCooldownEmitsAgain()
{
    var engine = new EventEngine();
    var start = DateTimeOffset.UtcNow;

    engine.Process(PacketAt("""{"lap_progress":0.20,"brake":0.9,"speed_kmh":140}""", start));
    engine.Process(PacketAt("""{"lap_progress":0.21,"brake":0.91,"speed_kmh":141}""", start.AddSeconds(1)));
    var afterCooldown = engine.Process(PacketAt("""{"lap_progress":0.22,"brake":0.92,"speed_kmh":142}""", start.AddSeconds(3)));

    var heavyBraking = afterCooldown.Single(item => item.Type == EventType.HeavyBraking);
    Assert((int)heavyBraking.Evidence["suppressed_count"]! == 1, "Emitted event should include suppressed_count from the previous cooldown window.");
}

static void MissingOptionalDataPreventsLowConfidenceHeuristic()
{
    var engine = new EventEngine();
    engine.Process(Packet("""{"lap_progress":0.20,"throttle":0.7,"steering":0.35}"""));
    var events = engine.Process(Packet("""{"lap_progress":0.21,"throttle":0.8,"steering":0.40}"""));

    Assert(events.All(item => item.Type != EventType.TractionLoss), "Traction loss heuristic should not emit without previous/current speed data.");
}

static void SeverityIsNormalized()
{
    var values = Enum.GetNames<EventSeverity>();

    Assert(values.SequenceEqual(["Info", "Warning", "Critical"]), "Severity model should be exactly info/warning/critical.");
}

static void EvidenceIncludesThresholdAndActualValue()
{
    var engine = new EventEngine();
    var events = engine.Process(Packet("""{"lap_progress":0.20,"brake":0.9,"speed_kmh":140}"""));
    var heavyBraking = events.Single(item => item.Type == EventType.HeavyBraking);

    Assert(heavyBraking.Evidence.ContainsKey("brake"), "Evidence should include actual brake value.");
    Assert(heavyBraking.Evidence.ContainsKey("brake_threshold"), "Evidence should include brake threshold.");
    Assert(heavyBraking.Evidence.ContainsKey("speed_kmh"), "Evidence should include actual speed value.");
    Assert(heavyBraking.Evidence.ContainsKey("speed_threshold_kmh"), "Evidence should include speed threshold.");
    Assert(heavyBraking.Evidence.ContainsKey("confidence_reason"), "Evidence should include confidence reason.");
}

static void LapProgressWrapCreatesLapEvents()
{
    var engine = new EventEngine();
    engine.Process(Packet("""{"lap_progress":0.99,"fuel":10.0}"""));
    var events = engine.Process(Packet("""{"lap_progress":0.01,"lap_time_s":91.2,"fuel":8.1}"""));

    Assert(events.Any(item => item.Type == EventType.LapEnd), "Lap wrap should create lap_end.");
    Assert(events.Any(item => item.Type == EventType.LapStart), "Lap wrap should create lap_start.");
    Assert(events.Single(item => item.Type == EventType.LapEnd).LapNumber == 1, "lap_end should describe completed lap 1.");
    Assert(events.Single(item => item.Type == EventType.LapStart).LapNumber == 2, "lap_start should advance to lap 2.");
}

static void SmallBackwardNoiseDoesNotCreateLap()
{
    var engine = new EventEngine();
    engine.Process(Packet("""{"lap_progress":0.50}"""));
    var events = engine.Process(Packet("""{"lap_progress":0.49}"""));

    Assert(!events.Any(item => item.Type is EventType.LapEnd or EventType.LapStart), "Small backward progress noise should not create lap events.");
}

static void BestLapUpdatesCorrectly()
{
    var engine = new EventEngine();
    var session = new SessionState();

    Apply(session, engine, Packet("""{"lap_progress":0.99,"fuel":20.0}"""));
    Apply(session, engine, Packet("""{"lap_progress":0.01,"lap_time_s":90.0,"fuel":18.0}"""));
    Apply(session, engine, Packet("""{"lap_progress":0.99,"fuel":18.0}"""));
    Apply(session, engine, Packet("""{"lap_progress":0.01,"lap_time_s":88.0,"fuel":16.0}"""));

    Assert(session.CompletedLaps.Count == 2, "Two completed laps should be recorded.");
    Assert(session.LastLap?.Duration == TimeSpan.FromSeconds(88), "Last lap should be the most recent completed lap.");
    Assert(session.BestLap?.Duration == TimeSpan.FromSeconds(88), "Best lap should update to the lower lap time.");
}

static void FuelPerLapEstimateRequiresEnoughData()
{
    var engine = new EventEngine();
    var session = new SessionState();

    Apply(session, engine, Packet("""{"lap_progress":0.99,"fuel":10.0}"""));
    Apply(session, engine, Packet("""{"lap_progress":0.01,"lap_time_s":90.0,"fuel":8.0}"""));
    Assert(session.FuelUsedPerLap is null, "Fuel-per-lap estimate should wait for at least two completed fuel samples.");
    Assert(session.EstimatedLapsRemaining is null, "Laps remaining should wait for reliable fuel-per-lap data.");

    Apply(session, engine, Packet("""{"lap_progress":0.99,"fuel":8.0}"""));
    Apply(session, engine, Packet("""{"lap_progress":0.01,"lap_time_s":89.0,"fuel":5.0}"""));

    Assert(Math.Abs(session.FuelUsedPerLap!.Value - 2.5) < 0.001, "Fuel-per-lap should average completed valid lap samples.");
    Assert(Math.Abs(session.EstimatedLapsRemaining!.Value - 2.0) < 0.001, "Estimated laps remaining should use latest fuel and fuel-per-lap.");
}

static void EventsIncludeLapNumberAfterTrackingIsActive()
{
    var engine = new EventEngine();
    engine.Process(Packet("""{"lap_progress":0.10}"""));
    var events = engine.Process(Packet("""{"lap_progress":0.20,"brake":0.9,"speed_kmh":140}"""));

    var heavyBraking = events.Single(item => item.Type == EventType.HeavyBraking);
    Assert(heavyBraking.LapNumber == 1, "Events should include current lap number after tracking is active.");
}

static void CoachStatesUncertaintyWithoutTelemetry()
{
    var coach = new CoachEngine();
    var answer = coach.Answer(new SessionState(), "How is fuel?");

    Assert(answer.Uncertainty is not null, "Coach should state uncertainty without telemetry.");
    Assert(answer.GroundedEventIds.Count == 0, "Ungrounded answer should not claim event grounding.");
}

static void CoachTyreStatusWithDataReturnsFacts()
{
    var coach = new CoachEngine();
    var session = new SessionState();
    session.ApplySnapshot(Packet("""{"lap_progress":0.2,"tyre_temp_c":[88,91,90,89],"tyre_pressure":[26.1,26.4,26.0,26.2],"tyre_wear":[0.01,0.02,0.01,0.01]}"""), []);

    var answer = coach.Answer(session, "tyre status");

    Assert(
        answer.Content.Contains("Tyres", StringComparison.OrdinalIgnoreCase)
            || answer.Content.Contains("tyre", StringComparison.OrdinalIgnoreCase),
        "Tyre answer should include natural coaching language.");
    Assert(
        !answer.Content.Contains("max tyre temp:", StringComparison.Ordinal),
        "Tyre answer should not be raw temperature only.");
    Assert(answer.EvidencePackets.Count > 0, "Tyre answer should include structured evidence packets.");
}

static void CoachTyreStatusWithoutDataSaysUnavailable()
{
    var coach = new CoachEngine();
    var session = new SessionState();
    session.ApplySnapshot(Packet("""{"lap_progress":0.2,"speed_kmh":100}"""), []);

    var answer = coach.Answer(session, "tyre status");

    Assert(
        answer.Content.Contains("not reliable", StringComparison.OrdinalIgnoreCase)
            || answer.Content.Contains("unavailable", StringComparison.OrdinalIgnoreCase),
        "Missing tyre data should be explicit.");
    Assert(answer.Uncertainty is not null, "Missing tyre data should set uncertainty.");
}

static void CoachFuelStatusUsesLatestFuelAndEstimate()
{
    var coach = new CoachEngine();
    var (session, _) = SessionWithFuelEstimate();

    var answer = coach.Answer(session, "fuel status");

    Assert(answer.Content.Contains("You have 5.0 liters.", StringComparison.Ordinal), "Fuel answer should lead with the actual fuel level.");
    Assert(answer.Content.Contains("latest fuel: 5.0", StringComparison.Ordinal), "Fuel answer should include latest fuel evidence.");
}

static void CoachRecentMistakesSummarizesEvents()
{
    var coach = new CoachEngine();
    var engine = new EventEngine();
    var session = new SessionState();
    Apply(session, engine, Packet("""{"lap_progress":0.2,"brake":0.9,"speed_kmh":140}"""));
    Apply(session, engine, PacketAt("""{"lap_progress":0.3,"brake":0.2,"speed_kmh":138}""", DateTimeOffset.UtcNow.AddSeconds(5)));

    var answer = coach.Answer(session, "recent mistakes");

    Assert(answer.Content.Contains("AbruptBrakeRelease", StringComparison.Ordinal), "Recent mistakes should summarize deterministic event types.");
    Assert(answer.GroundedEventIds.Count > 0, "Recent mistakes should be grounded in event ids.");
}

static void CoachNextLapFocusChoosesHighestPriorityIssue()
{
    var coach = new CoachEngine();
    var engine = new EventEngine();
    var session = new SessionState();
    Apply(session, engine, Packet("""{"lap_progress":0.2,"brake":0.9,"speed_kmh":140,"fuel":2.5}"""));

    var answer = coach.Answer(session, "what should I focus on next lap?");

    Assert(answer.Content.Contains("Fuel is tight", StringComparison.Ordinal), "Next-lap focus should choose highest priority recent issue.");
    Assert(answer.Content.Contains("severity: Critical", StringComparison.Ordinal), "Next-lap focus should include evidence for priority.");
}

static void CoachDoesNotInventFakeValuesWhenDataIsNull()
{
    var coach = new CoachEngine();
    var session = new SessionState();
    session.ApplySnapshot(Packet("""{"lap_progress":0.2}"""), []);

    var answer = coach.Answer(session, "fuel status");

    Assert(!answer.Content.Contains("0.0", StringComparison.Ordinal), "Coach must not invent fake zero values when fuel is null.");
    Assert(answer.Content.Contains("unavailable", StringComparison.OrdinalIgnoreCase), "Coach should state unavailable data.");
}

static void CriticalEventProducesVoiceCallout()
{
    var manager = new CalloutManager();
    var callout = manager.TryCreateCallout(Event(EventType.LowFuel, EventSeverity.Critical, DateTimeOffset.UtcNow));

    Assert(callout == "Fuel is low. Start saving.", "Critical low fuel event should produce a race-safe callout.");
}

static void LowPriorityRepeatedVoiceEventIsSuppressed()
{
    var manager = new CalloutManager();
    var start = DateTimeOffset.UtcNow;
    var first = manager.TryCreateCallout(Event(EventType.SteeringOveruse, EventSeverity.Warning, start));
    var second = manager.TryCreateCallout(Event(EventType.SteeringOveruse, EventSeverity.Warning, start.AddSeconds(3)));

    Assert(first is not null, "First technique callout should be eligible.");
    Assert(second is null, "Repeated technique callout inside voice cooldown should be suppressed.");
    Assert(manager.GetSuppressedCount(EventType.SteeringOveruse) == 1, "Voice suppression count should update.");
}

static void MutedVoiceProducesNoSpokenOutput()
{
    var output = new RecordingVoiceOutput();
    var voice = new VoiceService(output);
    voice.SetVoiceEnabled(true);
    voice.SetMuted(true);

    var spoken = voice.SpeakAutomaticCallout("Fuel is low. Start saving.", DateTimeOffset.UtcNow);

    Assert(!spoken, "Muted voice should not speak.");
    Assert(output.SpokenTexts.Count == 0, "Muted voice should not write to speech output.");
}

static void SpokenQueryRoutesThroughCoachEngine()
{
    var output = new RecordingVoiceOutput();
    var voice = new VoiceService(output);
    voice.SetVoiceEnabled(true);
    voice.SetMuted(false);
    var coach = new CoachEngine();
    var (session, _) = SessionWithFuelEstimate();

    var result = voice.HandleSpokenQuery(session, "fuel status", coach);

    Assert(result.WrittenResponse.Content.Contains("latest fuel: 5.0", StringComparison.Ordinal), "Spoken query should route through deterministic CoachEngine.");
    Assert(result.Spoken, "Spoken query should produce spoken output when unmuted.");
    Assert(output.SpokenTexts.Count == 1, "Spoken query should produce one spoken response when unmuted.");
    Assert(!output.SpokenTexts[0].Contains("Evidence:", StringComparison.Ordinal), "Spoken response should be shorter than written chat response.");
}

static void VoiceInputServiceRoutesRecognizedSpeech()
{
    var provider = new RecordingSpeechRecognitionProvider();
    var service = new VoiceInputService(provider);
    service.SetEnabled(true);
    string? query = null;
    service.QueryRecognized += (_, args) => query = args.Text;

    service.BeginPushToTalk();
    provider.SimulateRecognition("fuel status");
    service.EndPushToTalk();

    Assert(query == "fuel status", "Voice input should route recognized speech into query events.");
}

static void VoiceInputServiceEnforcesQueryCooldown()
{
    var provider = new RecordingSpeechRecognitionProvider();
    var service = new VoiceInputService(provider, new VoiceInputOptions { QueryCooldown = TimeSpan.FromSeconds(30) });
    service.SetEnabled(true);
    var queries = new List<string>();
    service.QueryRecognized += (_, args) => queries.Add(args.Text);

    service.BeginPushToTalk();
    provider.SimulateRecognition("fuel status");
    provider.SimulateRecognition("tyre status");
    service.EndPushToTalk();

    Assert(queries.Count == 1, "Voice input cooldown should suppress rapid repeat queries.");
    Assert(queries[0] == "fuel status", "First recognized query should still be accepted.");
}

static void VoiceInputConfirmationPrefixesSpokenResponse()
{
    var output = new RecordingVoiceOutput();
    var voice = new VoiceService(output);
    voice.SetVoiceEnabled(true);
    voice.SetMuted(false);
    var coach = new CoachEngine();
    var (session, _) = SessionWithFuelEstimate();

    _ = voice.HandleSpokenQuery(session, "fuel status", coach, confirmQuery: true);

    Assert(output.SpokenTexts.Count == 1, "Confirmation mode should still produce one spoken response.");
    Assert(output.SpokenTexts[0].StartsWith("Copy.", StringComparison.Ordinal), "Confirmation mode should prefix the spoken response.");
    Assert(
        output.SpokenTexts[0].Contains("fuel", StringComparison.OrdinalIgnoreCase),
        "Confirmation mode should preserve the actionable answer after Copy.");
    Assert(
        output.SpokenTexts[0].Length > "Copy.".Length,
        "Spoken confirmation must not collapse to only Copy.");
}

static void SpokenSummaryConfirmationPreservesAnswerAfterCopyPrefix()
{
    var output = new RecordingVoiceOutput();
    var voice = new VoiceService(output);
    voice.SetVoiceEnabled(true);
    voice.SetMuted(false);
    var hybrid = new HybridCoachEngine(
        new MockEngineerAiProvider(),
        new EngineerAiOptions(true, "mock", "", "", 40, 3));
    var (session, _) = SessionWithFuelEstimate();
    var evidence = BuildSampleCoachEvidence(session);

    var result = voice.HandleSpokenQuery(
        session,
        "how much fuel do i have",
        hybrid,
        evidence: evidence,
        confirmQuery: true);

    Assert(result.Spoken, "Hybrid AI fuel answer should be spoken.");
    Assert(result.FinalTtsPayload.StartsWith("Copy.", StringComparison.Ordinal), "Confirmation payload should include Copy prefix.");
    Assert(
        result.FinalTtsPayload.Contains("fuel", StringComparison.OrdinalIgnoreCase)
            || result.FinalTtsPayload.Contains("goriv", StringComparison.OrdinalIgnoreCase)
            || result.FinalTtsPayload.Contains("liters", StringComparison.OrdinalIgnoreCase),
        "Confirmation payload should include fuel summary.");
    Assert(!string.IsNullOrWhiteSpace(result.GeneratedSummary), "Spoken summary should be generated.");
    Assert(!string.IsNullOrWhiteSpace(result.OriginalAnswer), "Original answer should be logged.");
}

static void SpokenSummaryGeneratorExtractsActionableSentence()
{
    var message = new CoachMessage(
        "coach",
        "Najveći gubitak vremena je u Sector delta (Lost 0.5s in sector 2); Lap comparison (Gap 0.8s).",
        [],
        "AI-assisted from structured telemetry evidence.",
        []);
    var summary = SpokenSummaryGenerator.GenerateSpokenSummary(message);

    Assert(!string.IsNullOrWhiteSpace(summary.Summary), "AI-style answer should produce a spoken summary.");
    Assert(summary.Summary.Contains("gubitak", StringComparison.OrdinalIgnoreCase)
        || summary.Summary.Contains("Sector", StringComparison.OrdinalIgnoreCase),
        "Spoken summary should preserve actionable losing-time content.");
    var payload = SpokenSummaryGenerator.ComposeSpokenPayload(summary.Summary, confirmQuery: true, maxWords: 14);
    Assert(payload.StartsWith("Copy.", StringComparison.Ordinal), "Composed payload should include Copy prefix.");
    Assert(payload.Length > "Copy.".Length, "Composed payload should not collapse to Copy only.");
}

static void VoiceInputStartupHandlesLazyProviderFailures()
{
    var lazy = new LazySpeechRecognitionProvider(() => throw new InvalidOperationException("Speech init failed."));
    var service = VoiceInputStartup.CreateService(lazy, enabled: true);

    Assert(service.StatusText.Contains("ready", StringComparison.OrdinalIgnoreCase)
        || service.StatusText.Contains("disabled", StringComparison.OrdinalIgnoreCase)
        || service.StatusText.Contains("unavailable", StringComparison.OrdinalIgnoreCase),
        "Startup should leave voice input in a safe state before first use.");

    service.BeginPushToTalk();

    Assert(!service.IsListening || service.StatusText.Contains("unavailable", StringComparison.OrdinalIgnoreCase),
        "Failed lazy speech initialization should degrade without crashing.");
}

static void LazySpeechRecognitionProviderInitializesOnPushToTalk()
{
    var lazy = new LazySpeechRecognitionProvider(() => new UnavailableSpeechRecognitionProvider("provider init probe"));
    Assert(!lazy.IsInitialized, "Lazy provider should not initialize before first use.");
    Assert(lazy.IsAvailable, "Lazy provider should allow initialization attempt before first use.");

    var service = VoiceInputStartup.CreateService(lazy, enabled: true);
    service.BeginPushToTalk();

    Assert(lazy.IsInitialized, "PTT should initialize lazy speech provider.");
    Assert(!lazy.IsAvailable, "Unavailable inner provider should report unavailable after init.");
}

static void SpeechRecognitionCultureResolverSupportsConfiguredAndAutoFallback()
{
    var croatian = SpeechRecognitionCultureResolver.Resolve("hr-HR");
    Assert(croatian.Culture.Name == "hr-HR", "Configured hr-HR culture should resolve.");

    var english = SpeechRecognitionCultureResolver.Resolve("en-US");
    Assert(english.Culture.Name == "en-US", "Configured en-US culture should resolve.");

    var auto = SpeechRecognitionCultureResolver.Resolve("");
    Assert(!string.IsNullOrWhiteSpace(auto.Culture.Name), "Auto culture resolution should produce a culture name.");

    var invalid = SpeechRecognitionCultureResolver.Resolve("not-a-culture");
    Assert(invalid.WarningMessage is not null, "Invalid culture should produce a warning.");
    Assert(!string.IsNullOrWhiteSpace(invalid.Culture.Name), "Invalid culture should fall back safely.");
}

static void SpeechRecognitionProviderSelectionSupportsConfiguredValues()
{
    Assert(
        SpeechRecognitionProviderSelection.Parse("auto") == SpeechRecognitionProviderKind.Auto,
        "Auto provider should parse.");
    Assert(
        SpeechRecognitionProviderSelection.Parse("windows") == SpeechRecognitionProviderKind.Windows,
        "Windows provider should parse.");
    Assert(
        SpeechRecognitionProviderSelection.Parse("whisper") == SpeechRecognitionProviderKind.Whisper,
        "Whisper provider should parse.");

    var invalid = SpeechRecognitionProviderSelection.Resolve("not-a-provider");
    Assert(invalid.Kind == SpeechRecognitionProviderKind.Auto, "Invalid provider should fall back to auto.");
    Assert(invalid.WarningMessage is not null, "Invalid provider should produce a warning.");
}

static void WhisperModelLocatorResolvesDefaultAndConfiguredPaths()
{
    var configured = WhisperModelLocator.ResolveModelPath(@"C:\Models\ggml-small.bin");
    Assert(configured == @"C:\Models\ggml-small.bin", "Configured Whisper model path should resolve verbatim.");

    var defaultPath = WhisperModelLocator.ResolveModelPath("");
    Assert(
        defaultPath.EndsWith(Path.Combine("RaceEngineer", "Models", WhisperModelLocator.DefaultModelFileName), StringComparison.OrdinalIgnoreCase),
        "Default Whisper model path should use LocalAppData/RaceEngineer/Models.");
}

static void WhisperSpeechOptionsNormalizeSettingsValues()
{
    var defaults = WhisperSpeechOptions.FromSettings(AppSettings.Default);
    Assert(defaults.LanguageMode == WhisperLanguageModeResolver.Auto, "Default Whisper language mode should be auto.");
    Assert(defaults.TrailingAudioMilliseconds == 500, "Default trailing audio should be 500 ms.");
    Assert(defaults.NoSpeechThreshold == 0.5f, "Default no-speech threshold should be 0.5.");
    Assert(!string.IsNullOrWhiteSpace(defaults.Prompt), "Default Whisper prompt should not be empty.");

    var configured = WhisperSpeechOptions.FromSettings(AppSettings.Default with
    {
        WhisperLanguageMode = "hr",
        WhisperPrompt = "braking fuel pace",
        WhisperTrailingAudioMilliseconds = 900,
        WhisperNoSpeechThreshold = 0.05f
    });

    Assert(configured.LanguageMode == "hr", "Configured Croatian language mode should normalize to hr.");
    Assert(configured.Prompt == "braking fuel pace", "Configured prompt should pass through.");
    Assert(configured.TrailingAudioMilliseconds == 700, "Trailing audio should clamp to 700 ms.");
    Assert(configured.NoSpeechThreshold == 0.1f, "No-speech threshold should clamp to minimum 0.1.");
    Assert(configured.PreRollAudioMilliseconds == 400, "Default pre-roll should be 400 ms.");
}

static void MicrophonePcmConverterHandlesFloatStereo48k()
{
    const int sampleRate = 48_000;
    const int channels = 2;
    const int frameCount = sampleRate / 10;
    var buffer = new byte[frameCount * channels * 4];
    for (var frame = 0; frame < frameCount; frame++)
    {
        var value = 0.5f * MathF.Sin(2f * MathF.PI * 440f * frame / sampleRate);
        for (var channel = 0; channel < channels; channel++)
        {
            var offset = (frame * channels + channel) * 4;
            BitConverter.TryWriteBytes(buffer.AsSpan(offset, 4), value);
        }
    }

    var format = new CapturedAudioFormat(sampleRate, 32, channels, CapturedAudioEncoding.IeeeFloat, "Test mic");
    var chunk = MicrophonePcmConverter.ConvertToWhisperPcm16(buffer, 0, buffer.Length, format);
    Assert(chunk.Pcm16.Length > 0, "Converted PCM should not be empty.");
    Assert(chunk.ConvertedPeakRms > 1000f, $"Float stereo 48 kHz should produce realistic RMS, got {chunk.ConvertedPeakRms:0}.");
}

static void MicrophonePcmConverterInfersUnknown32BitAsFloat()
{
    const int sampleRate = 48_000;
    const int channels = 2;
    const int frameCount = sampleRate / 10;
    var buffer = new byte[frameCount * channels * 4];
    for (var frame = 0; frame < frameCount; frame++)
    {
        var value = 0.5f * MathF.Sin(2f * MathF.PI * 440f * frame / sampleRate);
        for (var channel = 0; channel < channels; channel++)
        {
            var offset = (frame * channels + channel) * 4;
            BitConverter.TryWriteBytes(buffer.AsSpan(offset, 4), value);
        }
    }

    var format = new CapturedAudioFormat(sampleRate, 32, channels, CapturedAudioEncoding.Unknown, "Test mic", 8);
    var chunk = MicrophonePcmConverter.ConvertToWhisperPcm16(buffer, 0, buffer.Length, format);
    Assert(chunk.ConvertedPeakRms > 1000f, $"Unknown 32-bit capture should infer float decode, got {chunk.ConvertedPeakRms:0}.");
    Assert(chunk.RawPeakRms > 1000f, $"Raw peak should stay realistic for float capture, got {chunk.RawPeakRms:0}.");
}

static void MicrophonePcmConverterHandlesPcm16Stereo44100()
{
    const int sampleRate = 44_100;
    const int channels = 2;
    const int frameCount = sampleRate / 10;
    var buffer = new byte[frameCount * channels * 2];
    for (var frame = 0; frame < frameCount; frame++)
    {
        var sample = (short)Math.Round(Math.Sin(2d * Math.PI * 440d * frame / sampleRate) * 12_000d);
        for (var channel = 0; channel < channels; channel++)
        {
            var offset = (frame * channels + channel) * 2;
            BitConverter.TryWriteBytes(buffer.AsSpan(offset, 2), sample);
        }
    }

    var format = new CapturedAudioFormat(sampleRate, 16, channels, CapturedAudioEncoding.Pcm, "Test mic");
    var chunk = MicrophonePcmConverter.ConvertToWhisperPcm16(buffer, 0, buffer.Length, format);
    Assert(chunk.Pcm16.Length > 0, "Converted PCM should not be empty.");
    Assert(chunk.ConvertedPeakRms > 1000f, $"PCM16 stereo 44.1 kHz should produce realistic RMS, got {chunk.ConvertedPeakRms:0}.");
}

static void TranscriptGateRejectsWeakSignalAndHallucinations()
{
    var weak = TranscriptGate.Evaluate(
        "bye bye",
        0.90f,
        new SpeechCaptureMetrics(10f, 10f, 10f, 1f, false, false, "Test mic", MicSignalQuality.Bad));
    Assert(!weak.Accepted, "Weak/no-speech capture should reject hallucinated transcript.");
    Assert(weak.SpokenRejectionMessage.Contains("catch", StringComparison.OrdinalIgnoreCase)
        || weak.SpokenRejectionMessage.Contains("unclear", StringComparison.OrdinalIgnoreCase),
        "Rejected transcript should map to spoken rejection message.");

    var good = TranscriptGate.Evaluate(
        "koliko goriva imam",
        0.80f,
        new SpeechCaptureMetrics(500f, 900f, 1200f, 2f, true, false, "Test mic", MicSignalQuality.Good));
    Assert(good.Accepted, "Known racing phrase with good signal should pass transcript gate.");
}

static void VoiceInputServiceRejectsGarbageTranscript()
{
    var provider = new RecordingSpeechRecognitionProvider();
    string? rejectionMessage = null;
    var service = new VoiceInputService(provider, new VoiceInputOptions
    {
        MinimumConfidence = 0.50f,
        TranscriptGate = TranscriptGateOptions.Default with { MinimumConfidence = 0.50f }
    });
    service.SetEnabled(true);
    service.TranscriptRejected += (_, args) => rejectionMessage = args.SpokenMessage;
    service.BeginPushToTalk();
    provider.SimulateRecognition(
        "bye bye",
        0.90f,
        new SpeechCaptureMetrics(10f, 10f, 10f, 1f, false, false, "Test mic", MicSignalQuality.Bad));
    service.EndPushToTalk();

    Assert(rejectionMessage is not null, "Garbage transcript should produce rejection spoken message.");
}

static void StrategyEngineComputesDeterministicFuelMetrics()
{
    var (session, _) = SessionWithFuelEstimate();
    var analytics = new TelemetryAnalyticsService().Analyze(new SessionAnalyticsInput(session));
    var first = new StrategyEngine().Analyze(new StrategyInput(session, analytics));
    var second = new StrategyEngine().Analyze(new StrategyInput(session, analytics));

    Assert(first.Fuel.FuelUsedPerLap == second.Fuel.FuelUsedPerLap, "Fuel per lap should be deterministic.");
    Assert(first.Fuel.LapsRemaining == second.Fuel.LapsRemaining, "Laps remaining should be deterministic.");
    Assert(first.Pit.Recommendation == second.Pit.Recommendation, "Pit recommendation should be deterministic.");
    Assert(first.Summary == second.Summary, "Strategy summary should be deterministic.");
}

static void StrategyCalloutManagerSpeaksOnlyOnStateChange()
{
    var manager = new StrategyCalloutManager();
    var timestamp = DateTimeOffset.UtcNow;
    var strategy = new StrategyEngine().Analyze(new StrategyInput(SessionWithFuelEstimate().Session));

    var first = manager.TryCreateCallout(strategy, timestamp);
    var second = manager.TryCreateCallout(strategy, timestamp.AddSeconds(1));

    if (first is not null)
    {
        Assert(second is null, "Unchanged strategy state should not create another callout.");
    }
}

static void SessionContextSuppressesStationaryFuelPanic()
{
    var classifier = new SessionContextClassifier();
    var session = new SessionState();
    session.ApplySnapshot(
        Packet("""{"speed_kmh":0,"fuel":30.0,"lap_progress":0.12}"""),
        []);
    var context = classifier.Classify(new SessionContextInput(session, [session.LatestSnapshot!]));

    Assert(context.Activity == VehicleActivity.Stationary, "Zero speed should classify as stationary.");
    Assert(!context.AllowLowFuelVoiceCallouts, "Stationary context should suppress low-fuel voice callouts.");

    var raw = new StrategyEngine().Analyze(new StrategyInput(session));
    var gated = StrategyGate.Apply(raw, context, session);

    Assert(gated.CalloutSignal is null, "Stationary strategy should not emit fuel panic callouts.");
    Assert(gated.Summary.Contains("30.0 L", StringComparison.Ordinal), "Stationary summary should report actual fuel level.");
    Assert(!gated.Summary.Contains("tight", StringComparison.OrdinalIgnoreCase), "Stationary summary should not claim fuel is tight.");
}

static void StrategyGateSuppressesPracticeStrategyCallouts()
{
    var classifier = new SessionContextClassifier();
    var (session, _) = SessionWithFuelEstimate();
    var context = classifier.Classify(new SessionContextInput(
        session,
        [session.LatestSnapshot!],
        new RacePrepPlan(null, null, "Practice", null, null, null, null, null, null, null)));

    var raw = new StrategyEngine().Analyze(new StrategyInput(session));
    var gated = StrategyGate.Apply(raw, context, session);

    Assert(context.Phase == SessionPhase.Practice, "Prep session type should classify as practice.");
    Assert(gated.CalloutSignal is null, "Practice session should suppress unsolicited strategy callouts.");
    Assert(gated.Pit.Recommendation is not PitRecommendation.PitNow, "Practice session should not recommend an immediate pit stop.");
}

static void CoachFuelAnswerLeadsWithActualFuelLevel()
{
    var coach = new CoachEngine();
    var session = new SessionState();
    session.ApplySnapshot(Packet("""{"speed_kmh":0,"fuel":30.0}"""), []);

    var answer = coach.Answer(
        session,
        "how much fuel do i have",
        new CoachContext(SessionContext: new SessionContextAssessment(
            SessionPhase.Practice,
            VehicleActivity.Stationary,
            "Practice / Stationary",
            StrategyConfidenceLevel.Low,
            "Low",
            false,
            false,
            false,
            false,
            false,
            false,
            false,
            "Waiting for stable lap samples.")));

    Assert(answer.Content.Contains("You have 30.0 liters.", StringComparison.Ordinal), "Fuel question should answer with actual telemetry fuel first.");
    Assert(answer.Content.Contains("Not enough race data yet", StringComparison.OrdinalIgnoreCase), "Insufficient context should be stated explicitly.");
}

static void VoiceInteractionGateSuppressesAutomaticCalloutsAfterQuestion()
{
    var output = new RecordingVoiceOutput();
    var voice = new VoiceService(output);
    voice.SetVoiceEnabled(true);
    voice.SetMuted(false);
    var coach = new CoachEngine();
    var (session, _) = SessionWithFuelEstimate();
    var manager = new CalloutManager();

    _ = voice.HandleSpokenQuery(session, "koliko goriva imam", coach);
    var blocked = voice.SpeakAutomaticCallout("Brake release is unstable.", DateTimeOffset.UtcNow);
    var raceContext = new SessionContextAssessment(
        SessionPhase.Race,
        VehicleActivity.OnTrack,
        "Race / On track",
        StrategyConfidenceLevel.High,
        "High",
        true,
        true,
        true,
        true,
        true,
        true,
        true,
        "Race context active.");
    var callout = manager.TryCreateCallout(
        Event(EventType.AbruptBrakeRelease, EventSeverity.Warning, DateTimeOffset.UtcNow),
        raceContext);

    Assert(output.SpokenTexts.Count == 1, "Only the direct answer should be spoken during quiet window.");
    Assert(!blocked, "Automatic callout should be blocked immediately after a spoken question.");
    Assert(callout is not null, "Callout manager should still create eligible callouts.");
    Assert(!voice.SpeakAutomaticCallout(callout!, DateTimeOffset.UtcNow), "Automatic callout speech should honor post-question quiet window.");
}

static void CoachEngineRoutesCroatianTyrePhrase()
{
    var coach = new CoachEngine();
    var session = new SessionState();
    session.ApplySnapshot(Packet("""{"speed_kmh":120,"tyre_temp_c":[90,91,89,88]}"""), []);

    var answer = coach.Answer(session, "kakve su gume");

    Assert(
        answer.Content.Contains("tyre", StringComparison.OrdinalIgnoreCase)
            || answer.Content.Contains("temp", StringComparison.OrdinalIgnoreCase),
        "Croatian tyre phrase should route to tyre coaching.");
}

static void CoachEngineRoutesStrategyQuestions()
{
    var coach = new CoachEngine();
    var (session, _) = SessionWithFuelEstimate();
    var analytics = new TelemetryAnalyticsService().Analyze(new SessionAnalyticsInput(session));
    var strategy = new StrategyEngine().Analyze(new StrategyInput(session, analytics));
    var evidence = new CoachEvidenceBuilder().Build(new CoachEvidenceInput(
        session,
        null,
        session.Events,
        analytics,
        null,
        null,
        null,
        strategy));

    var english = coach.Answer(session, "what is my strategy", evidence: evidence);
    var croatian = coach.Answer(session, "kakva je strategija", evidence: evidence);
    var pit = coach.Answer(session, "do I need to pit", evidence: evidence);

    Assert(english.EvidencePackets.Count > 0, "English strategy question should include evidence packets.");
    Assert(croatian.EvidencePackets.Count > 0, "Croatian strategy question should include evidence packets.");
    Assert(pit.EvidencePackets.Count > 0, "Pit strategy question should include evidence packets.");
    Assert(
        english.EvidencePackets.Any(packet => packet.Summary == "Pit recommendation"),
        "Strategy evidence should include a pit recommendation packet.");
    Assert(
        english.EvidencePackets.Any(packet => packet.Summary == "Strategy summary"),
        "Strategy evidence should include a strategy summary packet.");
}

static async Task ProfilePreferencesPersistAndMergeAppSettings()
{
    var dbPath = Path.Combine(Path.GetTempPath(), $"race-engineer-profile-{Guid.NewGuid():N}.sqlite3");
    var storage = new StorageService(dbPath);
    await storage.InitializeAsync();
    var service = new ProfilePreferencesService(storage);
    var baseSettings = AppSettings.Default with { PushToTalkHotkey = "F7", SpeechRecognitionProvider = "windows" };
    var bundle = new UserPreferencesBundle(
        new DriverProfileRecord(DriverName: "Alex", PreferredLanguage: "hr-HR", DrivingStyle: "aggressive"),
        new CoachPreferencesRecord(
            ResponseLength: "detailed",
            CalloutAggressiveness: "high",
            VoiceEnabledDefault: true,
            SpeechRecognitionProvider: "whisper",
            PushToTalkHotkey: "F8",
            VoiceInputConfirmationsEnabled: false,
            EvidenceBulletsEnabled: true),
        new StrategyPreferencesRecord(
            FuelSafetyMarginLaps: 2.5,
            PitRecommendationAggressiveness: "aggressive",
            TyreRiskSensitivity: "high",
            PitStrategyPreference: "undercut"));

    await service.SaveAsync(bundle);
    var loaded = await service.LoadAsync(baseSettings);

    Assert(loaded.Driver.DriverName == "Alex", "Driver name should round-trip through SQLite.");
    Assert(loaded.Driver.PreferredLanguage == "hr-HR", "Preferred language should round-trip.");
    Assert(loaded.Driver.DrivingStyle == "aggressive", "Driving style should round-trip.");
    Assert(loaded.Coach.ResponseLength == "detailed", "Coach response length should round-trip.");
    Assert(loaded.Coach.PushToTalkHotkey == "F8", "Push-to-talk hotkey should round-trip.");
    Assert(loaded.Strategy.FuelSafetyMarginLaps == 2.5, "Fuel safety margin should round-trip.");
    Assert(loaded.Strategy.PitStrategyPreference == "undercut", "Pit strategy preference should round-trip.");

    var merged = ProfilePreferencesService.MergeAppSettings(baseSettings, loaded);
    Assert(merged.VoiceEnabledDefault, "Merged settings should use saved voice default.");
    Assert(merged.PushToTalkHotkey == "F8", "Merged settings should use saved push-to-talk hotkey.");
    Assert(merged.SpeechRecognitionProvider == "whisper", "Merged settings should use saved speech provider.");
    Assert(merged.SpeechRecognitionCulture == "hr-HR", "Merged settings should derive speech culture from driver language.");
}

static void CoachResponseFormatterRespectsResponseLength()
{
    var content = "Plan an early pit stop on lap twelve because fuel is trending low. Keep tyre temps stable through sector two."
        + Environment.NewLine + Environment.NewLine
        + "Evidence:" + Environment.NewLine
        + "- Fuel laps remaining is 3.2" + Environment.NewLine
        + "- Pit window opens in 2 laps" + Environment.NewLine
        + "- Tyre risk is moderate" + Environment.NewLine
        + "- Undercut window is open";

    var shortPrefs = new CoachPreferencesRecord(ResponseLength: "short", EvidenceBulletsEnabled: true);
    var detailedPrefs = new CoachPreferencesRecord(ResponseLength: "detailed", EvidenceBulletsEnabled: true);
    var shortContent = CoachResponseFormatter.FormatContent(content, shortPrefs);
    var detailedContent = CoachResponseFormatter.FormatContent(content, detailedPrefs);

    Assert(shortContent.Length < detailedContent.Length, "Short response length should produce shorter coach content.");
    Assert(shortContent.Contains("Evidence:", StringComparison.Ordinal), "Short responses should still include evidence when enabled.");
    Assert(
        shortContent.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Count(line => line.StartsWith("- ", StringComparison.Ordinal)) <= 2,
        "Short response length should cap evidence bullets.");
    Assert(
        detailedContent.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Count(line => line.StartsWith("- ", StringComparison.Ordinal)) >= 3,
        "Detailed response length should retain more evidence bullets.");
}

static void CoachEngineRoutesCroatianBrakingPhrase()
{
    var coach = new CoachEngine();
    var (session, _) = SessionWithFuelEstimate();
    var answer = coach.Answer(session, "kako kočim");

    Assert(
        answer.Content.Contains("brake", StringComparison.OrdinalIgnoreCase)
            || answer.Content.Contains("Brake", StringComparison.Ordinal)
            || answer.Uncertainty?.Contains("brake", StringComparison.OrdinalIgnoreCase) == true,
        "Croatian braking phrase should route to brake coaching.");
    Assert(
        !answer.Content.Contains("Not enough braking data yet", StringComparison.OrdinalIgnoreCase),
        "Braking answer should be available after valid completed laps.");
}

static void CoachStationaryPitBrakingReturnsNoDataYet()
{
    var coach = new CoachEngine();
    var engine = new EventEngine();
    var session = new SessionState();
    Apply(session, engine, Packet("""{"speed_kmh":0,"fuel":30.0,"lap_progress":0.05,"race":{"flags":"pit"}}"""));
    var context = new SessionContextClassifier().Classify(new SessionContextInput(session, [session.LatestSnapshot!]));

    var answer = coach.Answer(
        session,
        "how is my braking",
        new CoachContext(SessionContext: context),
        new CoachEvidenceBuilder().Build(new CoachEvidenceInput(session, null, session.Events)));

    Assert(
        answer.Content.Contains("No braking data yet", StringComparison.OrdinalIgnoreCase),
        "Stationary pit with no completed laps should not provide braking technique feedback.");
    Assert(
        !answer.Content.Contains("No recent braking problem is active", StringComparison.OrdinalIgnoreCase),
        "Braking answer should not fall back to generic event-buffer text before any lap data.");
    Assert(
        !answer.EvidencePackets.Any(packet => packet.Summary == EventType.InvalidLapOrFlags.ToString()),
        "InvalidLapOrFlags should not be attached as braking technique evidence.");
}

static void CoachBrakingAnswersAfterValidLap()
{
    var coach = new CoachEngine();
    var (session, engine) = SessionWithFuelEstimate();
    Apply(session, engine, Packet("""{"speed_kmh":0,"fuel":5.0,"lap_progress":0.12,"brake":0.0}"""));
    var context = new SessionContextClassifier().Classify(new SessionContextInput(session, [session.LatestSnapshot!]));

    var answer = coach.Answer(session, "how is my braking", new CoachContext(SessionContext: context));

    Assert(
        !answer.Content.Contains("No braking data yet", StringComparison.OrdinalIgnoreCase),
        "Braking feedback should be available after at least one valid completed lap.");
}

static void SpokenBrakingSummaryWhenNoDataYet()
{
    var coach = new CoachEngine();
    var session = new SessionState();
    session.ApplySnapshot(Packet("""{"speed_kmh":0,"fuel":30.0}"""), []);
    var context = new SessionContextAssessment(
        SessionPhase.Practice,
        VehicleActivity.PitLane,
        "Practice / Pit lane",
        StrategyConfidenceLevel.Low,
        "Low",
        false,
        false,
        false,
        false,
        false,
        false,
        false,
        "Waiting for stable lap samples.");

    var answer = coach.Answer(session, "how is my braking", new CoachContext(SessionContext: context));
    var spoken = SpokenSummaryGenerator.GenerateSpokenSummary(answer, query: "how is my braking");

    Assert(
        spoken.Summary.Contains("No braking data yet", StringComparison.OrdinalIgnoreCase),
        "Spoken braking summary should clearly state no data yet.");
    Assert(
        spoken.Summary.Contains("clean lap", StringComparison.OrdinalIgnoreCase),
        "Spoken braking summary should tell the driver to complete a clean lap first.");
}

static void HybridCoachStationaryPitBrakingDoesNotUseInvalidLapFlags()
{
    var hybrid = new HybridCoachEngine(
        new MockEngineerAiProvider(),
        new EngineerAiOptions(true, "mock", "", "", 40, 3));
    var engine = new EventEngine();
    var session = new SessionState();
    Apply(session, engine, Packet("""{"speed_kmh":0,"fuel":30.0,"lap_progress":0.05,"race":{"flags":"pit"}}"""));
    var context = new SessionContextClassifier().Classify(new SessionContextInput(session, [session.LatestSnapshot!]));
    var coachContext = new CoachContext(SessionContext: context);
    var evidence = new CoachEvidenceBuilder().Build(new CoachEvidenceInput(session, null, session.Events));

    var answer = hybrid.Answer(session, "how is my braking", coachContext, evidence);
    Assert(
        !answer.Content.Contains("InvalidLapOrFlags", StringComparison.OrdinalIgnoreCase),
        "Hybrid coach answer must not leak InvalidLapOrFlags for blocked technique queries.");
    Assert(
        answer.Content.Contains("No braking data yet", StringComparison.OrdinalIgnoreCase),
        "Hybrid coach answer should use the driving technique gate message.");

    var pipeline = CoachQueryPipeline.Resolve(session, "how is my braking", hybrid, coachContext, evidence);
    Assert(
        pipeline.Trace.GateBlocked,
        "Pipeline trace should record the driving technique gate as blocked.");
    Assert(
        pipeline.FinalDisplayedText == CoachQueryPipeline.ExpectedStationaryBrakingGateMessage,
        "Pipeline final displayed text should use the braking gate message.");
    Assert(
        pipeline.SpokenSummary.Summary == CoachQueryPipeline.ExpectedStationaryBrakingGateMessage,
        "Pipeline spoken summary should use the braking gate message.");
    Assert(
        pipeline.FinalWritten.Content == CoachQueryPipeline.ExpectedStationaryBrakingGateMessage,
        "Pipeline final written content should match the unified gate message.");

    var voice = new VoiceService(new RecordingVoiceOutput());
    voice.SetVoiceEnabled(true);
    voice.SetMuted(false);
    var spoken = voice.HandleSpokenQuery(session, "how is my braking", hybrid, coachContext, evidence);
    Assert(
        !spoken.WrittenResponse.Content.Contains("InvalidLapOrFlags", StringComparison.OrdinalIgnoreCase),
        "Spoken query written response must not leak InvalidLapOrFlags.");
    Assert(
        !spoken.SpokenResponse.Contains("InvalidLapOrFlags", StringComparison.OrdinalIgnoreCase),
        "Spoken query TTS must not leak InvalidLapOrFlags.");
    Assert(
        spoken.FinalDisplayedText == CoachQueryPipeline.ExpectedStationaryBrakingGateMessage,
        "Spoken query final displayed text should use the braking gate message.");
    Assert(
        spoken.WrittenResponse.Content == CoachQueryPipeline.ExpectedStationaryBrakingGateMessage,
        "Spoken query written response should match the unified gate message.");
    Assert(
        spoken.SpokenResponse == CoachQueryPipeline.ExpectedStationaryBrakingGateMessage,
        "Spoken query TTS should use the no-data-yet braking message.");
    Assert(
        spoken.FinalDisplayedText == spoken.WrittenResponse.Content,
        "Spoken query UI and written response must use the same final string.");
}

static void PttPipelineStationaryPitBrakingUsesUnifiedGateMessage()
{
    var hybrid = new HybridCoachEngine(
        new MockEngineerAiProvider(),
        new EngineerAiOptions(true, "mock", "", "", 40, 3));
    var engine = new EventEngine();
    var session = new SessionState();
    Apply(session, engine, Packet("""{"speed_kmh":0,"fuel":30.0,"lap_progress":0.05,"race":{"flags":"pit"}}"""));
    var context = new SessionContextClassifier().Classify(new SessionContextInput(session, [session.LatestSnapshot!]));
    var coachContext = new CoachContext(SessionContext: context);
    var evidence = new CoachEvidenceBuilder().Build(new CoachEvidenceInput(session, null, session.Events));
    const string query = "how is my braking";

    CoachQueryRuntimeTrace? runtimeTrace = null;
    void CaptureTrace(CoachQueryRuntimeTrace trace) => runtimeTrace = trace;
    CoachQueryDiagnosticLog.RuntimeTraceRaised += CaptureTrace;

    try
    {
        var chatPipeline = CoachQueryPipeline.Resolve(session, query, hybrid, coachContext, evidence);
        var voice = new VoiceService(new RecordingVoiceOutput());
        voice.SetVoiceEnabled(true);
        voice.SetMuted(false);
        var voiceResult = voice.HandleSpokenQuery(session, query, hybrid, coachContext, evidence);

        Assert(runtimeTrace is not null, "Runtime trace should be raised for the PTT pipeline.");
        Assert(
            runtimeTrace!.Transcript == query,
            "Runtime trace should capture the braking transcript.");
        Assert(
            runtimeTrace.Topic == CoachQueryTopic.Braking,
            "Runtime trace should classify the braking topic.");
        Assert(
            runtimeTrace.CompletedLaps == 0,
            "Runtime trace should record zero completed laps.");
        Assert(
            runtimeTrace.GateBlocked,
            "Runtime trace should record the gate as blocked.");
        Assert(
            !string.IsNullOrWhiteSpace(runtimeTrace.PrimaryAnswer),
            "Runtime trace should capture the primary answer for debugging.");
        Assert(
            !runtimeTrace.PrimaryAnswer.Contains("InvalidLapOrFlags", StringComparison.OrdinalIgnoreCase),
            "Runtime trace primary answer must not leak InvalidLapOrFlags.");
        Assert(
            runtimeTrace.DeterministicAnswer.Contains("No braking data yet", StringComparison.OrdinalIgnoreCase),
            "Runtime trace deterministic answer should use the gate message.");
        Assert(
            chatPipeline.FinalDisplayedText == CoachQueryPipeline.ExpectedStationaryBrakingGateMessage,
            "Chat pipeline final displayed text should use the gate message.");
        Assert(
            voiceResult.FinalDisplayedText == CoachQueryPipeline.ExpectedStationaryBrakingGateMessage,
            "Voice pipeline final displayed text should use the gate message.");
        Assert(
            voiceResult.WrittenResponse.Content == voiceResult.FinalDisplayedText,
            "Voice written response and displayed text must match.");
        Assert(
            voiceResult.SpokenResponse == CoachQueryPipeline.ExpectedStationaryBrakingGateMessage,
            "Voice TTS payload should use the gate message.");
        Assert(
            chatPipeline.FinalDisplayedText == voiceResult.FinalDisplayedText,
            "Chat and voice must expose the same final answer string.");
        Assert(
            !chatPipeline.FinalDisplayedText.Contains("InvalidLapOrFlags", StringComparison.OrdinalIgnoreCase),
            "Final displayed answer must not contain InvalidLapOrFlags.");
        Assert(
            !voiceResult.SpokenResponse.Contains("telemetry", StringComparison.OrdinalIgnoreCase),
            "Final spoken answer must not contain telemetry leak phrasing.");
    }
    finally
    {
        CoachQueryDiagnosticLog.RuntimeTraceRaised -= CaptureTrace;
    }
}

static void VoiceInputAcceptsRecognitionAfterPttReleaseGrace()
{
    var provider = new RecordingSpeechRecognitionProvider();
    var service = new VoiceInputService(provider, new VoiceInputOptions { PttResultGracePeriod = TimeSpan.FromSeconds(2) });
    service.SetEnabled(true);
    string? query = null;
    service.QueryRecognized += (_, args) => query = args.Text;

    service.BeginPushToTalk();
    service.EndPushToTalk();
    provider.SimulateRecognition("kako kočim");

    Assert(query == "kako kočim", "Recognition arriving after PTT release should still route during grace period.");
}

static void VoiceInputReportsNoSpeechAfterGraceTimeout()
{
    var provider = new RecordingSpeechRecognitionProvider();
    var service = new VoiceInputService(provider, new VoiceInputOptions { PttResultGracePeriod = TimeSpan.FromMilliseconds(250) });
    service.SetEnabled(true);
    SpeechRecognitionDiagnosticEventArgs? timeoutDiagnostic = null;
    service.DiagnosticRaised += (_, args) =>
    {
        if (args.Stage == "No speech timeout")
        {
            timeoutDiagnostic = args;
        }
    };

    service.BeginPushToTalk();
    service.EndPushToTalk();
    Thread.Sleep(500);

    Assert(timeoutDiagnostic is not null, "PTT release without speech should emit a no-speech timeout diagnostic.");
    Assert(service.StatusText == "No speech recognized", "Status should report no speech recognized.");
}

static void VoiceCalloutContainsNoFakeTelemetryValues()
{
    var manager = new CalloutManager();
    var callout = manager.TryCreateCallout(Event(EventType.LowFuel, EventSeverity.Critical, DateTimeOffset.UtcNow));

    Assert(callout is not null, "Low fuel should create a callout.");
    var calloutText = callout ?? throw new InvalidOperationException("Low fuel should create a callout.");
    Assert(!calloutText.Contains("0.0", StringComparison.Ordinal), "Callout should not invent telemetry values.");
    Assert(!calloutText.Contains("null", StringComparison.OrdinalIgnoreCase), "Callout should not speak null values.");
}

static void DefaultSettingsLoad()
{
    var missingPath = Path.Combine(Path.GetTempPath(), $"missing-settings-{Guid.NewGuid():N}.json");
    var result = AppSettings.Load(missingPath);

    Assert(result.Settings.UdpBindIp == "127.0.0.1", "Default UDP bind IP should load.");
    Assert(result.Settings.UdpPort == 20999, "Default UDP port should load.");
    Assert(result.Warnings.Count > 0, "Missing settings file should produce a warning.");
}

static void InvalidSettingsFallBackSafely()
{
    var path = Path.Combine(Path.GetTempPath(), $"invalid-settings-{Guid.NewGuid():N}.json");
    File.WriteAllText(path, """{"udpBindIp":"","udpPort":-1,"databasePath":"","captureFolder":"","replayFolder":""}""");

    var result = AppSettings.Load(path);

    Assert(result.Settings.UdpBindIp == "127.0.0.1", "Invalid settings should fall back to default bind IP.");
    Assert(result.Settings.UdpPort == 20999, "Invalid settings should fall back to default port.");
    Assert(result.Warnings.Any(item => item.Contains("Invalid settings", StringComparison.Ordinal)), "Invalid settings should report warning.");
}

static void StoragePathIsCreated()
{
    var directory = Path.Combine(Path.GetTempPath(), $"race-engineer-storage-{Guid.NewGuid():N}");
    var dbPath = Path.Combine(directory, "race_engineer.sqlite3");
    _ = new StorageService(dbPath);

    Assert(Directory.Exists(directory), "StorageService should create database directory.");
}

static async Task ReceiverReportsPortBindFailureClearly()
{
    using var blocker = new UdpClient(new IPEndPoint(IPAddress.Parse("127.0.0.1"), 0));
    var port = ((IPEndPoint)blocker.Client.LocalEndPoint!).Port;
    await using var receiver = new TelemetryReceiver("127.0.0.1", port);

    try
    {
        await receiver.StartAsync();
        throw new InvalidOperationException("Expected receiver bind failure did not occur.");
    }
    catch (InvalidOperationException exception)
    {
        Assert(exception.Message.Contains($"127.0.0.1:{port}", StringComparison.Ordinal), "Bind failure should include endpoint.");
        Assert(exception.Message.Contains("port may already be in use", StringComparison.OrdinalIgnoreCase), "Bind failure should clearly explain likely cause.");
    }
}

static async Task PrepSavesAndLoadsByCarTrack()
{
    var dbPath = Path.Combine(Path.GetTempPath(), $"race-engineer-prep-{Guid.NewGuid():N}.sqlite3");
    var storage = new StorageService(dbPath);
    await storage.InitializeAsync();
    var plan = new RacePrepPlan("Porsche", "Spa", "Practice", "12 laps", "Start with 40L", "Keep fronts under control", "Trail brake smoother", "Breathe on exits", "Wing 4", "Undercut if tyres hold");
    await storage.SaveRacePrepPlanAsync(plan);

    var loaded = await storage.LoadRacePrepPlanAsync("Porsche", "Spa", "Practice");

    Assert(loaded?.FuelPlan == "Start with 40L", "Prep should save/load by car/track/session type.");
}

static void CoachAnswersFuelPlanFromPrep()
{
    var coach = new CoachEngine();
    var plan = new RacePrepPlan("Porsche", "Spa", "Practice", null, "Start with 40L", "Mediums", null, null, null, null);

    var answer = coach.Answer(new SessionState(), "what is the fuel plan?", new CoachContext(null, null, plan));

    Assert(answer.Content.Contains("Start with 40L", StringComparison.Ordinal), "Coach should answer fuel plan from structured prep.");
}

static void ReportIncludesRecurringEventCounts()
{
    var coach = new CoachEngine();
    var engine = new EventEngine();
    var session = new SessionState();
    Apply(session, engine, Packet("""{"lap_progress":0.2,"brake":0.9,"speed_kmh":140}"""));
    Apply(session, engine, PacketAt("""{"lap_progress":0.3,"brake":0.2,"speed_kmh":138}""", DateTimeOffset.UtcNow.AddSeconds(5)));

    var report = coach.GeneratePostSessionReport(session);

    Assert(report.Contains("AbruptBrakeRelease: 1", StringComparison.Ordinal), "Report should include recurring deterministic event counts.");
}

static void ReportSaysUnavailableForMissingTyreBrakeData()
{
    var coach = new CoachEngine();
    var report = coach.GeneratePostSessionReport(new SessionState());

    Assert(report.Contains("Tyres: unavailable", StringComparison.Ordinal), "Report should say tyre data unavailable when missing.");
    Assert(report.Contains("Brakes: unavailable", StringComparison.Ordinal), "Report should say brake data unavailable when missing.");
}

static void ReportDoesNotInventLapTimesWithoutCompletedLaps()
{
    var coach = new CoachEngine();
    var report = coach.GeneratePostSessionReport(new SessionState());

    Assert(report.Contains("Best lap: unavailable", StringComparison.Ordinal), "Report should not invent best lap time.");
    Assert(report.Contains("Last lap: unavailable", StringComparison.Ordinal), "Report should not invent last lap time.");
}

static async Task KnowledgeSourcesSaveSearchAndDelete()
{
    var dbPath = Path.Combine(Path.GetTempPath(), $"race-engineer-knowledge-{Guid.NewGuid():N}.sqlite3");
    var storage = new StorageService(dbPath);
    await storage.InitializeAsync();
    var source = KnowledgeSource.ManualNote("Monza track guide", "Long straights and big stops punish low top speed and unstable braking.", track: "Monza", category: "track", confidenceNote: "User-authored note.");
    await storage.SaveKnowledgeSourceAsync(source);

    var research = new StoredResearchService(storage);
    var search = await research.SearchAsync("Monza");
    var brief = await research.GetTrackBriefAsync("Monza");

    Assert(search.Any(item => item.Id == source.Id), "Stored research search should find saved knowledge sources.");
    Assert(brief.Sources.Count == 1, "Track brief should load stored track notes.");
    Assert(brief.Content.Contains("big stops", StringComparison.Ordinal), "Track brief should include source content.");

    await storage.DeleteKnowledgeSourceAsync(source.Id);
    var afterDelete = await research.SearchAsync("Monza");
    Assert(afterDelete.All(item => item.Id != source.Id), "Deleting outdated notes should remove the knowledge source.");
}

static void CoachAnswersTrackKnowledgeWithSourceLabels()
{
    var coach = new CoachEngine();
    var context = new CoachContext(
        RacePrepPlan: new RacePrepPlan(null, "Brands Hatch", "Practice", null, null, null, null, null, null, null),
        KnowledgeSources: [
            KnowledgeSource.ManualNote("Brands Hatch guide", "Watch front locking into heavy braking zones.", track: "Brands Hatch", category: "track")
        ]);

    var answer = coach.Answer(new SessionState(), "What should I watch for at Brands Hatch?", context);

    Assert(answer.Content.Contains("Stored track notes:", StringComparison.Ordinal), "Knowledge answer should label stored track notes.");
    Assert(answer.Content.Contains("External research:", StringComparison.Ordinal), "Knowledge answer should label external research state.");
    Assert(answer.Content.Contains("Watch front locking", StringComparison.Ordinal), "Knowledge answer should use saved source content.");
}

static void CoachSeparatesTelemetryFromTrackKnowledge()
{
    var coach = new CoachEngine();
    var engine = new EventEngine();
    var session = new SessionState();
    Apply(session, engine, Packet("""{"lap_progress":0.2,"tyre_temp_c":[106,104,99,98],"speed_kmh":120}"""));
    var context = new CoachContext(
        RacePrepPlan: new RacePrepPlan(null, "Monza", "Practice", null, null, null, null, null, null, null),
        KnowledgeSources: [
            KnowledgeSource.ManualNote("Monza guide", "Long loaded corners punish front sliding.", track: "Monza", category: "track")
        ]);

    var answer = coach.Answer(session, "Combine my telemetry with track notes", context);

    Assert(answer.Content.Contains("Telemetry evidence:", StringComparison.Ordinal), "Combined answer should label telemetry evidence.");
    Assert(answer.Content.Contains("Session evidence:", StringComparison.Ordinal), "Combined answer should label session evidence.");
    Assert(answer.Content.Contains("Stored track notes:", StringComparison.Ordinal), "Combined answer should label stored track notes.");
    Assert(answer.Content.Contains("max tyre temp: 106.0 C", StringComparison.Ordinal), "Combined answer should include telemetry as telemetry evidence.");
    Assert(answer.Content.Contains("Long loaded corners", StringComparison.Ordinal), "Combined answer should keep guide content in stored notes section.");
}

static void CoachSaysExternalResearchUnavailableOffline()
{
    var coach = new CoachEngine();
    var answer = coach.Answer(new SessionState(), "Tell me about this track", new CoachContext());

    Assert(answer.Content.Contains("External research:", StringComparison.Ordinal), "Offline knowledge answer should label external research.");
    Assert(answer.Content.Contains("unavailable", StringComparison.OrdinalIgnoreCase), "Offline knowledge answer should say external research is unavailable.");
}

static void ReplayMatchesLiveParserPath()
{
    var path = CaptureFileWithPackets([
        ValidRaw("""{"lap_progress":0.99,"fuel":10.0,"brake":0.9,"speed_kmh":140}"""),
        ValidRaw("""{"lap_progress":0.01,"lap_time_s":90.0,"fuel":8.0}""")
    ]);

    var replay = new PacketReplayTool().Replay(path);
    var engine = new EventEngine();
    var session = new SessionState();
    foreach (var raw in new[] { ValidRaw("""{"lap_progress":0.99,"fuel":10.0,"brake":0.9,"speed_kmh":140}"""), ValidRaw("""{"lap_progress":0.01,"lap_time_s":90.0,"fuel":8.0}""") })
    {
        var snapshot = SimHubPacketParser.Parse(raw);
        session.ApplySnapshot(snapshot, engine.Process(snapshot));
    }

    Assert(replay.SnapshotsProduced == 2, "Replay should produce same valid snapshot count.");
    Assert(replay.EventsProduced == session.RecentEvents.Count, "Replay should produce same event count as direct parser path.");
    Assert(replay.Session.CompletedLaps.Count == session.CompletedLaps.Count, "Replay should produce same completed lap count.");
}

static void DiagnosticsCountValidInvalidRates()
{
    var diagnostics = new TelemetryDiagnostics();
    var now = DateTimeOffset.UtcNow;
    diagnostics.Record(PacketResult(now, true, ValidRaw("""{"speed_kmh":1}""")));
    diagnostics.Record(PacketResult(now.AddMilliseconds(100), false, """{"schema":"wrong","schema_version":1}""", "Unsupported schema"));

    Assert(diagnostics.PacketsReceived == 2, "Diagnostics should count all packets.");
    Assert(diagnostics.ValidPackets == 1, "Diagnostics should count valid packets.");
    Assert(diagnostics.InvalidPackets == 1, "Diagnostics should count invalid packets.");
    Assert(diagnostics.PacketsPerSecond == 2, "Diagnostics packet rate should count recent packets.");
    Assert(diagnostics.ValidPacketsPerSecond == 1, "Diagnostics valid rate should count recent valid packets.");
    Assert(diagnostics.InvalidPacketsPerSecond == 1, "Diagnostics invalid rate should count recent invalid packets.");
}

static void RawCaptureStoresParseResult()
{
    var path = Path.Combine(Path.GetTempPath(), $"race-engineer-capture-{Guid.NewGuid():N}.jsonl");
    var capture = new RawPacketCapture(path, maxPackets: 10);
    capture.SetEnabled(true);
    capture.Record(PacketResult(DateTimeOffset.UtcNow, false, """{"schema":"wrong","schema_version":1}""", "Unsupported schema"));

    var text = File.ReadAllText(path);
    Assert(text.Contains("\"valid\":false", StringComparison.Ordinal), "Capture should store parse validity.");
    Assert(text.Contains("Unsupported schema", StringComparison.Ordinal), "Capture should store parse warning.");
    Assert(text.Contains("raw_json", StringComparison.Ordinal), "Capture should store raw packet.");
}

static void ReplayDoesNotRequireUdpSocket()
{
    var path = CaptureFileWithPackets([ValidRaw("""{"speed_kmh":123.4}""")]);
    var result = new PacketReplayTool().Replay(path);

    Assert(result.ValidPackets == 1, "Replay should parse packets without UDP.");
    Assert(result.Session.LatestSnapshot?.Car.SpeedKmh == 123.4, "Replay should feed parsed snapshots into session state.");
}

static async Task StoragePersistsSessionFactsAndExports()
{
    var dbPath = Path.Combine(Path.GetTempPath(), $"race-engineer-{Guid.NewGuid():N}.sqlite3");
    var storage = new StorageService(dbPath);
    var sessionId = Guid.NewGuid();
    var startedAt = DateTimeOffset.UtcNow.AddMinutes(-5);
    await storage.InitializeAsync();
    await storage.CreateSessionAsync(sessionId, startedAt, car: "Test Car", track: "Test Track");

    var engine = new EventEngine();
    var session = new SessionState();
    var first = Packet("""{"lap_progress":0.99,"fuel":10.0,"brake":0.9,"speed_kmh":140}""");
    var second = Packet("""{"lap_progress":0.01,"lap_time_s":90.0,"fuel":8.0}""");
    var firstEvents = engine.Process(first);
    session.ApplySnapshot(first, firstEvents);
    await storage.SaveSnapshotAsync(sessionId, first);
    await storage.SaveEventsAsync(sessionId, firstEvents);
    var secondEvents = engine.Process(second);
    var lapsBefore = session.CompletedLaps.Count;
    session.ApplySnapshot(second, secondEvents);
    await storage.SaveSnapshotAsync(sessionId, second);
    await storage.SaveEventsAsync(sessionId, secondEvents);
    foreach (var lap in session.CompletedLaps.Skip(lapsBefore))
    {
        await storage.SaveCompletedLapAsync(sessionId, lap);
    }

    await storage.AddNoteAsync(sessionId, "race_prep", "Brake earlier into T1.");
    await storage.SavePostSessionSummaryAsync(sessionId, "## Summary\nGood test.", new { ok = true });
    await storage.EndSessionAsync(sessionId, DateTimeOffset.UtcNow, "## Summary\nGood test.", new { ok = true });

    var sessions = await storage.ListSessionsAsync();
    var row = sessions.Single(item => item.SessionId == sessionId);
    Assert(row.Car == "Test Car", "Session browser should include car metadata.");
    Assert(row.Track == "Test Track", "Session browser should include track metadata.");
    Assert(row.BestLap == TimeSpan.FromSeconds(90), "Session browser should include best lap.");
    Assert(row.EventCount >= 3, "Session browser should include deterministic event count.");

    var summary = await storage.LoadSessionSummaryMarkdownAsync(sessionId);
    Assert(summary.Contains("Good test.", StringComparison.Ordinal), "Stored summary should load.");
    Assert(File.Exists(await storage.ExportSessionJsonAsync(sessionId)), "Session JSON export should be created.");
    Assert(File.Exists(await storage.ExportCoachingMarkdownAsync(sessionId)), "Coaching Markdown export should be created.");
}

static void AnalyticsComputesDeterministicMetrics()
{
    var (session, _) = SessionWithFuelEstimate();
    var analytics = new TelemetryAnalyticsService();
    var input = new SessionAnalyticsInput(session);
    var first = analytics.Analyze(input);
    var second = analytics.Analyze(input);

    Assert(first.LapConsistency.Score0To100 == second.LapConsistency.Score0To100, "Lap consistency should be deterministic.");
    Assert(first.BestVsAverage.BestLapSeconds == 89.0, "Best lap should match shortest completed lap.");
    Assert(first.BestVsAverage.AverageLapSeconds == 89.5, "Average lap should match completed lap average.");
    Assert(first.FuelTrend.FuelPerLap is > 0, "Fuel per lap should be computed.");
    Assert(first.DriverProfile.Strengths.Count + first.DriverProfile.Weaknesses.Count > 0, "Driver profile should classify metrics.");
}

static void LapIntelligenceComputesDeterministicInsights()
{
    var (session, _) = SessionWithFuelEstimate();
    var snapshots = BuildLapIntelligenceSnapshots(session);
    var service = new LapIntelligenceService();
    var input = new LapIntelligenceInput(session, snapshots);
    var first = service.Analyze(input);
    var second = service.Analyze(input);

    Assert(first.LapComparison.BestLapSeconds == second.LapComparison.BestLapSeconds, "Lap comparison should be deterministic.");
    Assert(first.LapComparison.BestLapSeconds == 89.0, "Best lap should be the fastest completed lap.");
    Assert(first.PaceDecay.Availability == "Available", "Pace decay should be computed from completed laps.");
    Assert(first.TheoreticalBest.TheoreticalSeconds is { } theoretical && first.TheoreticalBest.ActualBestSeconds is { } actualBest && theoretical <= actualBest, "Theoretical best should not exceed actual best.");
    Assert(first.ConsistencyHeatmap.Cells.Count > 0, "Heatmap cells should be generated from sector timing.");
    Assert(first.CoachingInsights.Count > 0, "Coaching insights should be generated.");
}

static IReadOnlyList<TelemetrySnapshot> BuildLapIntelligenceSnapshots(SessionState session)
{
    var snapshots = new List<TelemetrySnapshot>();
    var baseTime = session.StartedAt;
    foreach (var lap in session.CompletedLaps.OrderBy(item => item.LapNumber))
    {
        var progressPoints = new[] { 0.05, 0.20, 0.40, 0.55, 0.70, 0.90 };
        for (var index = 0; index < progressPoints.Length; index++)
        {
            var elapsed = (lap.StartedAt - baseTime).TotalSeconds + (progressPoints[index] * (lap.Duration?.TotalSeconds ?? 90));
            var snapshot = Packet(
                $$"""{"lap_progress":{{progressPoints[index].ToString(CultureInfo.InvariantCulture)}},"fuel":{{(8.0 - lap.LapNumber).ToString(CultureInfo.InvariantCulture)}},"brake":{{(progressPoints[index] < 0.25 ? 0.6 : 0.05).ToString(CultureInfo.InvariantCulture)}},"throttle":{{(progressPoints[index] > 0.65 ? 0.7 : 0.2).ToString(CultureInfo.InvariantCulture)}}}""");
            snapshots.Add(snapshot with
            {
                Timestamp = baseTime.AddSeconds(elapsed),
                Lap = snapshot.Lap with { LapNumber = lap.LapNumber, LapProgress = progressPoints[index] }
            });
        }
    }

    return snapshots;
}

static void CoachEvidenceBuilderCreatesDeterministicPackets()
{
    var (session, _) = SessionWithFuelEstimate();
    var snapshots = BuildLapIntelligenceSnapshots(session);
    var builder = new CoachEvidenceBuilder();
    var input = new CoachEvidenceInput(session, snapshots, session.Events);
    var first = builder.Build(input);
    var second = builder.Build(input);

    Assert(first.Packets.Count == second.Packets.Count, "Evidence packets should be deterministic.");
    Assert(first.Packets.Count > 0, "Evidence builder should produce packets.");
    Assert(first.Select(CoachEvidenceTopic.Fuel).Count > 0, "Fuel topic evidence should be available.");
    var answer = new CoachEngine().Answer(session, "where am I losing time?", null, first);
    Assert(answer.EvidencePackets.Count > 0, "Coach answer should attach structured evidence packets.");
}

static CoachEvidenceBundle BuildSampleCoachEvidence(SessionState session)
{
    var snapshots = BuildLapIntelligenceSnapshots(session);
    var analytics = new TelemetryAnalyticsService().Analyze(new SessionAnalyticsInput(session, snapshots));
    var lapIntelligence = new LapIntelligenceService().Analyze(new LapIntelligenceInput(session, snapshots));
    var driverPerformance = new DriverPerformanceIntelligenceService().Analyze(new DriverPerformanceInput(
        session,
        snapshots,
        session.Events,
        analytics,
        lapIntelligence));
    var strategy = new StrategyEngine().Analyze(new StrategyInput(session, analytics));
    return new CoachEvidenceBuilder().Build(new CoachEvidenceInput(
        session,
        snapshots,
        session.Events,
        analytics,
        lapIntelligence,
        null,
        driverPerformance,
        strategy));
}

static (SessionState Session, SessionDriverPerformance Performance, CoachContext Context) SessionWithPerformanceContext()
{
    var (session, _) = SessionWithFuelEstimate();
    var snapshots = BuildLapIntelligenceSnapshots(session);
    var analytics = new TelemetryAnalyticsService().Analyze(new SessionAnalyticsInput(session, snapshots));
    var lapIntelligence = new LapIntelligenceService().Analyze(new LapIntelligenceInput(session, snapshots));
    var selectedLapNumber = session.CompletedLaps
        .Where(lap => lap.IsValid && lap.Duration.HasValue)
        .OrderByDescending(lap => lap.Duration!.Value)
        .First()
        .LapNumber;
    var performance = new DriverPerformanceIntelligenceService().Analyze(new DriverPerformanceInput(
        session,
        snapshots,
        session.Events,
        analytics,
        lapIntelligence,
        SelectedLapNumber: selectedLapNumber));
    var context = new CoachContext(
        Analytics: analytics,
        DriverPerformance: performance,
        RecentSnapshots: snapshots);
    return (session, performance, context);
}

static void HybridCoachFallsBackWhenAiDisabled()
{
    var deterministic = new CoachEngine();
    var hybrid = new HybridCoachEngine(
        DisabledEngineerAiProvider.Instance,
        new EngineerAiOptions(false, "disabled", "", "", 40, 3));
    var (session, _) = SessionWithFuelEstimate();
    var evidence = BuildSampleCoachEvidence(session);

    var expected = deterministic.Answer(session, "gdje gubim vrijeme", evidence: evidence);
    var actual = hybrid.Answer(session, "gdje gubim vrijeme", evidence: evidence);

    Assert(expected.Content == actual.Content, "Disabled AI should fall back to deterministic CoachEngine.");
}

static void HybridCoachMockAnswersFromEvidence()
{
    var hybrid = new HybridCoachEngine(
        new MockEngineerAiProvider(),
        new EngineerAiOptions(true, "mock", "", "", 40, 3));
    var (session, _) = SessionWithFuelEstimate();
    var evidence = BuildSampleCoachEvidence(session);

    var answer = hybrid.Answer(session, "where am I losing time?", evidence: evidence);

    Assert(
        answer.Uncertainty?.Contains("telemetry", StringComparison.OrdinalIgnoreCase) == true,
        "Mock AI answer should report telemetry-based uncertainty.");
    Assert(answer.EvidencePackets.Count > 0, "Mock AI answer should attach evidence packets.");
    Assert(
        answer.Content.Contains("time loss", StringComparison.OrdinalIgnoreCase)
            || answer.Content.Contains("gubitak", StringComparison.OrdinalIgnoreCase),
        "Mock AI answer should reference losing-time evidence.");
}

static void HybridCoachFallsBackWhenNoEvidence()
{
    var hybrid = new HybridCoachEngine(
        new MockEngineerAiProvider(),
        new EngineerAiOptions(true, "mock", "", "", 40, 3));
    var (session, _) = SessionWithFuelEstimate();
    var expected = new CoachEngine().Answer(session, "fuel status");
    var actual = hybrid.Answer(session, "fuel status");

    Assert(expected.Content == actual.Content, "Missing evidence should fall back to deterministic CoachEngine.");
}

static void HybridCoachMockAnswersCroatianQuestions()
{
    var hybrid = new HybridCoachEngine(
        new MockEngineerAiProvider(),
        new EngineerAiOptions(true, "mock", "", "", 40, 3));
    var (session, _) = SessionWithFuelEstimate();
    var evidence = BuildSampleCoachEvidence(session);

    var losingTime = hybrid.Answer(session, "gdje gubim vrijeme", evidence: evidence);
    var braking = hybrid.Answer(session, "kako kočim", evidence: evidence);
    var fuel = hybrid.Answer(session, "imam li dovoljno goriva", evidence: evidence);

    Assert(
        losingTime.Content.Contains("gubitak", StringComparison.OrdinalIgnoreCase)
            || losingTime.EvidencePackets.Count > 0,
        "Croatian losing-time question should use telemetry evidence.");
    Assert(
        braking.Content.Contains("Kočenje", StringComparison.OrdinalIgnoreCase)
            || braking.Content.Contains("Braking", StringComparison.OrdinalIgnoreCase)
            || braking.EvidencePackets.Count > 0,
        "Croatian braking question should use telemetry evidence.");
    Assert(
        fuel.Content.Contains("goriv", StringComparison.OrdinalIgnoreCase)
            || fuel.Content.Contains("fuel", StringComparison.OrdinalIgnoreCase)
            || fuel.EvidencePackets.Count > 0,
        "Croatian fuel question should use telemetry evidence.");
}

static void HybridCoachFallsBackOnAiTimeout()
{
    var hybrid = new HybridCoachEngine(
        new SlowMockEngineerAiProvider(),
        new EngineerAiOptions(true, "mock", "", "", 40, 1));
    var (session, _) = SessionWithFuelEstimate();
    var evidence = BuildSampleCoachEvidence(session);
    var expected = new CoachEngine().Answer(session, "where am I losing time?", evidence: evidence);
    var actual = hybrid.Answer(session, "where am I losing time?", evidence: evidence);

    Assert(expected.Content == actual.Content, "Timed-out AI should fall back to deterministic CoachEngine.");
    Assert(
        AiEngineerRuntime.LastDiagnostic?.TimedOut == true,
        "Timed-out AI should record timeout diagnostic.");
    Assert(
        AiEngineerStatusResolver.ResolveLabel(AppSettings.Default with { AiEngineerEnabled = true, AiProvider = "mock" }, AiEngineerRuntime.LastDiagnostic) == "Timeout",
        "Status line should show Timeout after AI timeout.");
}

static void OpenAiCompatibleProviderGeneratesAnswerWithMockHttp()
{
    var previous = Environment.GetEnvironmentVariable(OpenAiCompatibleEngineerAiProvider.ApiKeyEnvironmentVariable);
    try
    {
        Environment.SetEnvironmentVariable(OpenAiCompatibleEngineerAiProvider.ApiKeyEnvironmentVariable, "test-key");
        using var provider = new OpenAiCompatibleEngineerAiProvider(
            "https://example.test/v1/chat/completions",
            "gpt-4o-mini",
            3,
            new HttpClient(new MockOpenAiHttpHandler()));
        var context = new EngineerAiContext(
            "how is my braking",
            "Live",
            "High",
            true,
            2,
            null,
            [new EngineerAiFact("Braking", "Brake stability", "Stable entry pressure", 0.9, "Analytics", 2)],
            ["Use only the provided facts."]);
        var result = provider
            .GenerateAnswerAsync(new EngineerAiRequest("how is my braking", context, 40), CancellationToken.None)
            .GetAwaiter()
            .GetResult();

        Assert(result.Success, $"OpenAI-compatible provider should succeed with mock HTTP: {result.FailureReason}");
        Assert(
            result.Answer?.Contains("Braking looks stable", StringComparison.OrdinalIgnoreCase) == true,
            "OpenAI-compatible provider should return validated mock content.");
        Assert(result.LatencyMs is >= 0, "OpenAI-compatible provider should record latency.");
    }
    finally
    {
        Environment.SetEnvironmentVariable(OpenAiCompatibleEngineerAiProvider.ApiKeyEnvironmentVariable, previous);
    }
}

static void AiEngineerStatusResolverReportsConfiguredStates()
{
    var disabled = AppSettings.Default with { AiEngineerEnabled = false, AiProvider = "openai" };
    Assert(
        AiEngineerStatusResolver.ResolveLabel(disabled) == "Disabled",
        "Disabled AI should report Disabled status.");

    var mock = AppSettings.Default with { AiEngineerEnabled = true, AiProvider = "mock" };
    Assert(
        AiEngineerStatusResolver.ResolveLabel(mock) == "Mock",
        "Mock provider should report Mock status.");

    var openAiMissingKey = AppSettings.Default with { AiEngineerEnabled = true, AiProvider = "openai" };
    var previous = Environment.GetEnvironmentVariable(OpenAiCompatibleEngineerAiProvider.ApiKeyEnvironmentVariable);
    try
    {
        Environment.SetEnvironmentVariable(OpenAiCompatibleEngineerAiProvider.ApiKeyEnvironmentVariable, null);
        Assert(
            AiEngineerStatusResolver.ResolveLabel(openAiMissingKey) == "Missing API key",
            "OpenAI without API key should report Missing API key.");

        Environment.SetEnvironmentVariable(OpenAiCompatibleEngineerAiProvider.ApiKeyEnvironmentVariable, "test-key");
        Assert(
            AiEngineerStatusResolver.ResolveLabel(openAiMissingKey) == "OpenAI ready",
            "OpenAI with API key should report OpenAI ready.");

        var fallbackDiagnostic = new EngineerAiDiagnosticTrace("openai", "gpt-4o-mini", 3, "No facts", 120, false, true, false);
        Assert(
            AiEngineerStatusResolver.ResolveLabel(openAiMissingKey, fallbackDiagnostic) == "Fallback",
            "Last fallback should surface Fallback status.");
    }
    finally
    {
        Environment.SetEnvironmentVariable(OpenAiCompatibleEngineerAiProvider.ApiKeyEnvironmentVariable, previous);
    }
}

static void EngineerAiProviderFactorySelectsOpenAiProvider()
{
    var provider = EngineerAiProviderFactory.Create(AppSettings.Default with
    {
        AiEngineerEnabled = true,
        AiProvider = "openai",
        AiModel = "gpt-4o-mini",
        AiTimeoutSeconds = 4
    });

    Assert(provider.Name == "openai", "Factory should create OpenAI-compatible provider.");
    var openAi = AssertIs<OpenAiCompatibleEngineerAiProvider>(provider);
    Assert(openAi.ResolvedModel == "gpt-4o-mini", "Factory should pass configured model.");
    Assert(openAi.TimeoutSeconds == 4, "Factory should pass configured timeout.");
}

static void HybridCoachMockAnswersDirectDriverQuestions()
{
    var hybrid = new HybridCoachEngine(
        new MockEngineerAiProvider(),
        new EngineerAiOptions(true, "mock", "", "", 40, 3));
    var session = new SessionState();
    session.ApplySnapshot(
        SimHubPacketParser.Parse(
            """{"schema":"acevo_engineer.simhub_datacore","schema_version":1,"fuel":18.5,"speed_kmh":180,"provider_diag":{"pm_last_track_id":"Monza-GP","pm_last_car_id":"Ferrari F2004"},"tyre_temp_c":[88,91,90,89]}"""),
        []);
    var raceContext = RaceContextService.Build(session, session.LatestSnapshot);
    var evidence = new CoachEvidenceBuilder().Build(new CoachEvidenceInput(session, [session.LatestSnapshot!], [], null, null, null, null, null, null, null, raceContext));

    var tyre = hybrid.Answer(session, "kakve su gume", new CoachContext(RaceContext: raceContext), evidence);
    var fuel = hybrid.Answer(session, "koliko goriva imam", new CoachContext(RaceContext: raceContext), evidence);
    var track = hybrid.Answer(session, "which track am i on", new CoachContext(RaceContext: raceContext), evidence);

    Assert(
        tyre.Content.Contains("Tyres from telemetry", StringComparison.OrdinalIgnoreCase)
            || tyre.Content.Contains("Front-left", StringComparison.OrdinalIgnoreCase)
            || tyre.EvidencePackets.Count > 0,
        "Tyre question should use AI/mock answer from structured evidence.");
    Assert(
        fuel.Content.Contains("liters", StringComparison.OrdinalIgnoreCase)
            || fuel.Content.Contains("goriv", StringComparison.OrdinalIgnoreCase),
        "Fuel question should use AI/mock answer from structured evidence.");
    Assert(
        track.Content.Contains("You are on Monza-GP", StringComparison.OrdinalIgnoreCase),
        "Track identity question should use AI/mock answer from structured evidence.");
    Assert(
        AiEngineerRuntime.LastDiagnostic?.UsedAi == true,
        "Direct driver question should record successful AI usage.");
}

static void HybridCoachLiveCalloutsStayDeterministic()
{
    var hybrid = new HybridCoachEngine(
        new MockEngineerAiProvider(),
        new EngineerAiOptions(true, "mock", "", "", 40, 3));
    var deterministic = new CoachEngine();
    var engine = new EventEngine();
    var session = new SessionState();
    Apply(session, engine, Packet("""{"speed_kmh":120,"lap_progress":0.4,"brake":0.9}"""));
    Apply(session, engine, Packet("""{"speed_kmh":138,"lap_progress":0.41,"brake":0.2}"""));
    var events = session.RecentEvents.ToArray();
    var context = SessionContextAssessment.InitialLive;

    var hybridCallout = hybrid.ChooseLiveCallout(events, context);
    var deterministicCallout = deterministic.ChooseLiveCallout(events, context);

    Assert(
        (hybridCallout?.Content ?? "") == (deterministicCallout?.Content ?? ""),
        "Automatic callouts must stay deterministic even when AI is enabled.");
}

static T AssertIs<T>(object value) where T : class
{
    Assert(value is T, $"Expected instance of {typeof(T).Name}.");
    return (T)value;
}

static void StrictTopicRoutingTyreQuestionDoesNotReturnFuel()
{
    var coach = new CoachEngine();
    var hybrid = new HybridCoachEngine(
        new MockEngineerAiProvider(),
        new EngineerAiOptions(true, "mock", "", "", 40, 3));
    var (session, _) = SessionWithFuelEstimate();
    var evidence = BuildSampleCoachEvidence(session);

    var deterministic = coach.Answer(session, "kakve su gume", evidence: evidence);
    var hybridAnswer = hybrid.Answer(session, "kakve su gume", evidence: evidence);

    Assert(!LooksLikeFuelAnswer(deterministic.Content), "Tyre question should not return a fuel-only deterministic answer.");
    Assert(!LooksLikeFuelAnswer(hybridAnswer.Content), "Tyre question should not return a fuel-only hybrid answer.");
}

static void StrictTopicRoutingPositionQuestionDoesNotReturnFuel()
{
    var coach = new CoachEngine();
    var (session, _) = SessionWithFuelEstimate();
    var evidence = BuildSampleCoachEvidence(session);

    var answer = coach.Answer(session, "koja mi je pozicija", evidence: evidence);

    Assert(!LooksLikeFuelAnswer(answer.Content), "Position question should not return a fuel answer.");
    Assert(
        answer.Content.Contains("Position data is unavailable", StringComparison.OrdinalIgnoreCase)
            || answer.Content.Contains("You are P", StringComparison.OrdinalIgnoreCase),
        "Position question should answer position or say unavailable.");
}

static void StrictTopicRoutingLapTimeQuestionDoesNotReturnFuel()
{
    var coach = new CoachEngine();
    var hybrid = new HybridCoachEngine(
        new MockEngineerAiProvider(),
        new EngineerAiOptions(true, "mock", "", "", 40, 3));
    var (session, _) = SessionWithFuelEstimate();
    var evidence = BuildSampleCoachEvidence(session);

    var deterministic = coach.Answer(session, "koliko mi je vrijeme kruga", evidence: evidence);
    var hybridAnswer = hybrid.Answer(session, "koliko mi je vrijeme kruga", evidence: evidence);

    Assert(!LooksLikeFuelAnswer(deterministic.Content), "Lap time question should not return a fuel answer.");
    Assert(!LooksLikeFuelAnswer(hybridAnswer.Content), "Hybrid lap time question should not return a fuel answer.");
    Assert(
        deterministic.Content.Contains("lap", StringComparison.OrdinalIgnoreCase)
            || deterministic.Content.Contains("No valid lap time yet", StringComparison.OrdinalIgnoreCase),
        "Lap time question should reference lap time or unavailable state.");
}

static void FuelQuestionStillReturnsFuel()
{
    var coach = new CoachEngine();
    var (session, _) = SessionWithFuelEstimate();
    var evidence = BuildSampleCoachEvidence(session);

    var english = coach.Answer(session, "how much fuel do i have", evidence: evidence);
    var croatian = coach.Answer(session, "koliko goriva imam", evidence: evidence);

    Assert(LooksLikeFuelAnswer(english.Content), "English fuel question should return fuel.");
    Assert(LooksLikeFuelAnswer(croatian.Content), "Croatian fuel question should return fuel.");
}

static void CroatianInputProducesEnglishSpokenSummary()
{
    var output = new RecordingVoiceOutput();
    var voice = new VoiceService(output);
    voice.SetVoiceEnabled(true);
    voice.SetMuted(false);
    var hybrid = new HybridCoachEngine(
        new MockEngineerAiProvider(),
        new EngineerAiOptions(true, "mock", "", "", 40, 3));
    var (session, _) = SessionWithFuelEstimate();
    var evidence = BuildSampleCoachEvidence(session);
    var preferences = new CoachPreferencesRecord(CoachResponseLanguage: "auto");

    var result = voice.HandleSpokenQuery(
        session,
        "kakve su gume",
        hybrid,
        evidence: evidence,
        preferences: preferences);

    Assert(CoachSpokenLanguageResolver.UseEnglishSpokenOutput(preferences), "Auto coach response language should prefer English speech.");
    Assert(result.Spoken, "Croatian tyre question should still produce spoken output.");
    Assert(!result.GeneratedSummary.Contains("goriv", StringComparison.OrdinalIgnoreCase), "Spoken summary should not use Croatian fuel wording.");
    Assert(
        result.GeneratedSummary.Contains("Tyre", StringComparison.OrdinalIgnoreCase)
            || result.GeneratedSummary.Contains("reliable", StringComparison.OrdinalIgnoreCase)
            || result.GeneratedSummary.Contains("temp", StringComparison.OrdinalIgnoreCase),
        "Spoken summary should use English tyre wording.");
}

static void TyreIntelligenceCoachesColdTyreWarmup()
{
    var coach = new CoachEngine();
    var session = new SessionState();
    session.ApplySnapshot(Packet("""{"lap_progress":0.12,"speed_kmh":95,"tyre_temp_c":[52,54,50,51]}"""), []);
    var intelligence = new TyreIntelligenceService().Analyze(new TyreIntelligenceInput(
        session,
        [session.LatestSnapshot!],
        [],
        VehicleActivity.OutLap,
        SessionPhase.Race));
    var answer = coach.Answer(
        session,
        "kakve su gume",
        new CoachContext(TyreIntelligence: intelligence));

    Assert(intelligence.WarmupState == TyreWarmupState.Cold, "Cold tyre temps should classify as cold.");
    Assert(
        answer.Content.Contains("cold", StringComparison.OrdinalIgnoreCase)
            || answer.Content.Contains("half lap", StringComparison.OrdinalIgnoreCase)
            || answer.Content.Contains("corners", StringComparison.OrdinalIgnoreCase),
        "Cold tyre question should explain warmup readiness.");
    Assert(!LooksLikeFuelAnswer(answer.Content), "Tyre warmup answer must not mention fuel.");
}

static void TraceProgressSurvivesLapWrapBleed()
{
    var builder = new TelemetryTraceBuilder();
    var session = new SessionState();
    var snapshots = new List<TelemetrySnapshot>();
    for (var progress = 0.88; progress <= 0.99; progress += 0.03)
    {
        snapshots.Add(StampTraceSnapshot(progress, 5));
    }

    snapshots.Add(StampTraceSnapshot(0.02, 6));
    snapshots.Add(StampTraceSnapshot(0.08, 6));

    var timeline = builder.Build(new TelemetryTimelineInput(session, snapshots, [], 6, 0.08));
    var throttle = timeline.Rows.FirstOrDefault(row => row.Name == "Throttle");

    Assert(throttle is not null, "Trace should remain available through lap wrap.");
    Assert(
        throttle!.Series.First().Points.Any(point => point.Progress >= 0.85),
        "Trace should preserve late-lap progress before wrap.");
    Assert(
        throttle.Series.First().Points.Any(point => point.Progress <= 0.15),
        "Trace should preserve early progress after wrap without clearing the lap.");
}

static void FuelAmountQuestionLeadsWithLiters()
{
    var coach = new CoachEngine();
    var (session, _) = SessionWithFuelEstimate();
    var answer = coach.Answer(session, "koliko goriva imam");

    Assert(
        answer.Content.StartsWith("You have", StringComparison.Ordinal)
            && answer.Content.Contains("liters.", StringComparison.Ordinal),
        "Fuel amount question should start with current liters.");
    Assert(
        !answer.Content.StartsWith("Fuel use", StringComparison.Ordinal)
            && !answer.Content.StartsWith("About", StringComparison.Ordinal),
        "Fuel amount question should not lead with consumption or laps remaining.");
}

static void FuelConsumptionQuestionReportsPerLap()
{
    var coach = new CoachEngine();
    var (session, _) = SessionWithFuelEstimate();
    var answer = coach.Answer(session, "potrošnja goriva");

    Assert(
        answer.Content.Contains("L/lap", StringComparison.Ordinal),
        "Fuel consumption question should report L/lap.");
    Assert(
        answer.Content.StartsWith("Fuel use", StringComparison.Ordinal),
        "Fuel consumption question should lead with consumption, not tank level.");
}

static void FuelStrategyQuestionReportsRiskAndLapsRemaining()
{
    var coach = new CoachEngine();
    var (session, _) = SessionWithFuelEstimate();
    var evidence = BuildSampleCoachEvidence(session);
    var answer = coach.Answer(session, "imam li dovoljno goriva", evidence: evidence);

    Assert(
        answer.Content.Contains("laps remaining", StringComparison.OrdinalIgnoreCase)
            || answer.Content.Contains("fuel risk", StringComparison.OrdinalIgnoreCase)
            || answer.Content.Contains("Yes,", StringComparison.OrdinalIgnoreCase)
            || answer.Content.Contains("No,", StringComparison.OrdinalIgnoreCase)
            || answer.Content.Contains("Maybe,", StringComparison.OrdinalIgnoreCase),
        "Fuel strategy question should report finish verdict and/or laps remaining.");
    Assert(
        !answer.Content.StartsWith("Fuel use", StringComparison.Ordinal),
        "Fuel strategy question should not lead with consumption.");
    Assert(
        !answer.Content.Contains("braking", StringComparison.OrdinalIgnoreCase),
        "Fuel strategy question should not mention braking.");
}

static void PushConfidenceDistinctFromTyreQuestion()
{
    var coach = new CoachEngine();
    var session = new SessionState();
    session.ApplySnapshot(Packet("""{"speed_kmh":140,"lap_progress":0.35,"throttle":0.6,"tyre_temp_c":[88,89,87,86]}"""), []);
    var context = new CoachContext(
        SessionContext: new SessionContextAssessment(
            SessionPhase.Practice,
            VehicleActivity.OnTrack,
            "Practice / On track",
            StrategyConfidenceLevel.Medium,
            "Medium",
            true,
            true,
            true,
            true,
            true,
            true,
            true,
            "Stable samples."),
        TyreIntelligence: new TyreIntelligenceService().Analyze(new TyreIntelligenceInput(session)));

    var tyre = coach.Answer(session, "kakve su gume", context);
    var push = coach.Answer(session, "can i push", context);

    Assert(
        tyre.Content.Contains("temp", StringComparison.OrdinalIgnoreCase)
            || tyre.Content.Contains("Front", StringComparison.OrdinalIgnoreCase),
        "Tyre question should focus on tyre condition.");
    Assert(
        push.Content.Contains("grip", StringComparison.OrdinalIgnoreCase)
            || push.Content.Contains("stable", StringComparison.OrdinalIgnoreCase)
            || push.Content.Contains("push", StringComparison.OrdinalIgnoreCase),
        "Push question should focus on grip/confidence.");
    Assert(
        !string.Equals(NormalizeCoachText(tyre.Content), NormalizeCoachText(push.Content), StringComparison.Ordinal),
        "Tyre and push answers should not be identical templates.");
}

static void FuelStrategyDoesNotMentionBraking()
{
    var coach = new CoachEngine();
    var (session, _) = SessionWithFuelEstimate();
    var evidence = BuildSampleCoachEvidence(session);
    var answer = coach.Answer(session, "can i finish", evidence: evidence);

    Assert(
        !answer.Content.Contains("brake", StringComparison.OrdinalIgnoreCase),
        "Can I finish should never mention braking.");
    Assert(
        !answer.Content.Contains("throttle", StringComparison.OrdinalIgnoreCase),
        "Can I finish should never mention throttle.");
    Assert(
        answer.Content.Contains("fuel", StringComparison.OrdinalIgnoreCase)
            || answer.Content.Contains("laps remaining", StringComparison.OrdinalIgnoreCase)
            || answer.Content.Contains("Yes,", StringComparison.OrdinalIgnoreCase)
            || answer.Content.Contains("No,", StringComparison.OrdinalIgnoreCase),
        "Can I finish should stay in fuel strategy scope.");
}

static void ThrottleUsesLiveTraceWithoutCompletedLap()
{
    var coach = new CoachEngine();
    var engine = new EventEngine();
    var session = new SessionState();
    var snapshots = new List<TelemetrySnapshot>();
    for (var index = 0; index < 8; index++)
    {
        var throttle = index % 2 == 0 ? "0.25" : "0.65";
        Apply(session, engine, Packet("""{"speed_kmh":120,"lap_progress":0.45,"throttle":""" + throttle + ""","steering":0.05}"""));
        if (session.LatestSnapshot is not null)
        {
            snapshots.Add(session.LatestSnapshot);
        }
    }

    engine.Process(Packet("""{"speed_kmh":120,"lap_progress":0.46,"throttle":0.18,"steering":0.04}"""));
    engine.Process(Packet("""{"speed_kmh":121,"lap_progress":0.47,"throttle":0.22,"steering":0.03}"""));
    engine.Process(Packet("""{"speed_kmh":122,"lap_progress":0.48,"throttle":0.20,"steering":0.02}"""));

    var context = new CoachContext(
        SessionContext: new SessionContextAssessment(
            SessionPhase.Practice,
            VehicleActivity.OnTrack,
            "Practice / On track",
            StrategyConfidenceLevel.Low,
            "Low",
            false,
            false,
            false,
            false,
            false,
            false,
            false,
            "No completed laps yet."),
        RecentSnapshots: snapshots);

    var answer = coach.Answer(session, "how is my throttle", context);
    Assert(
        !answer.Content.Contains("No throttle data yet", StringComparison.OrdinalIgnoreCase),
        "Throttle coaching should use live trace signals before a completed lap.");
    Assert(
        answer.Content.Contains("throttle", StringComparison.OrdinalIgnoreCase),
        "Throttle answer should discuss throttle behavior.");
}

static string NormalizeCoachText(string content) =>
    content.Replace(" ", "", StringComparison.Ordinal).ToLowerInvariant();

static void RaceAwarenessPacketParsesPartialFields()
{
    var parsed = SimHubPacketParser.TryParse(
        """
        {"schema":"acevo_engineer.simhub_datacore","schema_version":1,"track_name":"Monza","car_name":"GT3","session_type":"race","position":4,"total_cars":18,"lap_number":7,"speed_kmh":210}
        """,
        out var snapshot,
        out var warning);

    Assert(parsed, $"Race-awareness packet should parse: {warning}");
    Assert(snapshot!.RaceAwareness?.TrackName == "Monza", "Track name should parse from telemetry.");
    Assert(snapshot.RaceAwareness?.CarName == "GT3", "Car name should parse from telemetry.");
    Assert(snapshot.RaceAwareness?.Position == 4, "Position should parse from telemetry.");
    Assert(snapshot.RaceAwareness?.GapAheadSeconds is null, "Missing gap fields must remain null.");

    var diagnostics = RaceContextService.BuildTelemetryDiagnostics(snapshot);
    Assert(diagnostics.PresentFields.Contains("track_name"), "Diagnostics should list present track_name.");
    Assert(diagnostics.MissingFields.Contains("gap_ahead_s"), "Diagnostics should list missing gap_ahead_s.");
}

static void RaceContextServiceReportsMissingOpponentGaps()
{
    var session = new SessionState();
    session.ApplySnapshot(
        SimHubPacketParser.Parse(
            """{"schema":"acevo_engineer.simhub_datacore","schema_version":1,"track_name":"Monza","car_name":"GT3","session_type":"race","position":3,"total_cars":20,"speed_kmh":180}"""),
        []);
    var context = RaceContextService.Build(session, session.LatestSnapshot);
    Assert(context.Position == 3, "Race context should expose parsed position.");
    Assert(context.GapAheadSeconds is null, "Gap ahead must stay unavailable when telemetry omits it.");
    Assert(context.Diagnostics.MissingFields.Contains("gap_ahead_s"), "Missing opponent gap should be reported in diagnostics.");
}

static void OpponentTelemetryDiscoveryMapsCamelCaseFields()
{
    const string json =
        """{"schema":"acevo_engineer.simhub_datacore","schema_version":1,"speed_kmh":180,"position":4,"totalCars":20,"gapAhead":1.250,"gapBehind":2.500,"carAhead":"Pilot A","carBehind":"Pilot B"}""";
    var snapshot = SimHubPacketParser.Parse(json);
    var report = RaceContextService.BuildOpponentTelemetryDiagnostics(snapshot, json);

    Assert(snapshot.RaceAwareness?.GapAheadSeconds == 1.250, "gapAhead should map to GapAheadSeconds.");
    Assert(snapshot.RaceAwareness?.GapBehindSeconds == 2.500, "gapBehind should map to GapBehindSeconds.");
    Assert(snapshot.RaceAwareness?.CarAhead == "Pilot A", "carAhead should map to CarAhead.");
    Assert(snapshot.RaceAwareness?.CarBehind == "Pilot B", "carBehind should map to CarBehind.");
    Assert(snapshot.RaceAwareness?.TotalCars == 20, "totalCars should map to TotalCars.");
    Assert(report.PresentFields.Contains("gap_ahead_s"), "Discovery report should mark gap_ahead_s present.");
    Assert(report.ResolvedPropertyNames["gap_ahead_s"] == "gapAhead", "Discovery report should record resolved source path.");
}

static void OpponentTelemetryDiscoveryFindsUnmappedCandidates()
{
    const string json =
        """{"schema":"acevo_engineer.simhub_datacore","schema_version":1,"speed_kmh":180,"standings":[{"position":1,"driver":"A","gap":0.0}],"leaderboard":[{"name":"A","gapSeconds":0.0}]}""";
    var snapshot = SimHubPacketParser.Parse(json);
    var report = RaceContextService.BuildOpponentTelemetryDiagnostics(snapshot, json);

    Assert(snapshot.RaceAwareness?.GapAheadSeconds is null, "Nested standings/leaderboard must not invent gap values.");
    Assert(report.MissingFields.Contains("gap_ahead_s"), "Gap ahead should remain missing when only nested candidates exist.");
    Assert(report.CandidateRawFields.Any(candidate => candidate.JsonPath.Contains("standings", StringComparison.OrdinalIgnoreCase)), "Discovery should report standings candidate path.");
    Assert(report.CandidateRawFields.Any(candidate => candidate.JsonPath.Contains("leaderboard", StringComparison.OrdinalIgnoreCase)), "Discovery should report leaderboard candidate path.");
}

static void ProviderDiagOnlyPacketReportsOpponentGapsUnavailable()
{
    const string json =
        """{"schema":"acevo_engineer.simhub_datacore","schema_version":1,"speed_kmh":180,"provider_diag":{"pm_last_track_id":"Monza-GP","pm_last_car_id":"Ferrari F2004","pm_game_name":"Assetto Corsa"}}""";
    var snapshot = SimHubPacketParser.Parse(json);
    var report = RaceContextService.BuildOpponentTelemetryDiagnostics(snapshot, json);
    var context = RaceContextService.Build(new SessionState(), snapshot);

    Assert(context.GapAheadSeconds is null, "provider_diag-only packet must not invent gap ahead.");
    Assert(context.GapBehindSeconds is null, "provider_diag-only packet must not invent gap behind.");
    Assert(report.MissingFields.Contains("gap_ahead_s"), "Discovery should report gap_ahead_s missing for provider_diag-only packet.");
    Assert(report.MissingFields.Contains("gap_behind_s"), "Discovery should report gap_behind_s missing for provider_diag-only packet.");
}

static void ProviderDiagOnlyGapTrendSaysUnavailable()
{
    const string json =
        """{"schema":"acevo_engineer.simhub_datacore","schema_version":1,"speed_kmh":180,"provider_diag":{"pm_last_track_id":"Monza-GP","pm_last_car_id":"Ferrari F2004"}}""";
    var snapshot = SimHubPacketParser.Parse(json);
    var session = new SessionState();
    session.ApplySnapshot(snapshot, []);
    var raceContext = RaceContextService.Build(session, snapshot);
    var recommendation = OpponentIntelligenceService.Build(new OpponentIntelligenceInput(
        raceContext,
        [snapshot],
        null,
        null,
        null));

    Assert(
        recommendation.GapTrend.Summary.Contains("Gap trend is unavailable from telemetry.", StringComparison.OrdinalIgnoreCase),
        "Gap trend must stay unavailable when provider_diag-only packet has no gap fields.");
}

static void OpponentTelemetryCaptureReportReadsJsonl()
{
    var fixturePath = SampleThreeLapFixture.ResolvePath();
    var report = OpponentTelemetryCaptureReport.BuildReport(fixturePath);

    Assert(report.Contains("Opponent/gap telemetry report:", StringComparison.Ordinal), "Capture report should include header.");
    Assert(report.Contains("gap_ahead_s", StringComparison.Ordinal), "Fixture aggregate report should list missing gap_ahead_s.");
    Assert(report.Contains("Valid SimHub packets:", StringComparison.Ordinal), "Capture report should include valid packet count.");
}

static void SessionMemorySummaryBuildsFromFixtureLaps()
{
    var (session, _) = SessionWithFuelEstimate();
    Apply(session, new EventEngine(), Packet("""{"track_name":"Monza","car_name":"GT3","session_type":"Practice"}"""));
    var snapshots = BuildLapIntelligenceSnapshots(session);
    var analytics = new TelemetryAnalyticsService().Analyze(new SessionAnalyticsInput(session, snapshots));
    var lapIntelligence = new LapIntelligenceService().Analyze(new LapIntelligenceInput(session, snapshots));
    var driverPerformance = new DriverPerformanceIntelligenceService().Analyze(new DriverPerformanceInput(
        session,
        snapshots,
        session.Events,
        analytics,
        lapIntelligence));
    var strategy = new StrategyEngine().Analyze(new StrategyInput(session, analytics));
    var input = new SessionMemoryBuildInput(
        "Monza",
        "GT3",
        "Practice",
        session,
        analytics,
        lapIntelligence,
        null,
        strategy,
        driverPerformance);
    var first = SessionMemorySummaryBuilder.Build(input);
    var second = SessionMemorySummaryBuilder.Build(input);

    Assert(first.TrackName == second.TrackName && first.TrackName == "Monza", "Session memory summary track should be deterministic.");
    Assert(first.CarName == "GT3", "Session memory summary should record car name.");
    Assert(first.SessionType == "Practice", "Session memory summary should record session type.");
    Assert(first.BestLapSeconds == 89.0, "Session memory summary should record best lap from completed laps.");
    Assert(first.AverageCleanLapSeconds == 89.5, "Session memory summary should record average clean lap.");
    Assert(first.FuelUsedPerLap is > 0, "Session memory summary should record fuel use per lap when available.");
    Assert(first.OneLineSummary.Contains("Stored session data", StringComparison.OrdinalIgnoreCase), "One-line summary should label stored session data.");
}

static async Task SessionMemoryRetrievalByTrackCar()
{
    var dbPath = Path.Combine(Path.GetTempPath(), $"race-engineer-session-memory-{Guid.NewGuid():N}.sqlite3");
    var storage = new StorageService(dbPath);
    await storage.InitializeAsync();
    var service = new SessionMemoryService();
    var (session, _) = SessionWithFuelEstimate();
    var snapshots = BuildLapIntelligenceSnapshots(session);
    var analytics = new TelemetryAnalyticsService().Analyze(new SessionAnalyticsInput(session, snapshots));
    var input = new SessionMemoryBuildInput(
        "Monza",
        "GT3",
        "Practice",
        session,
        analytics,
        null,
        null,
        null,
        null);
    var summary = service.BuildSummary(input);
    await service.SaveSummaryAsync(storage, summary);

    var loaded = await service.LoadRecentAsync(storage, "Monza", "GT3");
    Assert(loaded.Count == 1, "Stored session memory should load for same track/car.");
    Assert(loaded[0].TrackName == "Monza" && loaded[0].CarName == "GT3", "Loaded session memory should preserve track/car.");
    Assert(loaded[0].BestLapSeconds == summary.BestLapSeconds, "Loaded session memory should preserve best lap.");
}

static void SessionDebriefContainsStrengthsWeaknessesActions()
{
    var (session, _) = SessionWithFuelEstimate();
    var snapshots = BuildLapIntelligenceSnapshots(session);
    var analytics = new TelemetryAnalyticsService().Analyze(new SessionAnalyticsInput(session, snapshots));
    var lapIntelligence = new LapIntelligenceService().Analyze(new LapIntelligenceInput(session, snapshots));
    var driverPerformance = new DriverPerformanceIntelligenceService().Analyze(new DriverPerformanceInput(
        session,
        snapshots,
        session.Events,
        analytics,
        lapIntelligence));
    var input = new SessionMemoryBuildInput(
        "Monza",
        "GT3",
        "Practice",
        session,
        analytics,
        lapIntelligence,
        null,
        null,
        driverPerformance);
    var summary = SessionMemorySummaryBuilder.Build(input);
    var debrief = SessionDebriefGenerator.Generate(summary, input);

    Assert(debrief.Strengths.Count > 0 || debrief.Weaknesses.Count > 0, "Debrief should include strengths or weaknesses.");
    Assert(!string.IsNullOrWhiteSpace(debrief.FuelAnalysis), "Debrief should include fuel analysis.");
    Assert(!string.IsNullOrWhiteSpace(debrief.TyreAnalysis), "Debrief should include tyre analysis.");
    Assert(!string.IsNullOrWhiteSpace(debrief.ConsistencyAnalysis), "Debrief should include consistency analysis.");
    Assert(debrief.NextSessionActions.Count > 0, "Debrief should include next-session actions.");
    Assert(debrief.Markdown.Contains("Strengths", StringComparison.OrdinalIgnoreCase), "Debrief markdown should include strengths section.");
    Assert(debrief.Markdown.Contains("Weaknesses", StringComparison.OrdinalIgnoreCase), "Debrief markdown should include weaknesses section.");
    Assert(debrief.Markdown.Contains("Top 3 next-session actions", StringComparison.OrdinalIgnoreCase), "Debrief markdown should include next-session actions.");
}

static void CoachEvidenceIncludesStoredSessionMemoryWhenTrackCarMatch()
{
    var (session, _) = SessionWithFuelEstimate();
    var stored = new SessionMemorySummary(
        Guid.NewGuid(),
        "monza|gt3",
        "Monza",
        "GT3",
        "Practice",
        DateTimeOffset.UtcNow.AddDays(-1),
        112.4,
        112.8,
        78,
        2.1,
        "Tyres needed one lap to warm.",
        null,
        ["Late braking in Turn 1"],
        ["Hesitation on throttle at Parabolica"],
        ["Sector 2 delta"],
        [],
        ["Fuel window was tight"],
        ["Improve sector 2 exit", "Brake earlier into Turn 1", "Hold throttle at Parabolica"]);
    var evidence = new CoachEvidenceBuilder().Build(new CoachEvidenceInput(
        session,
        null,
        session.Events,
        PreviousStoredSessionMemory: stored,
        RecentStoredSessionMemories: [stored]));

    Assert(
        evidence.Packets.Any(packet =>
            packet.Category == "TrackMemory"
                && packet.Explanation.Contains("Stored session data", StringComparison.OrdinalIgnoreCase)),
        "Coach evidence should include stored session memory packets when track/car memory exists.");

    var aiContext = EngineerAiContextBuilder.Build(
        session,
        "what should I improve",
        new CoachContext(PreviousStoredSessionMemory: stored),
        evidence);
    Assert(
        aiContext.Facts.Any(fact => fact.Detail.Contains("Stored session data", StringComparison.OrdinalIgnoreCase)),
        "AI context should include stored session memory facts for improvement questions.");

    var coach = new CoachEngine();
    var answer = coach.Answer(
        session,
        "what did I struggle with here",
        new CoachContext(PreviousStoredSessionMemory: stored),
        evidence);
    Assert(
        answer.Content.Contains("During your previous Monza session", StringComparison.OrdinalIgnoreCase),
        "Coach answer should use natural coaching language for stored struggle questions.");
    Assert(
        !answer.Content.Contains("Zone 4", StringComparison.OrdinalIgnoreCase),
        "Coach answer should not expose raw zone identifiers when a track guide is available.");
}

static void CoachingQueriesRouteDespiteLowConfidence()
{
    var session = new SessionState();
    var result = CoachQueryPipeline.Resolve(
        session,
        "what should i improve",
        new CoachEngine(),
        null,
        null,
        null,
        false,
        new CoachQueryTranscriptContext("what should i improve", "what should i improve", 0.35f));

    Assert(
        !result.FinalDisplayedText.Contains("didn't catch", StringComparison.OrdinalIgnoreCase),
        "Known coaching query should route even with low speech confidence.");
}

static void StoredSessionMemoryUsesNaturalLanguage()
{
    Assert(TrackGuideWebCatalog.TryGetGuide("Monza", out var guide), "Fixture catalog should provide Monza guide.");
    var stored = new SessionMemorySummary(
        Guid.NewGuid(),
        "monza|gt3",
        "Monza",
        "GT3",
        "Practice",
        DateTimeOffset.UtcNow.AddDays(-2),
        112.4,
        112.8,
        78,
        2.1,
        null,
        null,
        [],
        [],
        ["Zone 4: unstable throttle (0.3s)"],
        [],
        [],
        []);

    var answer = SessionMemoryCoachingFormatter.BuildStruggleAnswer(stored, guide);
    Assert(
        answer.Contains("During your previous Monza session", StringComparison.OrdinalIgnoreCase),
        "Struggle answer should open with natural session context.");
    Assert(
        answer.Contains("Focus on", StringComparison.OrdinalIgnoreCase),
        "Struggle answer should include a recommendation.");
    Assert(
        !answer.Contains("Zone 4", StringComparison.OrdinalIgnoreCase),
        "Struggle answer should not expose raw zone identifiers when corner mapping exists.");
    Assert(
        answer.Contains("Ascari", StringComparison.OrdinalIgnoreCase),
        "Zone 4 should map to Ascari in the cached Monza guide.");
}

static void CoachingTrackGuidePhrasesPassTranscriptGate()
{
    foreach (var query in new[]
             {
                 "what should i improve",
                 "what can i improve",
                 "what should i focus on",
                 "what should i watch on this track",
                 "how should i drive this track",
                 "track advice",
                 "driving advice"
             })
    {
        var decision = TranscriptGate.Evaluate(
            query,
            0.40f,
            new SpeechCaptureMetrics(10f, 10f, 10f, 1f, false, false, "Test mic", MicSignalQuality.Bad));
        Assert(decision.Accepted, $"Coaching phrase should pass transcript gate: {query}");
    }
}

static async Task CachedTrackGuideLookupByTrack()
{
    var dbPath = Path.Combine(Path.GetTempPath(), $"race-engineer-track-guide-{Guid.NewGuid():N}.sqlite3");
    var storage = new StorageService(dbPath);
    await storage.InitializeAsync();
    var options = new TrackResearchOptions(true, "web", false, 30);
    var service = new TrackResearchService(storage, options);

    var fetch = await service.FetchGuideAsync("Monza");
    Assert(fetch.Guide is not null && fetch.Saved, "Track guide fetch should save a cached guide for Monza.");
    Assert(fetch.Guide!.MajorBrakingZones.Count > 0, "Cached track guide should include braking zones.");

    var cached = await service.GetCachedGuideAsync("Monza");
    Assert(cached.Guide is not null && cached.FromCache, "Cached track guide lookup should return stored guide.");
    Assert(cached.Guide!.TrackName.Contains("Monza", StringComparison.OrdinalIgnoreCase), "Cached guide should preserve track name.");
}

static async Task TrackResearchDisabledProviderDoesNotFetchWeb()
{
    var dbPath = Path.Combine(Path.GetTempPath(), $"race-engineer-track-guide-disabled-{Guid.NewGuid():N}.sqlite3");
    var storage = new StorageService(dbPath);
    await storage.InitializeAsync();
    var service = new TrackResearchService(storage, TrackResearchOptions.Disabled);

    var fetch = await service.FetchGuideAsync("Monza");
    Assert(fetch.Guide is null, "Disabled track research should not fetch web guides.");
    Assert(fetch.Message?.Contains("disabled", StringComparison.OrdinalIgnoreCase) == true, "Disabled fetch should explain why it did not run.");

    var ensure = await service.EnsureGuideCachedAsync(
        "Monza",
        new TrackResearchContext(VehicleActivity.OnTrack, IsReviewMode: false));
    Assert(ensure.Guide is null, "Disabled ensure should not create a cached guide.");
}

static void CoachUsesCachedTrackGuideWhenAvailable()
{
    var coach = new CoachEngine();
    var session = new SessionState();
    session.ApplySnapshot(Packet("""{"speed_kmh":180,"fuel":12.5,"track_name":"Monza"}"""), []);
    Assert(TrackGuideWebCatalog.TryGetGuide("Monza", out var guide), "Fixture catalog should provide Monza guide.");

    var answer = coach.Answer(
        session,
        "What should I watch for at Monza?",
        new CoachContext(
            RacePrepPlan: new RacePrepPlan(null, "Monza", "Practice", null, null, null, null, null, null, null),
            ExternalResearchAvailable: true,
            CachedTrackGuide: guide));

    Assert(
        answer.Content.Contains("Cached track guide", StringComparison.OrdinalIgnoreCase),
        "Coach answer should label cached track guide data.");
    Assert(
        answer.Content.Contains("Turn 1", StringComparison.OrdinalIgnoreCase)
            || answer.Content.Contains("braking", StringComparison.OrdinalIgnoreCase),
        "Coach answer should use cached track guide content.");
    Assert(
        answer.Content.Contains("Live telemetry", StringComparison.OrdinalIgnoreCase),
        "Coach answer should combine cached track guide with live telemetry when available.");
}

static async Task TrackGuideCacheRefresh()
{
    var dbPath = Path.Combine(Path.GetTempPath(), $"race-engineer-track-guide-refresh-{Guid.NewGuid():N}.sqlite3");
    var storage = new StorageService(dbPath);
    await storage.InitializeAsync();
    var service = new TrackResearchService(storage, new TrackResearchOptions(true, "web", false, 1));

    var initial = await service.FetchGuideAsync("Monza");
    Assert(initial.Guide is not null && initial.Saved, "Initial track guide refresh should save Monza.");

    var stale = initial.Guide! with
    {
        FetchedAt = DateTimeOffset.UtcNow.AddDays(-10),
        RefreshedAt = null
    };
    await new CachedTrackResearchService(storage).SaveGuideAsync(stale, KnowledgeSourceTypes.Web);
    Assert(service.IsStale(stale), "Stale guide should exceed refresh threshold.");

    var refreshed = await service.FetchGuideAsync("Monza", forceRefresh: true);
    Assert(refreshed.Guide is not null && refreshed.Saved, "Forced refresh should save an updated guide.");
    Assert(refreshed.Guide!.RefreshedAt is not null, "Refreshed guide should record refresh timestamp.");
}

static void TrackGuideNoGuideFallback()
{
    var coach = new CoachEngine();
    var session = new SessionState();
    session.ApplySnapshot(Packet("""{"speed_kmh":180,"fuel":12.5,"track_name":"Unknown Circuit"}"""), []);

    var answer = coach.Answer(
        session,
        "How should I drive here?",
        new CoachContext(
            RacePrepPlan: new RacePrepPlan(null, "Unknown Circuit", "Practice", null, null, null, null, null, null, null),
            ExternalResearchAvailable: false,
            CachedTrackGuide: null));

    Assert(
        answer.Content.Contains("No stored track notes", StringComparison.OrdinalIgnoreCase)
            || answer.Content.Contains("unavailable", StringComparison.OrdinalIgnoreCase),
        "Coach should fall back gracefully when no cached track guide exists.");
}

static void EngineerAiContextIncludesTrackGuideSummary()
{
    Assert(TrackGuideWebCatalog.TryGetGuide("Monza", out var guide), "Fixture catalog should provide Monza guide.");
    var session = new SessionState();
    session.ApplySnapshot(Packet("""{"speed_kmh":180,"fuel":12.5,"track_name":"Monza"}"""), []);
    var evidence = new CoachEvidenceBuilder().Build(new CoachEvidenceInput(
        session,
        CachedTrackGuide: guide));
    var context = EngineerAiContextBuilder.Build(
        session,
        "What are the key corners?",
        new CoachContext(CachedTrackGuide: guide),
        evidence);

    Assert(
        context.Facts.Any(fact =>
            fact.Topic == "TrackGuide"
                || fact.Detail.Contains("Cached track guide", StringComparison.OrdinalIgnoreCase)
                || fact.Detail.Contains("Key corners", StringComparison.OrdinalIgnoreCase)),
        "AI context should include cached track guide summary packets.");
}

static IReadOnlyList<TelemetrySnapshot> BuildOpponentGapSnapshots(params (int lap, double gapAhead, double? gapBehind)[] points)
{
    var snapshots = new List<TelemetrySnapshot>();
    var timestamp = DateTimeOffset.UtcNow;
    foreach (var point in points)
    {
        var behind = point.gapBehind is { } gapBehind
            ? $",\"gap_behind_s\":{gapBehind.ToString(CultureInfo.InvariantCulture)},\"car_behind\":\"Pilot B\""
            : "";
        snapshots.Add(PacketAt(
            $"\"lap_number\":{point.lap},\"lap_progress\":0.50,\"position\":4,\"total_cars\":20,\"gap_ahead_s\":{point.gapAhead.ToString(CultureInfo.InvariantCulture)},\"car_ahead\":\"Pilot A\"{behind}",
            timestamp));
        timestamp = timestamp.AddSeconds(2);
    }

    return snapshots;
}

static LiveRaceContext BuildOpponentRaceContext(double? gapAhead, double? gapBehind, string? carAhead = "Pilot A", string? carBehind = "Pilot B") =>
    new(
        "Monza",
        null,
        "GT3",
        "GT3",
        "Race",
        4,
        20,
        4,
        20,
        gapAhead,
        gapBehind,
        carAhead,
        carBehind,
        null,
        null,
        null,
        null,
        null,
        RaceContextConfidence.Good,
        new RaceFieldDiagnostics(["gap_ahead_s", "gap_behind_s"], [], "ok"));

static void OpponentGapTrendGainingDetection()
{
    var snapshots = BuildOpponentGapSnapshots((1, 2.0, 3.0), (2, 1.8, 3.0), (3, 1.6, 3.0), (4, 1.4, 3.0), (5, 1.2, 3.0));
    var trend = OpponentIntelligenceService.AnalyzeGapTrend(snapshots.Select(snapshot => new OpponentSnapshot(
        snapshot.RaceAwareness?.Position,
        snapshot.RaceAwareness?.TotalCars,
        snapshot.RaceAwareness?.CarAhead,
        snapshot.RaceAwareness?.CarBehind,
        snapshot.RaceAwareness?.GapAheadSeconds,
        snapshot.RaceAwareness?.GapBehindSeconds,
        snapshot.RaceAwareness?.CurrentLap ?? snapshot.Lap.LapNumber)).ToArray());
    Assert(trend.AheadDirection == GapTrendDirection.Gaining, "Gap ahead trend should classify as gaining.");
    Assert(trend.GapAheadChangePerLapSeconds is < 0, "Gaining on car ahead should produce negative gap change.");
}

static void OpponentGapTrendLosingDetection()
{
    var snapshots = BuildOpponentGapSnapshots((1, 1.0, 2.0), (2, 1.2, 2.0), (3, 1.4, 2.0), (4, 1.6, 2.0), (5, 1.8, 2.0));
    var trend = OpponentIntelligenceService.AnalyzeGapTrend(snapshots.Select(snapshot => new OpponentSnapshot(
        4,
        20,
        "Pilot A",
        "Pilot B",
        snapshot.RaceAwareness?.GapAheadSeconds,
        snapshot.RaceAwareness?.GapBehindSeconds,
        snapshot.RaceAwareness?.CurrentLap ?? snapshot.Lap.LapNumber)).ToArray());
    Assert(trend.AheadDirection == GapTrendDirection.Losing, "Gap ahead trend should classify as losing.");
}

static void OpponentGapTrendStableDetection()
{
    var snapshots = BuildOpponentGapSnapshots((1, 1.50, 2.0), (2, 1.51, 2.0), (3, 1.49, 2.0), (4, 1.50, 2.0), (5, 1.50, 2.0));
    var trend = OpponentIntelligenceService.AnalyzeGapTrend(snapshots.Select(snapshot => new OpponentSnapshot(
        4,
        20,
        "Pilot A",
        "Pilot B",
        snapshot.RaceAwareness?.GapAheadSeconds,
        snapshot.RaceAwareness?.GapBehindSeconds,
        snapshot.RaceAwareness?.CurrentLap ?? snapshot.Lap.LapNumber)).ToArray());
    Assert(trend.AheadDirection == GapTrendDirection.Stable, "Small gap noise should classify as stable.");
}

static void OpponentAttackOpportunityDetection()
{
    TrackGuideWebCatalog.TryGetGuide("Monza", out var guide);
    var recommendation = OpponentIntelligenceService.Build(new OpponentIntelligenceInput(
        BuildOpponentRaceContext(1.2, 3.0),
        BuildOpponentGapSnapshots((1, 1.8, 3.0), (2, 1.6, 3.0), (3, 1.4, 3.0), (4, 1.2, 3.0), (5, 1.0, 3.0)),
        guide));
    Assert(recommendation.Battle.Type == RaceBattleType.AttackOpportunity, "Closing gap inside 1.5s should detect attack opportunity.");
    Assert(
        recommendation.AttackZoneRecommendation?.Contains("Best opportunity", StringComparison.OrdinalIgnoreCase) == true,
        "Attack recommendation should reference a cached track guide zone.");
}

static void OpponentDefendRecommendation()
{
    TrackGuideWebCatalog.TryGetGuide("Monza", out var guide);
    var snapshots = BuildOpponentGapSnapshots((1, 3.0, 1.4), (2, 3.0, 1.2), (3, 3.0, 1.0), (4, 3.0, 0.8), (5, 3.0, 0.6));
    var recommendation = OpponentIntelligenceService.Build(new OpponentIntelligenceInput(
        BuildOpponentRaceContext(3.0, 0.6),
        snapshots,
        guide));
    Assert(recommendation.Battle.Type == RaceBattleType.DefensiveSituation, "Closing gap behind should detect defensive situation.");
    Assert(
        recommendation.DefendZoneRecommendation?.Contains("Defend", StringComparison.OrdinalIgnoreCase) == true,
        "Defensive recommendation should reference a track guide zone.");
}

static void OpponentUnavailableTelemetryFallback()
{
    var coach = new CoachEngine();
    var session = new SessionState();
    session.ApplySnapshot(Packet("""{"speed_kmh":180,"track_name":"Monza","position":4}"""), []);
    var answer = coach.Answer(
        session,
        "Can I overtake?",
        new CoachContext(
            RaceContext: LiveRaceContext.Unavailable("missing"),
            OpponentIntelligence: OpponentIntelligenceRecommendation.Unavailable));
    Assert(
        answer.Content.Contains(OpponentIntelligenceAnswerBuilder.UnavailableMessage, StringComparison.OrdinalIgnoreCase),
        "Coach should report unavailable opponent telemetry instead of inventing gaps.");
}

static void EngineerAiContextIncludesBattleSummary()
{
    var recommendation = OpponentIntelligenceService.Build(new OpponentIntelligenceInput(
        BuildOpponentRaceContext(1.1, 2.5),
        BuildOpponentGapSnapshots((1, 1.8, 2.5), (2, 1.6, 2.5), (3, 1.4, 2.5), (4, 1.2, 2.5), (5, 1.1, 2.5))));
    var session = new SessionState();
    session.ApplySnapshot(Packet("""{"speed_kmh":180,"track_name":"Monza","position":4,"gap_ahead_s":1.1,"gap_behind_s":2.5}"""), []);
    var evidence = new CoachEvidenceBuilder().Build(new CoachEvidenceInput(
        session,
        RaceContext: BuildOpponentRaceContext(1.1, 2.5),
        OpponentIntelligence: recommendation));
    var context = EngineerAiContextBuilder.Build(
        session,
        "What is my race situation?",
        new CoachContext(RaceContext: BuildOpponentRaceContext(1.1, 2.5), OpponentIntelligence: recommendation),
        evidence);
    Assert(
        context.Facts.Any(fact => fact.Topic == "OpponentIntelligence" || fact.Detail.Contains("attack", StringComparison.OrdinalIgnoreCase) || fact.Detail.Contains("gap", StringComparison.OrdinalIgnoreCase)),
        "AI context should include opponent battle summary packets.");
}

static void StrategyKnowledgeTenLapFuelFromLiveBurn()
{
    var engine = new EventEngine();
    var session = new SessionState();
    Apply(session, engine, Packet("""{"lap_progress":0.99,"fuel":30.0,"track_name":"Monza","car_name":"GT3","car_class":"GT3"}"""));
    Apply(session, engine, Packet("""{"lap_progress":0.01,"lap_time_s":90.0,"fuel":27.6,"track_name":"Monza","car_name":"GT3","car_class":"GT3"}"""));
    Apply(session, engine, Packet("""{"lap_progress":0.99,"fuel":27.6,"track_name":"Monza","car_name":"GT3","car_class":"GT3"}"""));
    Apply(session, engine, Packet("""{"lap_progress":0.01,"lap_time_s":89.5,"fuel":25.2,"track_name":"Monza","car_name":"GT3","car_class":"GT3"}"""));

    var recommendation = StrategyKnowledgeService.Build(
        new StrategyKnowledgeInput(
            "Monza",
            "GT3",
            "GT3",
            "Race",
            20,
            session,
            new TelemetryAnalyticsService().Analyze(new SessionAnalyticsInput(session)),
            null,
            null,
            null,
            null,
            null,
            null,
            1.0,
            10),
        "what fuel for 10 laps");

    Assert(recommendation.FuelPerLapSource == StrategyKnowledgeDataSource.LiveTelemetry, "Ten-lap fuel recommendation should use live burn.");
    Assert(recommendation.ExpectedFuelPerLap is > 0, "Live burn should produce fuel per lap.");
    Assert(recommendation.RecommendedStartingFuelLiters is > 0, "Ten-lap recommendation should include total fuel.");
    Assert(
        recommendation.LiveFuelNote?.Contains("Based on your current burn", StringComparison.OrdinalIgnoreCase) == true,
        "Live burn note should describe current burn based fuel need.");

    var coach = new CoachEngine();
    var answer = coach.Answer(
        session,
        "what fuel for 10 laps",
        new CoachContext(
            RaceContext: RaceContextService.Build(session, session.LatestSnapshot),
            StrategyKnowledge: recommendation));
    Assert(
        answer.Content.Contains("live telemetry", StringComparison.OrdinalIgnoreCase)
            || answer.Content.Contains("Based on your current burn", StringComparison.OrdinalIgnoreCase),
        "Coach fuel planning answer should include live burn (TrackCarKnowledge or StrategyKnowledge).");
}

static void StrategyKnowledgeStoredCarFallback()
{
    var memory = TrackMemoryRecord.Empty("monza|gt3", "Monza", "GT3") with
    {
        FuelUsedPerLap = 2.35
    };
    var recommendation = StrategyKnowledgeService.Build(new StrategyKnowledgeInput(
        "Monza",
        "GT3",
        "GT3",
        "Race",
        10,
        new SessionState(),
        null,
        null,
        null,
        memory,
        null,
        null,
        null,
        1.0,
        10));

    Assert(recommendation.FuelPerLapSource == StrategyKnowledgeDataSource.StoredCar, "Stored car memory should provide fuel per lap.");
    Assert(recommendation.ExpectedFuelPerLap == 2.35, "Stored car fuel per lap should be preserved.");
}

static void StrategyKnowledgeClassBaselineFallback()
{
    var recommendation = StrategyKnowledgeService.Build(new StrategyKnowledgeInput(
        "Monza",
        "Ferrari 488 GT3",
        "GT3",
        "Race",
        10,
        new SessionState(),
        null,
        null,
        null,
        null,
        null,
        null,
        null,
        1.0,
        10));

    Assert(recommendation.FuelPerLapSource == StrategyKnowledgeDataSource.GenericClass, "Missing exact car history should fall back to class baseline.");
    Assert(
        recommendation.ClassBaselineNote?.Contains("GT3 baseline", StringComparison.OrdinalIgnoreCase) == true,
        "Class fallback should label GT3 baseline usage.");
}

static void StrategyKnowledgeMissingDataUnavailable()
{
    var recommendation = StrategyKnowledgeService.Build(new StrategyKnowledgeInput(
        null,
        null,
        null,
        null,
        null,
        new SessionState(),
        null,
        null,
        null,
        null,
        null,
        null,
        null,
        1.0,
        10));

    var coach = new CoachEngine();
    var answer = coach.Answer(session: new SessionState(), "what fuel for 10 laps", new CoachContext(StrategyKnowledge: recommendation));
    Assert(
        answer.Content.Contains("unavailable", StringComparison.OrdinalIgnoreCase),
        "Missing strategy data should return unavailable response.");
}

static void TrackCarKnowledgeFuelForTenLapsUsesKnowledgeBaseline()
{
    var recommendation = TrackCarKnowledgeService.Build(new TrackCarKnowledgeInput(
        "Red Bull Ring",
        "GT3 Car",
        "GT3",
        new SessionState(),
        null,
        null,
        null,
        RequestedLapCount: 10));

    Assert(recommendation.IsAvailable, "Red Bull Ring GT3 knowledge should be available.");
    Assert(
        recommendation.FuelSource == TrackCarKnowledgeDataSource.StoredKnowledge,
        "Baseline fuel should come from stored knowledge without live burn.");
    Assert(
        recommendation.KnowledgeFuelNote?.Contains("Stored knowledge", StringComparison.OrdinalIgnoreCase) == true,
        "Fuel note should label stored knowledge.");

    var coach = new CoachEngine();
    var answer = coach.Answer(
        new SessionState(),
        "fuel for 10 laps",
        new CoachContext(
            RacePrepPlan: new RacePrepPlan("GT3 Car", "Red Bull Ring", "Practice", null, null, null, null, null, null, null),
            TrackCarKnowledge: recommendation));

    Assert(
        answer.Content.Contains("Stored knowledge", StringComparison.OrdinalIgnoreCase),
        "Coach answer should label stored knowledge when live burn is unavailable.");
    Assert(
        answer.Content.Contains("10 laps", StringComparison.OrdinalIgnoreCase),
        "Coach answer should reference requested lap count.");
}

static void TrackCarKnowledgeFuelForTenLapsUsesLiveTelemetry()
{
    var engine = new EventEngine();
    var session = new SessionState();
    Apply(session, engine, Packet("""{"lap_progress":0.99,"fuel":10.0,"track_name":"Red Bull Ring","car_name":"GT3","car_class":"GT3"}"""));
    Apply(session, engine, Packet("""{"lap_progress":0.01,"lap_time_s":90.0,"fuel":7.7,"track_name":"Red Bull Ring","car_name":"GT3","car_class":"GT3"}"""));
    Apply(session, engine, Packet("""{"lap_progress":0.99,"fuel":7.7,"track_name":"Red Bull Ring","car_name":"GT3","car_class":"GT3"}"""));
    Apply(session, engine, Packet("""{"lap_progress":0.01,"lap_time_s":89.5,"fuel":5.3,"track_name":"Red Bull Ring","car_name":"GT3","car_class":"GT3"}"""));

    Assert(session.FuelUsedPerLap is > 0, "Smoke setup should produce at least two valid lap fuel samples.");

    var recommendation = TrackCarKnowledgeService.Build(new TrackCarKnowledgeInput(
        "Red Bull Ring",
        "GT3",
        "GT3",
        session,
        null,
        null,
        null,
        RequestedLapCount: 10));

    Assert(
        recommendation.FuelSource == TrackCarKnowledgeDataSource.LiveTelemetry,
        "Live burn should override stored baseline when valid laps exist.");
    Assert(
        recommendation.LiveFuelNote?.Contains("live telemetry", StringComparison.OrdinalIgnoreCase) == true,
        "Live fuel note should label telemetry source.");

    var answer = new CoachEngine().Answer(
        session,
        "fuel for 10 laps",
        new CoachContext(
            RaceContext: RaceContextService.Build(session, session.LatestSnapshot),
            TrackCarKnowledge: recommendation));

    Assert(
        answer.Content.Contains("live telemetry", StringComparison.OrdinalIgnoreCase),
        "Coach answer should prefer live telemetry fuel estimate when available.");
}

static void TrackCarKnowledgeBrakeDemandUsesStoredKnowledge()
{
    var recommendation = TrackCarKnowledgeService.Build(new TrackCarKnowledgeInput(
        "Red Bull Ring",
        "GT3",
        "GT3",
        new SessionState(),
        null,
        null,
        null));

    var answer = TrackCarKnowledgeService.BuildCoachAnswer(recommendation, "is this track hard on brakes", new SessionState());
    Assert(
        answer.Contains("Stored knowledge", StringComparison.OrdinalIgnoreCase),
        "Brake demand answer should be labelled as stored knowledge.");
    Assert(
        answer.Contains("Red Bull Ring", StringComparison.OrdinalIgnoreCase),
        "Brake demand answer should name the track.");
    Assert(
        !answer.Contains("overheating", StringComparison.OrdinalIgnoreCase),
        "Brake demand answer must not invent live telemetry state.");
}

static void TrackCarKnowledgeAiContextIncludesFacts()
{
    var recommendation = TrackCarKnowledgeService.Build(new TrackCarKnowledgeInput(
        "Monza",
        "GT3",
        "GT3",
        new SessionState(),
        null,
        null,
        null));
    var facts = TrackCarKnowledgeService.BuildAiFacts(recommendation);
    Assert(facts.Count >= 3, "AI facts should include track, brake, and tyre baselines.");
    Assert(
        facts.Any(fact => fact.Topic == "TrackCarKnowledge" && fact.Source == "stored knowledge"),
        "AI facts should label stored knowledge source.");
}

static async Task WebResearchCacheLookupRoundTrip()
{
    var dbPath = Path.Combine(Path.GetTempPath(), $"race-engineer-web-research-{Guid.NewGuid():N}.sqlite3");
    var storage = new StorageService(dbPath);
    await storage.InitializeAsync();
    var cache = new ResearchCacheService(storage);
    var item = new ResearchKnowledgeItem(
        Guid.NewGuid(),
        ResearchTopics.BrakeDemand,
        "Red Bull Ring",
        null,
        "GT3",
        "Cached research: Red Bull Ring rewards stable braking into Turn 1.",
        ["Turn 1 is a heavy stop", "Remus repeats front-axle load"],
        [new ResearchSourceLabel("web-search-placeholder catalog", "catalog://brake-demand")],
        DateTimeOffset.UtcNow,
        "Medium",
        "web-search-placeholder");

    await cache.SaveAsync(item);
    var lookup = await cache.LookupAsync("Red Bull Ring", carClass: "GT3");
    Assert(lookup.Items.Count >= 1, "Cached research lookup should return saved items.");
    Assert(
        lookup.Items.Any(entry => entry.Topic == ResearchTopics.BrakeDemand),
        "Cached research lookup should preserve topic.");
    Assert(
        ResearchKnowledgeMapper.TryParse(ResearchKnowledgeMapper.ToKnowledgeSource(item), out var parsed)
            && parsed.Summary == item.Summary,
        "Research mapper round-trip should preserve summary.");
}

static async Task WebResearchCacheMatchesTrackAliasAndCarFilter()
{
    var dbPath = Path.Combine(Path.GetTempPath(), $"race-engineer-web-research-alias-{Guid.NewGuid():N}.sqlite3");
    var storage = new StorageService(dbPath);
    await storage.InitializeAsync();
    var cache = new ResearchCacheService(storage);
    await cache.SaveAsync(new ResearchKnowledgeItem(
        Guid.NewGuid(),
        ResearchTopics.TrackGuide,
        "Red Bull Ring",
        null,
        null,
        "Cached track research for Red Bull Ring.",
        ["Turn 1 heavy stop"],
        [new ResearchSourceLabel("web-search-placeholder catalog")],
        DateTimeOffset.UtcNow,
        "Medium",
        "web-search-placeholder"));
    await cache.SaveAsync(new ResearchKnowledgeItem(
        Guid.NewGuid(),
        ResearchTopics.TrackCarStrategy,
        "Red Bull Ring",
        "BMW M4 GT3 Evo",
        "GT3",
        "Cached track-car strategy for Red Bull Ring GT3.",
        ["One-stop typical"],
        [new ResearchSourceLabel("built-in catalog")],
        DateTimeOffset.UtcNow,
        "Medium",
        "web-search-placeholder"));

    var lookup = await cache.LookupAsync("Red Bull Ring-GP", "BMW M4 GT3 Evo", "GT3");
    Assert(lookup.Items.Count >= 2, "Alias track lookup should include track-wide and GT3 strategy items.");
    Assert(
        lookup.Items.Any(item => item.Topic == ResearchTopics.TrackGuide),
        "Alias track lookup should include track guide research saved under canonical track name.");
    Assert(
        lookup.Items.Any(item => item.Topic == ResearchTopics.TrackCarStrategy && item.CarClass == "GT3"),
        "Alias track lookup should include car-class strategy items.");

    var service = new WebResearchService(storage, new WebResearchOptions(true, false, 30));
    var fetch = await service.FetchTrackResearchAsync("Red Bull Ring-GP", "BMW M4 GT3 Evo", "GT3");
    Assert(fetch.Status == "success", "Fetch with track alias should succeed against placeholder provider.");
    Assert(fetch.ItemsReturned >= 4, "Track fetch should return multiple research items.");
    var bundle = await service.LoadBundleAsync("Red Bull Ring-GP", "BMW M4 GT3 Evo", "GT3");
    Assert(bundle.HasResearch, "Loaded bundle should contain cached research after fetch.");
    Assert(bundle.SourceCount >= fetch.ItemsReturned, "Bundle source count should reflect cached research items.");
}

static async Task WebResearchStaleRefreshBehavior()
{
    var dbPath = Path.Combine(Path.GetTempPath(), $"race-engineer-web-research-stale-{Guid.NewGuid():N}.sqlite3");
    var storage = new StorageService(dbPath);
    await storage.InitializeAsync();
    var cache = new ResearchCacheService(storage);
    var staleItem = new ResearchKnowledgeItem(
        Guid.NewGuid(),
        ResearchTopics.TrackGuide,
        "Monza",
        null,
        null,
        "Stale cached track research.",
        ["stale fact"],
        [new ResearchSourceLabel("stale source", "catalog://stale")],
        DateTimeOffset.UtcNow.AddDays(-40),
        "Low",
        "web-search-placeholder");
    await cache.SaveAsync(staleItem);

    var service = new WebResearchService(storage, new WebResearchOptions(true, false, 1));
    var bundle = await service.EnsureCachedAsync(
        "Monza",
        null,
        null,
        new WebResearchContext(VehicleActivity.Stationary, IsReviewMode: false));
    Assert(bundle.HasResearch, "Stale refresh should populate cached research when web research is enabled.");
    Assert(
        bundle.Items.Any(item => item.Topic == ResearchTopics.TrackGuide && item.Summary.Contains("Cached track research", StringComparison.OrdinalIgnoreCase)),
        "Stale refresh should replace stale track research with fresh placeholder research.");
}

static void WebResearchDisabledProviderDoesNotFetch()
{
    var provider = DisabledWebResearchProvider.Instance;
    var fetch = provider.FetchTrackResearchAsync("Monza").GetAwaiter().GetResult();
    Assert(fetch.Items.Count == 0, "Disabled provider should not fetch research items.");
    Assert(
        fetch.Message.Contains("disabled", StringComparison.OrdinalIgnoreCase),
        "Disabled provider should explain that web research is disabled.");
}

static void WebResearchCoachAnswerCombinesCachedResearchAndTelemetry()
{
    var engine = new EventEngine();
    var session = new SessionState();
    Apply(session, engine, Packet("""{"lap_progress":0.99,"fuel":10.0,"track_name":"Red Bull Ring","car_name":"GT3","car_class":"GT3"}"""));
    Apply(session, engine, Packet("""{"lap_progress":0.01,"lap_time_s":90.0,"fuel":7.7,"track_name":"Red Bull Ring","car_name":"GT3","car_class":"GT3"}"""));
    Apply(session, engine, Packet("""{"lap_progress":0.99,"fuel":7.7,"track_name":"Red Bull Ring","car_name":"GT3","car_class":"GT3"}"""));
    Apply(session, engine, Packet("""{"lap_progress":0.01,"lap_time_s":89.5,"fuel":5.3,"track_name":"Red Bull Ring","car_name":"GT3","car_class":"GT3"}"""));

    var bundle = new WebResearchBundle(
        "Red Bull Ring",
        "GT3",
        "GT3",
        [
            new ResearchKnowledgeItem(
                Guid.NewGuid(),
                ResearchTopics.OvertakingZones,
                "Red Bull Ring",
                "GT3",
                "GT3",
                "Cached research: key overtaking zones at Red Bull Ring include main straight into Turn 1.",
                ["Main straight into Turn 1"],
                [new ResearchSourceLabel("web-search-placeholder catalog")],
                DateTimeOffset.UtcNow,
                "Medium",
                "web-search-placeholder")
        ]);

    var answer = ResearchKnowledgeCoachService.BuildCombinedAnswer(
        "where can i overtake",
        bundle,
        session,
        session.Events,
        TrackCarKnowledgeService.Build(new TrackCarKnowledgeInput("Red Bull Ring", "GT3", "GT3", session, null, null, null)));

    Assert(
        answer.Contains("Cached research", StringComparison.OrdinalIgnoreCase),
        "Coach combined answer should label cached research.");
    Assert(
        answer.Contains("Live telemetry", StringComparison.OrdinalIgnoreCase),
        "Coach combined answer should include live telemetry when lap fuel samples exist.");

    var coach = new CoachEngine();
    var coachAnswer = coach.Answer(
        session,
        "combine my telemetry with track notes",
        new CoachContext(
            CachedWebResearch: bundle,
            ExternalResearchAvailable: true));
    Assert(
        coachAnswer.Content.Contains("cached research", StringComparison.OrdinalIgnoreCase),
        "Combined telemetry/knowledge coach answer should surface cached research lines.");
}

static void WebResearchAiContextIncludesCachedResearchFacts()
{
    var bundle = new WebResearchBundle(
        "Monza",
        "GT3",
        "GT3",
        [
            new ResearchKnowledgeItem(
                Guid.NewGuid(),
                ResearchTopics.SetupPriorities,
                "Monza",
                "GT3",
                "GT3",
                "Cached research setup priorities for Monza GT3: stable braking into Turn 1.",
                ["Stable braking into Turn 1"],
                [new ResearchSourceLabel("built-in catalog")],
                DateTimeOffset.UtcNow,
                "Medium",
                "web-search-placeholder")
        ]);

    var facts = ResearchKnowledgeCoachService.BuildAiFacts(bundle, "what setup matters here");
    Assert(facts.Count >= 1, "AI facts should include cached research when bundle is available.");
    Assert(
        facts.Any(fact => fact.Topic == "WebResearch" && fact.Source == "cached research"),
        "AI facts should label cached research source.");

    var evidence = new CoachEvidenceBuilder().Build(new CoachEvidenceInput(
        new SessionState(),
        CachedWebResearch: bundle));
    Assert(
        evidence.Packets.Any(packet => packet.Category == "WebResearch"),
        "Coach evidence should include cached research packets.");

    var aiContext = EngineerAiContextBuilder.Build(
        new SessionState(),
        "what setup matters here",
        new CoachContext(CachedWebResearch: bundle),
        evidence);
    Assert(
        aiContext.Facts.Any(fact => fact.Source == "cached research"),
        "Engineer AI context should include cached research facts for setup questions.");
}

static async Task TrackMemoryRetrievalAndHistoricalComparison()
{
    var dbPath = Path.Combine(Path.GetTempPath(), $"race-engineer-track-memory-{Guid.NewGuid():N}.sqlite3");
    var storage = new StorageService(dbPath);
    await storage.InitializeAsync();
    var service = new TrackMemoryService();
    var engine = new EventEngine();
    var session = new SessionState();
    Apply(session, engine, Packet("""{"lap_progress":0.99,"fuel":10.0,"track_name":"Monza","car_name":"GT3"}"""));
    Apply(session, engine, Packet("""{"lap_progress":0.01,"lap_time_s":112.4,"fuel":8.0,"track_name":"Monza","car_name":"GT3"}"""));
    Apply(session, engine, Packet("""{"lap_progress":0.99,"fuel":8.0,"track_name":"Monza","car_name":"GT3"}"""));
    Apply(session, engine, Packet("""{"lap_progress":0.01,"lap_time_s":111.8,"fuel":6.0,"track_name":"Monza","car_name":"GT3"}"""));

    var analytics = new TelemetryAnalyticsService().Analyze(new SessionAnalyticsInput(session));
    var saved = await service.UpsertFromSessionAsync(
        storage,
        new TrackMemoryInput("Monza", "GT3", session, analytics, null, null, null));
    var loaded = await service.LoadAsync(storage, "Monza", "GT3");

    Assert(loaded is not null, "Track memory should load for same track/car.");
    Assert(loaded!.SessionCount == 1, "Track memory should record one stored session.");
    Assert(loaded.BestLapSeconds is <= 111.8, "Stored best lap should reflect completed session.");

    var followUp = new SessionState();
    Apply(followUp, engine, Packet("""{"lap_progress":0.99,"fuel":10.0,"track_name":"Monza","car_name":"GT3"}"""));
    Apply(followUp, engine, Packet("""{"lap_progress":0.01,"lap_time_s":111.0,"fuel":8.0,"track_name":"Monza","car_name":"GT3"}"""));
    var comparison = service.Compare(loaded, followUp, analytics);
    Assert(comparison.HasHistoricalData, "Comparison should use stored session data.");
    Assert(comparison.Summary.Contains("Stored session data", StringComparison.OrdinalIgnoreCase), "Comparison summary should label stored session data.");
    Assert(comparison.BestLapDeltaSeconds is < 0, "Faster current lap should produce negative delta versus stored best.");
}

static void CoachRaceAwarenessMissingGapSaysUnavailable()
{
    var coach = new CoachEngine();
    var session = new SessionState();
    session.ApplySnapshot(
        SimHubPacketParser.Parse(
            """{"schema":"acevo_engineer.simhub_datacore","schema_version":1,"track_name":"Monza","position":5,"speed_kmh":180}"""),
        []);
    var raceContext = RaceContextService.Build(session, session.LatestSnapshot);
    var answer = coach.Answer(
        session,
        "gap ahead",
        new CoachContext(RaceContext: raceContext));

    Assert(
        answer.Content.Contains("Opponent gap ahead data is unavailable", StringComparison.OrdinalIgnoreCase)
            || answer.Content.Contains("does not expose opponent gaps", StringComparison.OrdinalIgnoreCase),
        "Race awareness answer must say opponent gap data is unavailable when telemetry omits it.");
    Assert(
        !answer.Content.Contains("0.3s/lap", StringComparison.OrdinalIgnoreCase),
        "Race awareness must not invent gap values.");
}

static void SimHubTelemetryProviderStillParsesExistingFixture()
{
    var provider = new SimHubTelemetryProvider("127.0.0.1", 20999);
    var raw = ValidRaw("""{"speed_kmh":181.5,"gear":4,"throttle":0.72,"lap_progress":0.42,"tyre_temp_c":[90,91,null,89]}""");
    Assert(SimHubPacketParser.TryParse(raw, out var snapshot, out var warning), $"Fixture packet should parse: {warning}");
    Assert(provider.ProviderId == "simhub", "SimHub provider should identify as simhub.");
    Assert(snapshot!.Car.SpeedKmh == 181.5, "Fixture speed should still parse through SimHub schema.");
}

static void SimHubProviderCapabilitiesReportMissingOpponentGaps()
{
    var capabilities = TelemetryProviderCapabilities.SimHub;

    Assert(!capabilities.OpponentGaps, "SimHub provider should report opponent gaps as unsupported.");
    Assert(!capabilities.Standings, "SimHub provider should report standings as unsupported.");
    Assert(
        capabilities.MissingCapabilityLabels.Contains(TelemetryProviderCapabilities.CapabilityLabels.OpponentGaps),
        "Missing capabilities should include opponent gaps.");
    Assert(
        capabilities.MissingCapabilityLabels.Contains(TelemetryProviderCapabilities.CapabilityLabels.Standings),
        "Missing capabilities should include standings.");
}

static void CoachGapQuestionExplainsProviderLimitation()
{
    var coach = new CoachEngine();
    var session = new SessionState();
    session.ApplySnapshot(
        SimHubPacketParser.Parse(
            """{"schema":"acevo_engineer.simhub_datacore","schema_version":1,"track_name":"Monza","position":5,"speed_kmh":180}"""),
        []);
    var raceContext = RaceContextService.Build(session, session.LatestSnapshot);
    var answer = coach.Answer(
        session,
        "gap ahead",
        new CoachContext(
            RaceContext: raceContext,
            TelemetryProviderCapabilities: TelemetryProviderCapabilities.SimHub));

    Assert(
        answer.Content.Contains("SimHub", StringComparison.OrdinalIgnoreCase),
        "Coach gap answer should name the active provider.");
    Assert(
        answer.Content.Contains("does not expose opponent gaps", StringComparison.OrdinalIgnoreCase),
        "Coach gap answer should explain the provider limitation.");
}

static void TelemetryProviderFactorySelectsSimHub()
{
    var result = TelemetryProviderFactory.Instance.Create(AppSettings.Default);

    Assert(result.Provider is SimHubTelemetryProvider, "Default settings should create the SimHub provider.");
    Assert(result.Provider.ProviderId == "simhub", "SimHub provider id should be simhub.");
    Assert(result.Warnings.Count == 0, "Default SimHub selection should not emit warnings.");
}

static void TelemetryProviderFactoryUnknownProviderFallsBackSafely()
{
    var result = TelemetryProviderFactory.Instance.Create(AppSettings.Default with { TelemetryProvider = "unknown-provider" });

    Assert(result.Provider is SimHubTelemetryProvider, "Unknown provider should fall back to SimHub.");
    Assert(
        result.Warnings.Any(warning => warning.Contains("Unknown telemetry provider", StringComparison.OrdinalIgnoreCase)),
        "Unknown provider fallback should emit a warning.");
}

static void TelemetryProviderFactorySelectsIracing()
{
    var result = TelemetryProviderFactory.Instance.Create(AppSettings.Default with { TelemetryProvider = "iracing" });

    Assert(result.Provider is IracingTelemetryProvider, "iRacing setting should create the iRacing provider.");
    Assert(result.Provider.ProviderId == "iracing", "iRacing provider id should be iracing.");
    Assert(result.Provider.DisplayName == "iRacing", "iRacing provider display name should be iRacing.");
    Assert(
        result.Warnings.Any(warning => warning.Contains("iRacing", StringComparison.OrdinalIgnoreCase)),
        "iRacing startup should emit an SDK availability warning.");
}

static void IracingUnavailableProviderReportsClearDiagnostic()
{
    var provider = new IracingTelemetryProvider(new IracingSdkTelemetrySession());

    Assert(provider.Status == TelemetryProviderStatus.Offline, "Uninitialized iRacing provider should start offline.");
    Assert(
        provider.Diagnostics.Contains("SDK", StringComparison.OrdinalIgnoreCase),
        "Unavailable iRacing provider should explain SDK is not connected.");
}

static void IracingCapabilitiesIncludeOpponentGapsAndStandings()
{
    var capabilities = TelemetryProviderCapabilities.Iracing;

    Assert(capabilities.OpponentGaps, "iRacing provider should report opponent gaps as supported.");
    Assert(capabilities.Standings, "iRacing provider should report standings as supported.");
    Assert(capabilities.SessionInfo, "iRacing provider should report session info as supported.");
    Assert(!capabilities.Tyres, "iRacing provider should report tyres as unsupported for now.");
    Assert(
        capabilities.MissingCapabilityLabels.Contains(TelemetryProviderCapabilities.CapabilityLabels.Tyres),
        "Missing capabilities should include tyres for iRacing.");
}

static void IracingMapperMapsFixtureToTelemetrySnapshot()
{
    var frame = new IracingTelemetryFrame(
        DateTimeOffset.UtcNow,
        new IracingCarTelemetry(50.5, 7200, 4, 0.82, 0.0, -0.12, 42.5),
        new IracingSessionTelemetry("summit_summit_raceway", "Summit Point Raceway", "MX-5 Cup", "Class C", "Race", 1, 20, 900, null),
        new IracingRaceTelemetry(3, 2, 18, 7, 92.456, 0.61, 1.234, 0.876, "Car 12", "Car 8", 0),
        new IracingPitTelemetry(false, false, false),
        new IracingFlagsTelemetry("green", null));

    var snapshot = IracingTelemetryMapper.Map(frame);

    Assert(Math.Abs(snapshot.Car.SpeedKmh!.Value - 181.8) < 0.1, "iRacing mapper should convert m/s to km/h.");
    Assert(snapshot.Car.Gear == 4, "iRacing mapper should preserve gear.");
    Assert(snapshot.RaceAwareness?.GapAheadSeconds == 1.234, "iRacing mapper should map gap ahead.");
    Assert(snapshot.RaceAwareness?.Position == 3, "iRacing mapper should map race position.");
    Assert(snapshot.RaceAwareness?.TrackName == "Summit Point Raceway", "iRacing mapper should map track display name.");
}

static void CoachIracingUnavailableExplainsProviderLimitation()
{
    var coach = new CoachEngine();
    var session = new SessionState();
    var answer = coach.Answer(
        session,
        "gap ahead",
        new CoachContext(
            TelemetryProviderCapabilities: TelemetryProviderCapabilities.Iracing,
            TelemetryProviderStatus: TelemetryProviderStatus.Error,
            TelemetryProviderDiagnostics: IracingProviderDiagnostics.SdkUnavailable));

    Assert(
        answer.Content.Contains("iRacing telemetry provider is unavailable", StringComparison.OrdinalIgnoreCase),
        "Coach should explain iRacing provider is unavailable.");
    Assert(
        answer.Content.Contains("SDK", StringComparison.OrdinalIgnoreCase),
        "Coach should include SDK diagnostic detail.");
}

static void CoachTrackIdentityRoutingDoesNotFallbackToPosition()
{
    var coach = new CoachEngine();
    var session = new SessionState();
    session.ApplySnapshot(
        SimHubPacketParser.Parse(
            """{"schema":"acevo_engineer.simhub_datacore","schema_version":1,"track_name":"Monza","position":1,"total_cars":20,"speed_kmh":180}"""),
        []);
    var raceContext = RaceContextService.Build(session, session.LatestSnapshot);

    var trackAnswer = coach.Answer(
        session,
        "which track am i on",
        new CoachContext(RaceContext: raceContext));
    Assert(
        trackAnswer.Content.Contains("You are on Monza", StringComparison.OrdinalIgnoreCase),
        "Track identity question should return the parsed track name.");
    Assert(
        !trackAnswer.Content.Contains("You are P", StringComparison.OrdinalIgnoreCase),
        "Track identity question must not fallback to race position.");

    var missingTrackSession = new SessionState();
    missingTrackSession.ApplySnapshot(
        SimHubPacketParser.Parse(
            """{"schema":"acevo_engineer.simhub_datacore","schema_version":1,"position":1,"total_cars":20,"speed_kmh":180}"""),
        []);
    var missingTrackContext = RaceContextService.Build(missingTrackSession, missingTrackSession.LatestSnapshot);
    var missingTrackAnswer = coach.Answer(
        missingTrackSession,
        "which track am i on",
        new CoachContext(RaceContext: missingTrackContext));
    Assert(
        missingTrackAnswer.Content.Contains(RaceAwarenessAnswerBuilder.TrackUnavailableMessage, StringComparison.OrdinalIgnoreCase),
        "Missing track telemetry should return the track unavailable message.");
    Assert(
        !missingTrackAnswer.Content.Contains("You are P", StringComparison.OrdinalIgnoreCase),
        "Missing track telemetry must not answer with race position.");
}

static void RaceAwarenessRoutingDiagnosticsCoverTrackAndPosition()
{
    var session = new SessionState();
    session.ApplySnapshot(
        SimHubPacketParser.Parse(
            """{"schema":"acevo_engineer.simhub_datacore","schema_version":1,"track_name":"Spa","car_name":"GT3","session_type":"race","position":4,"total_cars":18,"gap_ahead_s":1.250,"speed_kmh":190}"""),
        []);
    var raceContext = RaceContextService.Build(session, session.LatestSnapshot);

    var trackRouting = RaceAwarenessQueryClassifier.Classify("which track am i on", raceContext);
    Assert(trackRouting.Subtopic == RaceAwarenessSubtopic.TrackIdentity, "Track question should classify as TrackIdentity.");
    Assert(trackRouting.SelectedTelemetryFields.Contains("track_name"), "Track routing should select track_name when present.");
    Assert(trackRouting.MissingTelemetryFields.Contains("circuit_id"), "Track routing should still report missing circuit_id.");

    var positionRouting = RaceAwarenessQueryClassifier.Classify("koja mi je pozicija", raceContext);
    Assert(positionRouting.Subtopic == RaceAwarenessSubtopic.Position, "Position question should classify as Position.");
    Assert(positionRouting.SelectedTelemetryFields.Contains("position"), "Position routing should select position when present.");

    var gapRouting = RaceAwarenessQueryClassifier.Classify("gap ahead", raceContext);
    Assert(gapRouting.Subtopic == RaceAwarenessSubtopic.GapAhead, "Gap ahead question should classify as GapAhead.");
    Assert(gapRouting.SelectedTelemetryFields.Contains("gap_ahead_s"), "Gap ahead routing should select gap_ahead_s when present.");
}

static void RaceAwarenessValidatorBlocksCrossTopicFallback()
{
    var session = new SessionState();
    session.ApplySnapshot(
        SimHubPacketParser.Parse(
            """{"schema":"acevo_engineer.simhub_datacore","schema_version":1,"track_name":"Monza","position":1,"speed_kmh":180}"""),
        []);
    var raceContext = RaceContextService.Build(session, session.LatestSnapshot);
    var context = new CoachContext(RaceContext: raceContext);
    var leaked = new CoachMessage(
        "coach",
        "Telemetry shows Race position (Race position is P1.)",
        [],
        null,
        []);

    var corrected = RaceAwarenessAnswerValidator.Enforce("which track am i on", leaked, session, context);
    Assert(
        corrected.Content.Contains("You are on Monza", StringComparison.OrdinalIgnoreCase),
        "Validator should replace position leak with the track identity answer when track telemetry exists.");
    Assert(
        !corrected.Content.Contains("Race position", StringComparison.OrdinalIgnoreCase),
        "Validator must remove unrelated race position content from track questions.");

    var missingTrackSession = new SessionState();
    missingTrackSession.ApplySnapshot(
        SimHubPacketParser.Parse(
            """{"schema":"acevo_engineer.simhub_datacore","schema_version":1,"position":2,"speed_kmh":180}"""),
        []);
    var missingTrackContext = new CoachContext(RaceContext: RaceContextService.Build(missingTrackSession, missingTrackSession.LatestSnapshot));
    var correctedMissingTrack = RaceAwarenessAnswerValidator.Enforce(
        "which track am i on",
        leaked,
        missingTrackSession,
        missingTrackContext);
    Assert(
        correctedMissingTrack.Content.Contains(RaceAwarenessAnswerBuilder.TrackUnavailableMessage, StringComparison.OrdinalIgnoreCase),
        "Validator should replace position leak with track unavailable when track telemetry is missing.");

    var positionSession = new SessionState();
    positionSession.ApplySnapshot(
        SimHubPacketParser.Parse(
            """{"schema":"acevo_engineer.simhub_datacore","schema_version":1,"track_name":"Monza","position":3,"total_cars":20,"speed_kmh":180}"""),
        []);
    var positionContext = new CoachContext(RaceContext: RaceContextService.Build(positionSession, positionSession.LatestSnapshot));
    var positionAnswer = new CoachEngine().Answer(positionSession, "koja mi je pozicija", positionContext);
    Assert(
        positionAnswer.Content.Contains("You are P3", StringComparison.OrdinalIgnoreCase),
        "Position question should answer with parsed position.");
    Assert(
        !positionAnswer.Content.Contains("You are on Monza", StringComparison.OrdinalIgnoreCase),
        "Position question must not mention track name.");
}

static void ProviderDiagMapsTrackAndCarIntoRaceAwareness()
{
    var json =
        """{"schema":"acevo_engineer.simhub_datacore","schema_version":1,"speed_kmh":180,"provider_diag":{"pm_last_track_id":"Monza-GP","pm_last_car_id":"Ferrari F2004","pm_game_name":"Assetto Corsa"}}""";
    var valid = SimHubPacketParser.TryParse(json, out var snapshot, out var warning);
    Assert(valid, $"Provider diag packet should parse: {warning}");
    Assert(snapshot!.RaceAwareness?.TrackName == "Monza-GP", "pm_last_track_id should map to track name.");
    Assert(snapshot.RaceAwareness?.CircuitId == "Monza-GP", "pm_last_track_id should also populate circuit id fallback.");
    Assert(snapshot.RaceAwareness?.CarName == "Ferrari F2004", "pm_last_car_id should map to car name.");

    var session = new SessionState();
    session.ApplySnapshot(snapshot, []);
    var raceContext = RaceContextService.Build(session, snapshot);
    Assert(raceContext.TrackName == "Monza-GP", "Race context should expose provider_diag track.");
    Assert(raceContext.CarName == "Ferrari F2004", "Race context should expose provider_diag car.");
    Assert(
        raceContext.Diagnostics.PresentFields.Contains("track_name"),
        "Race diagnostics should mark track_name present from provider_diag.");
    Assert(
        raceContext.Diagnostics.PresentFields.Contains("car_name"),
        "Race diagnostics should mark car_name present from provider_diag.");

    var coach = new CoachEngine();
    var trackAnswer = coach.Answer(session, "which track am i on", new CoachContext(RaceContext: raceContext));
    Assert(
        trackAnswer.Content.Contains("You are on Monza-GP", StringComparison.OrdinalIgnoreCase),
        "Track identity question should use provider_diag track id.");
    var carAnswer = coach.Answer(session, "which car am i in", new CoachContext(RaceContext: raceContext));
    Assert(
        carAnswer.Content.Contains("Ferrari F2004", StringComparison.OrdinalIgnoreCase),
        "Car identity question should use provider_diag car id.");

    var dbPath = Path.Combine(Path.GetTempPath(), $"race-engineer-provider-diag-{Guid.NewGuid():N}.sqlite3");
    var storage = new StorageService(dbPath);
    storage.InitializeAsync().GetAwaiter().GetResult();
    var memoryService = new TrackMemoryService();
    var engine = new EventEngine();
    Apply(session, engine, Packet("""{"lap_progress":0.99,"fuel":10.0,"provider_diag":{"pm_last_track_id":"Monza-GP","pm_last_car_id":"Ferrari F2004"}}"""));
    Apply(session, engine, Packet("""{"lap_progress":0.01,"lap_time_s":112.0,"fuel":8.0,"provider_diag":{"pm_last_track_id":"Monza-GP","pm_last_car_id":"Ferrari F2004"}}"""));
    var analytics = new TelemetryAnalyticsService().Analyze(new SessionAnalyticsInput(session));
    memoryService.UpsertFromSessionAsync(
        storage,
        new TrackMemoryInput("Monza-GP", "Ferrari F2004", session, analytics, null, null, null)).GetAwaiter().GetResult();
    var loaded = memoryService.LoadAsync(storage, "Monza-GP", "Ferrari F2004").GetAwaiter().GetResult();
    Assert(loaded is not null, "Track memory should key off provider_diag track/car values.");
    Assert(loaded!.TrackName == "Monza-GP", "Stored track memory should retain provider_diag track id.");
    Assert(loaded.CarName == "Ferrari F2004", "Stored track memory should retain provider_diag car id.");
}

static void TrackCarIdentityRoutingKeepsFieldsSeparate()
{
    var session = new SessionState();
    session.ApplySnapshot(
        SimHubPacketParser.Parse(
            """{"schema":"acevo_engineer.simhub_datacore","schema_version":1,"speed_kmh":180,"provider_diag":{"pm_last_track_id":"Monza-GP","pm_last_car_id":"Ferrari F2004"}}"""),
        []);
    var raceContext = RaceContextService.Build(session, session.LatestSnapshot);
    var coach = new CoachEngine();
    var context = new CoachContext(RaceContext: raceContext);

    var trackRouting = RaceAwarenessQueryClassifier.Classify("which track am i on", raceContext);
    Assert(trackRouting.Subtopic == RaceAwarenessSubtopic.TrackIdentity, "Track question should classify as TrackIdentity.");
    Assert(trackRouting.SelectedField == "track_name", "Track routing should select track_name field.");
    Assert(trackRouting.SelectedValue == "Monza-GP", "Track routing should expose track field value.");

    var carRouting = RaceAwarenessQueryClassifier.Classify("which car am i in", raceContext);
    Assert(carRouting.Subtopic == RaceAwarenessSubtopic.CarIdentity, "Car question should classify as CarIdentity.");
    Assert(carRouting.SelectedField == "car_name", "Car routing should select car_name field.");
    Assert(carRouting.SelectedValue == "Ferrari F2004", "Car routing should expose car field value.");

    var trackAnswer = coach.Answer(session, "which track am i on", context);
    Assert(
        trackAnswer.Content.Contains("You are on Monza-GP", StringComparison.OrdinalIgnoreCase),
        "Track question should answer with track only.");
    Assert(
        !trackAnswer.Content.Contains("Ferrari", StringComparison.OrdinalIgnoreCase),
        "Track question must not answer with car identity.");

    var carAnswer = coach.Answer(session, "which car am i in", context);
    Assert(
        carAnswer.Content.Contains("Ferrari F2004", StringComparison.OrdinalIgnoreCase),
        "Car question should answer with car only.");
    Assert(
        !carAnswer.Content.Contains("Monza-GP", StringComparison.OrdinalIgnoreCase),
        "Car question must not answer with track identity.");
    Assert(
        !carAnswer.Content.Contains("You are on", StringComparison.OrdinalIgnoreCase),
        "Car question must not use track phrasing.");

    var trackOnlySession = new SessionState();
    trackOnlySession.ApplySnapshot(
        SimHubPacketParser.Parse(
            """{"schema":"acevo_engineer.simhub_datacore","schema_version":1,"provider_diag":{"pm_last_track_id":"Monza-GP"}}"""),
        []);
    var trackOnlyContext = RaceContextService.Build(trackOnlySession, trackOnlySession.LatestSnapshot);
    var missingCarAnswer = coach.Answer(
        trackOnlySession,
        "which car am i in",
        new CoachContext(RaceContext: trackOnlyContext));
    Assert(
        missingCarAnswer.Content.Contains(RaceAwarenessAnswerBuilder.CarUnavailableMessage, StringComparison.OrdinalIgnoreCase),
        "Missing car must not fallback to track.");
    Assert(
        !missingCarAnswer.Content.Contains("Monza-GP", StringComparison.OrdinalIgnoreCase),
        "Missing car answer must not mention track name.");

    var carOnlySession = new SessionState();
    carOnlySession.ApplySnapshot(
        SimHubPacketParser.Parse(
            """{"schema":"acevo_engineer.simhub_datacore","schema_version":1,"provider_diag":{"pm_last_car_id":"Ferrari F2004"}}"""),
        []);
    var carOnlyContext = RaceContextService.Build(carOnlySession, carOnlySession.LatestSnapshot);
    var missingTrackAnswer = coach.Answer(
        carOnlySession,
        "which track am i on",
        new CoachContext(RaceContext: carOnlyContext));
    Assert(
        missingTrackAnswer.Content.Contains(RaceAwarenessAnswerBuilder.TrackUnavailableMessage, StringComparison.OrdinalIgnoreCase),
        "Missing track must not fallback to car.");
    Assert(
        !missingTrackAnswer.Content.Contains("Ferrari", StringComparison.OrdinalIgnoreCase),
        "Missing track answer must not mention car name.");

    var leakedCar = new CoachMessage("coach", "Ferrari F2004.", [], null, []);
    var correctedTrack = RaceAwarenessAnswerValidator.Enforce(
        "which track am i on",
        leakedCar,
        session,
        context);
    Assert(
        correctedTrack.Content.Contains("You are on Monza-GP", StringComparison.OrdinalIgnoreCase),
        "Validator must replace car leak on track question with track identity answer.");
    Assert(
        !correctedTrack.Content.Contains("Ferrari", StringComparison.OrdinalIgnoreCase),
        "Validator must remove car field from track question answer.");

    var leakedTrack = new CoachMessage("coach", "You are on Monza-GP.", [], null, []);
    var correctedCar = RaceAwarenessAnswerValidator.Enforce(
        "which car am i in",
        leakedTrack,
        session,
        context);
    Assert(
        correctedCar.Content.Contains("Ferrari F2004", StringComparison.OrdinalIgnoreCase),
        "Validator must replace track leak on car question with car identity answer.");
    Assert(
        !correctedCar.Content.Contains("You are on", StringComparison.OrdinalIgnoreCase),
        "Validator must remove track field from car question answer.");

    var hybrid = new HybridCoachEngine(new MockEngineerAiProvider(), new EngineerAiOptions(true, "mock", "", "", 40, 3));
    var evidence = new CoachEvidenceBuilder().Build(new CoachEvidenceInput(session, [session.LatestSnapshot!], [], null, null, null, null, null, null, null, raceContext));
    var pipelineTrack = CoachQueryPipeline.Resolve(session, "which track am i on", hybrid, context, evidence);
    Assert(
        pipelineTrack.FinalDisplayedText.Contains("Monza-GP", StringComparison.OrdinalIgnoreCase),
        "Pipeline track question must display track answer only.");
    Assert(
        !pipelineTrack.FinalDisplayedText.Contains("Ferrari", StringComparison.OrdinalIgnoreCase),
        "Pipeline track question must not display car answer.");
    Assert(pipelineTrack.Trace.RaceAwarenessSubtopic == RaceAwarenessSubtopic.TrackIdentity, "Pipeline trace should include track subtopic.");
    Assert(pipelineTrack.Trace.RaceAwarenessSelectedValue == "Monza-GP", "Pipeline trace should include selected track value.");
}

static void TopicRoutingGuardsIdentityBleed()
{
    var session = new SessionState();
    session.ApplySnapshot(
        SimHubPacketParser.Parse(
            """{"schema":"acevo_engineer.simhub_datacore","schema_version":1,"speed_kmh":180,"provider_diag":{"pm_last_track_id":"Monza-GP","pm_last_car_id":"Ferrari F2004"},"tyre_temp_c":[88,91,90,89]}"""),
        []);
    var raceContext = RaceContextService.Build(session, session.LatestSnapshot);
    var coach = new CoachEngine();
    var context = new CoachContext(RaceContext: raceContext);
    var evidence = new CoachEvidenceBuilder().Build(new CoachEvidenceInput(session, [session.LatestSnapshot!], [], null, null, null, null, null, null, null, raceContext));

    Assert(
        CoachQueryTopicClassifier.ClassifyPrimary("kakve su gume") == CoachQueryTopic.Tyre,
        "Tyre question should classify as Tyre.");
    Assert(
        CoachQueryTopicClassifier.ClassifyPrimary("gdje gubim vrijeme") == CoachQueryTopic.LosingTime,
        "Losing-time question should classify as LosingTime.");
    Assert(
        CoachQueryTopicClassifier.ClassifyPrimary("which sector") == CoachQueryTopic.LosingTime,
        "Sector question should classify as LosingTime.");
    Assert(
        CoachQueryTopicClassifier.ClassifyPrimary("which car am i in") == CoachQueryTopic.CarIdentity,
        "Car identity question should classify as CarIdentity.");
    Assert(
        CoachQueryTopicClassifier.ClassifyPrimary("which track am i on") == CoachQueryTopic.TrackIdentity,
        "Track identity question should classify as TrackIdentity.");

    var tyreAnswer = coach.Answer(session, "kakve su gume", context, evidence);
    Assert(
        !CoachIdentityAnswerGuard.LooksLikeIdentityAnswer(tyreAnswer.Content, raceContext),
        "Tyre answer must not look like car or track identity.");
    Assert(
        tyreAnswer.Content.Contains("Front-left", StringComparison.OrdinalIgnoreCase)
            || tyreAnswer.Content.Contains("Tyre", StringComparison.OrdinalIgnoreCase)
            || tyreAnswer.Content.Contains("gum", StringComparison.OrdinalIgnoreCase),
        "Tyre answer should stay on tyre content.");

    var losingTimeAnswer = coach.Answer(session, "gdje gubim vrijeme", context, evidence);
    Assert(
        !CoachIdentityAnswerGuard.LooksLikeIdentityAnswer(losingTimeAnswer.Content, raceContext),
        "Losing-time answer must not look like identity.");
    Assert(
        losingTimeAnswer.Content.Contains(SessionDriverPerformance.NeedCleanLapMessage, StringComparison.Ordinal)
            || losingTimeAnswer.Content.Contains("loss", StringComparison.OrdinalIgnoreCase)
            || losingTimeAnswer.Content.Contains("Sector", StringComparison.OrdinalIgnoreCase)
            || losingTimeAnswer.Content.Contains("Zone", StringComparison.OrdinalIgnoreCase),
        "Losing-time answer should use performance coaching or request a clean lap.");

    var sectorAnswer = coach.Answer(session, "which sector", context, evidence);
    Assert(
        !CoachIdentityAnswerGuard.LooksLikeIdentityAnswer(sectorAnswer.Content, raceContext),
        "Sector question must never return car or track identity.");
    Assert(
        CoachQueryTopicClassifier.ClassifyPrimary("which sector") == CoachQueryTopic.LosingTime,
        "Sector question routing should stay on performance topic.");

    var carAnswer = coach.Answer(session, "which car am i in", context, evidence);
    Assert(
        carAnswer.Content.Contains("Ferrari F2004", StringComparison.OrdinalIgnoreCase),
        "Car identity question should answer with car only.");
    Assert(
        !carAnswer.Content.Contains("You are on", StringComparison.OrdinalIgnoreCase),
        "Car identity answer must not include track phrasing.");

    var trackAnswer = coach.Answer(session, "which track am i on", context, evidence);
    Assert(
        trackAnswer.Content.Contains("You are on Monza-GP", StringComparison.OrdinalIgnoreCase),
        "Track identity question should answer with track only.");
    Assert(
        !trackAnswer.Content.Contains("Ferrari", StringComparison.OrdinalIgnoreCase),
        "Track identity answer must not include car name.");

    var leakedIdentity = new CoachMessage("coach", "Ferrari F2004.", [], null, []);
    var guardedTyre = CoachTopicOutputGuard.RejectIdentityBleed(CoachQueryTopic.Tyre, leakedIdentity, raceContext);
    Assert(
        guardedTyre.Content.Contains("Tyre status is unavailable", StringComparison.OrdinalIgnoreCase),
        "Final topic guard should reject identity bleed on tyre questions.");

    var guardedSector = CoachTopicOutputGuard.RejectIdentityBleed(CoachQueryTopic.LosingTime, leakedIdentity, raceContext);
    Assert(
        guardedSector.Content.Contains(SessionDriverPerformance.NeedCleanLapMessage, StringComparison.Ordinal),
        "Final topic guard should reject identity bleed on sector/time-loss questions.");

    var unchangedTyre = RaceAwarenessAnswerValidator.Enforce("kakve su gume", tyreAnswer, session, context);
    Assert(
        unchangedTyre.Content == tyreAnswer.Content,
        "Validator must not rewrite tyre answers into identity responses.");

    var lowConfidence = CoachQueryPipeline.Resolve(
        session,
        "which car am i in",
        coach,
        context,
        evidence,
        transcriptContext: new CoachQueryTranscriptContext(
            RawTranscript: "which car am i in",
            NormalizedTranscript: "which car am i in",
            Confidence: 0.20f));
    Assert(
        lowConfidence.FinalDisplayedText.Contains("I didn't catch that", StringComparison.OrdinalIgnoreCase)
            || lowConfidence.Trace.AnswerSource == "transcript-rejected",
        "Low-confidence transcript should not route to car identity.");
}

static void CoachHistoricalLapComparisonUsesStoredData()
{
    var coach = new CoachEngine();
    var engine = new EventEngine();
    var session = new SessionState();
    Apply(session, engine, Packet("""{"lap_progress":0.99,"fuel":10.0,"track_name":"Monza","car_name":"GT3"}"""));
    Apply(session, engine, Packet("""{"lap_progress":0.01,"lap_time_s":111.0,"fuel":8.0,"track_name":"Monza","car_name":"GT3"}"""));

    var memory = TrackMemoryRecord.Empty("monza|gt3", "Monza", "GT3") with
    {
        SessionCount = 1,
        BestLapSeconds = 112.4,
        AverageCleanLapSeconds = 112.8
    };
    var comparison = new TrackMemoryService().Compare(memory, session, null);
    var answer = coach.Answer(
        session,
        "am I faster than last time",
        new CoachContext(TrackMemory: memory, TrackMemoryComparison: comparison));

    Assert(
        answer.Content.Contains("Stored session data", StringComparison.OrdinalIgnoreCase),
        "Historical lap comparison should label stored session data.");
    Assert(
        answer.Content.Contains("faster", StringComparison.OrdinalIgnoreCase),
        "Historical lap comparison should report faster pace when current best beats stored best.");
}

static void DriverPerformanceIntelligenceComputesDeterministicInsights()
{
    var (session, _, _) = SessionWithPerformanceContext();
    var snapshots = BuildLapIntelligenceSnapshots(session);
    var analytics = new TelemetryAnalyticsService().Analyze(new SessionAnalyticsInput(session, snapshots));
    var lapIntelligence = new LapIntelligenceService().Analyze(new LapIntelligenceInput(session, snapshots));
    var selectedLapNumber = session.CompletedLaps
        .Where(lap => lap.IsValid && lap.Duration.HasValue)
        .OrderByDescending(lap => lap.Duration!.Value)
        .First()
        .LapNumber;
    var service = new DriverPerformanceIntelligenceService();
    var input = new DriverPerformanceInput(
        session,
        snapshots,
        session.Events,
        analytics,
        lapIntelligence,
        SelectedLapNumber: selectedLapNumber);
    var first = service.Analyze(input);
    var second = service.Analyze(input);

    Assert(first.Availability == second.Availability, "Performance availability should be deterministic.");
    Assert(first.Availability == "Available", "Performance analysis should be available with valid laps.");
    Assert(first.ZoneCount == second.ZoneCount && first.ZoneCount > 0, "Performance zones should be deterministic.");
    Assert(first.ZoneMetrics.Count > 0, "Performance zone metrics should be generated.");
    Assert(first.CoachingMessages.Count > 0, "Performance coaching messages should be generated.");
}

static void CoachLosingTimeUsesPerformanceIntelligence()
{
    var coach = new CoachEngine();
    var (session, performance, context) = SessionWithPerformanceContext();
    var evidence = BuildSampleCoachEvidence(session);

    Assert(performance.Availability == "Available", "Performance analysis should be available for losing-time coaching.");

    var answer = coach.Answer(session, "gdje gubim vrijeme", context, evidence);

    Assert(
        answer.Content.Contains("Zone", StringComparison.OrdinalIgnoreCase)
            || answer.Content.Contains("Sector", StringComparison.OrdinalIgnoreCase)
            || answer.Content.Contains("throttle", StringComparison.OrdinalIgnoreCase)
            || answer.Content.Contains("brake", StringComparison.OrdinalIgnoreCase)
            || answer.Content.Contains("loss", StringComparison.OrdinalIgnoreCase)
            || answer.Content.Contains("losing", StringComparison.OrdinalIgnoreCase),
        $"Losing-time answer should mention a specific zone, sector, or behavior. Actual: '{answer.Content}'");
    Assert(
        !answer.Content.Contains("Focus on the sector with the largest loss", StringComparison.Ordinal),
        "Losing-time answer should not fall back to generic coaching when performance data exists.");
}

static void CoachPerformanceNeedsCleanLapWhenInsufficientData()
{
    var coach = new CoachEngine();
    var session = new SessionState();
    var performance = SessionDriverPerformance.Unavailable(SessionDriverPerformance.NeedCleanLapMessage);
    var context = new CoachContext(DriverPerformance: performance, DriverCoaching: DriverCoachingRecommendation.Unavailable);

    var losingTime = coach.Answer(session, "gdje gubim vrijeme", context);
    var improvement = coach.Answer(session, "what should I improve", context);

    Assert(
        losingTime.Content.Contains(SessionDriverPerformance.NeedCleanLapMessage, StringComparison.Ordinal),
        "Losing-time answer should request a clean lap when performance data is unavailable.");
    Assert(
        improvement.Content.Contains(SessionDriverPerformance.NeedCleanLapMessage, StringComparison.Ordinal),
        "Improvement answer should request a clean lap when performance data is unavailable.");
}

static void DriverCoachingDetectsCurrentSessionImprovement()
{
    var (session, performance, _) = SessionWithPerformanceContext();
    var analytics = new TelemetryAnalyticsService().Analyze(new SessionAnalyticsInput(session, BuildLapIntelligenceSnapshots(session)));
    var previous = new SessionMemorySummary(
        Guid.NewGuid(),
        "monza|gt3",
        "Monza",
        "GT3",
        "Practice",
        DateTimeOffset.UtcNow.AddDays(-1),
        92.0,
        93.5,
        70,
        2.5,
        null,
        null,
        [],
        [],
        [],
        [],
        [],
        []);
    var coaching = DriverCoachingIntelligenceService.Build(new DriverCoachingInput(
        performance,
        analytics with
        {
            BestVsAverage = analytics.BestVsAverage with { AverageLapSeconds = 92.8 }
        },
        null,
        null,
        previous));

    Assert(coaching.HasData, "Driver coaching should be available with valid laps.");
    Assert(
        coaching.ProgressTrend is DriverProgressTrend.Improving or DriverProgressTrend.Stable,
        $"Expected improving/stable trend, got {coaching.ProgressTrend}.");
}

static void DriverCoachingComparesPreviousSession()
{
    var (session, performance, _) = SessionWithPerformanceContext();
    var analytics = new TelemetryAnalyticsService().Analyze(new SessionAnalyticsInput(session, BuildLapIntelligenceSnapshots(session)));
    var previous = new SessionMemorySummary(
        Guid.NewGuid(),
        "monza|gt3",
        "Monza",
        "GT3",
        "Practice",
        DateTimeOffset.UtcNow.AddDays(-2),
        92.0,
        93.0,
        65,
        2.5,
        null,
        null,
        [],
        [],
        [],
        [],
        [],
        []);
    var coaching = DriverCoachingIntelligenceService.Build(new DriverCoachingInput(
        performance,
        analytics with { BestVsAverage = analytics.BestVsAverage with { AverageLapSeconds = 92.5 } },
        null,
        null,
        previous));

    Assert(
        coaching.PreviousSessionDeltaSummary?.Contains("Monza", StringComparison.OrdinalIgnoreCase) == true,
        "Previous session comparison should mention the track name.");
    Assert(
        coaching.PreviousSessionDeltaSummary?.Contains("faster", StringComparison.OrdinalIgnoreCase) == true
            || coaching.PreviousSessionDeltaSummary?.Contains("slower", StringComparison.OrdinalIgnoreCase) == true
            || coaching.PreviousSessionDeltaSummary?.Contains("matching", StringComparison.OrdinalIgnoreCase) == true,
        "Previous session comparison should report pace direction.");
}

static void DriverCoachingExtractsRepeatedWeaknesses()
{
    var performance = SessionWithPerformanceContext().Performance with
    {
        MistakeClusters =
        [
            new MistakeCluster("abrupt brake release", 3, "Zone 2"),
            new MistakeCluster("delayed throttle pickup", 2, "Zone 4")
        ]
    };
    var memory = TrackMemoryRecord.Empty("monza|gt3", "Monza", "GT3") with
    {
        RepeatedWeaknesses = ["abrupt brake release in Zone 2"]
    };
    var coaching = DriverCoachingIntelligenceService.Build(new DriverCoachingInput(
        performance,
        TrackMemory: memory));

    Assert(coaching.RepeatedWeaknesses.Count >= 2, "Repeated weaknesses should merge clusters and stored memory.");
    Assert(
        coaching.RepeatedWeaknesses.Any(item => item.Contains("brake", StringComparison.OrdinalIgnoreCase)),
        "Repeated weaknesses should include braking pattern.");
}

static void DriverCoachingBuildsTopThreeTargets()
{
    var (session, performance, _) = SessionWithPerformanceContext();
    var coaching = DriverCoachingIntelligenceService.Build(new DriverCoachingInput(performance));

    Assert(coaching.TopCoachingTargets.Count > 0, "Top coaching targets should be generated.");
    Assert(coaching.TopCoachingTargets.Count <= 3, "Top coaching targets should be capped at three.");
}

static void DriverCoachingNoDataFallback()
{
    var coaching = DriverCoachingRecommendation.Unavailable;
    var answer = DriverCoachingAnswerBuilder.Build("what should I improve", coaching);

    Assert(
        answer.Contains(SessionDriverPerformance.NeedCleanLapMessage, StringComparison.Ordinal),
        "Driver coaching answer should fall back when no data exists.");
}

static void TrackGuideZoneMapperMapsMonzaZones()
{
    Assert(TrackGuideWebCatalog.TryGetGuide("Monza", out var guide), "Fixture catalog should provide Monza guide.");
    Assert(
        TrackGuideZoneMapper.MapZoneLabel("Zone 1", guide) == "Rettifilo",
        "Monza Zone 1 should map to Rettifilo.");
    Assert(
        TrackGuideZoneMapper.MapZoneLabel("Zone 2", guide) == "Lesmo 1",
        "Monza Zone 2 should map to Lesmo 1.");
    Assert(
        TrackGuideZoneMapper.MapZoneLabel("Zone 3", guide) == "Lesmo 2",
        "Monza Zone 3 should map to Lesmo 2.");
    Assert(
        TrackGuideZoneMapper.MapZoneLabel("Zone 4", guide) == "Ascari",
        "Monza Zone 4 should map to Ascari.");
    Assert(
        TrackGuideZoneMapper.MapZoneLabel("Zone 5", guide) == "Parabolica",
        "Monza Zone 5 should map to Parabolica.");
    Assert(
        TrackGuideZoneMapper.MapZoneLabel("Zone 4", null) == "Zone 4",
        "Without a track guide, Zone labels should remain unchanged.");
}

static void TrackGuideZoneMapperMapsMonzaZoneSix()
{
    Assert(TrackGuideWebCatalog.TryGetGuide("Monza", out var guide), "Fixture catalog should provide Monza guide.");
    var zone6 = new PerformanceZone(6, "Zone 6", 6, 0.88, 0.96);
    Assert(
        TrackGuideZoneMapper.MapZoneLabel(zone6, guide) == "Parabolica",
        "Monza Zone 6 should map to Parabolica via estimated lap progress.");
    Assert(
        TrackGuideZoneMapper.MapZoneReferences("Zone 6: late braking (0.2s)", guide).Contains("Parabolica", StringComparison.OrdinalIgnoreCase),
        "Stored Zone 6 weakness text should map to Parabolica.");
}

static void StoredSessionMemoryWatchAnswerMapsRawZones()
{
    Assert(TrackGuideWebCatalog.TryGetGuide("Monza", out var guide), "Fixture catalog should provide Monza guide.");
    var stored = new SessionMemorySummary(
        Guid.NewGuid(),
        "monza|gt3",
        "Monza",
        "GT3",
        "Practice",
        DateTimeOffset.UtcNow.AddDays(-2),
        112.4,
        112.8,
        78,
        2.1,
        null,
        null,
        [],
        [],
        ["Zone 1", "Zone 4: unstable throttle (0.3s)", "Zone 6: late braking (0.2s)"],
        [],
        [],
        []);

    var watch = SessionMemoryCoachingFormatter.BuildWatchAnswer(stored, guide);
    Assert(
        !watch.Contains("Zone 1", StringComparison.OrdinalIgnoreCase),
        "Watch answer should not expose Zone 1 when corner mapping exists.");
    Assert(
        !watch.Contains("Zone 4", StringComparison.OrdinalIgnoreCase),
        "Watch answer should not expose Zone 4 when corner mapping exists.");
    Assert(
        !watch.Contains("Zone 6", StringComparison.OrdinalIgnoreCase),
        "Watch answer should not expose Zone 6 when corner mapping exists.");
    Assert(
        watch.Contains("Rettifilo", StringComparison.OrdinalIgnoreCase),
        "Watch answer should include Rettifilo for Zone 1.");
    Assert(
        watch.Contains("Parabolica", StringComparison.OrdinalIgnoreCase),
        "Watch answer should include Parabolica for Zone 6.");
}

static void CoachQueryPipelineMapsStoredZonesWithoutCachedGuide()
{
    Assert(TrackGuideWebCatalog.TryGetGuide("Monza", out _), "Fixture catalog should provide Monza guide.");
    var stored = new SessionMemorySummary(
        Guid.NewGuid(),
        "monza|gt3",
        "Monza",
        "GT3",
        "Practice",
        DateTimeOffset.UtcNow.AddDays(-1),
        112.4,
        112.8,
        78,
        2.1,
        null,
        null,
        [],
        [],
        ["Zone 1: unstable braking (0.2s)", "Zone 4: unstable throttle (0.3s)"],
        [],
        ["Zone 6: late braking (0.2s)"],
        []);

    var session = new SessionState();
    var context = new CoachContext(
        RacePrepPlan: new RacePrepPlan("GT3", "Monza", "Practice", null, null, null, null, null, null, null),
        PreviousStoredSessionMemory: stored,
        CachedTrackGuide: null);
    var evidence = new CoachEvidenceBuilder().Build(new CoachEvidenceInput(
        session,
        PreviousStoredSessionMemory: stored,
        RaceContext: new LiveRaceContext(
            "Monza",
            null,
            "GT3",
            null,
            "Practice",
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            RaceContextConfidence.Partial,
            new RaceFieldDiagnostics([], [], "test"))));

    var pipeline = CoachQueryPipeline.Resolve(
        session,
        "what should i watch on this track",
        new CoachEngine(),
        context,
        evidence);

    Assert(
        !pipeline.FinalDisplayedText.Contains("Zone 1", StringComparison.OrdinalIgnoreCase),
        "Pipeline watch answer should not expose Zone 1 without cached guide.");
    Assert(
        !pipeline.FinalDisplayedText.Contains("Zone 4", StringComparison.OrdinalIgnoreCase),
        "Pipeline watch answer should not expose Zone 4 without cached guide.");
    Assert(
        !pipeline.FinalDisplayedText.Contains("Zone 6", StringComparison.OrdinalIgnoreCase),
        "Pipeline watch answer should not expose Zone 6 without cached guide.");
}

static int CountNumberedPriorityLines(string content) =>
    content.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
        .Count(line => line.Length >= 3 && char.IsDigit(line[0]) && line[1] == '.' && line[2] == ' ');

static void CoachResponsePrioritizerLimitsDefaultAnswer()
{
    var packets = new CoachEvidencePacket[]
    {
        new("Knowledge", "Catalog tip", "Info", 0.4, CoachEvidenceSourceType.Knowledge, null, [], null, "Carry minimum speed through Parabolica."),
        new("WebResearch", "Research note", "Info", 0.5, CoachEvidenceSourceType.Knowledge, null, [], null, "Cached research: attack curb at T1."),
        new("TrackMemory", "Stored weakness", "Info", 0.7, CoachEvidenceSourceType.Session, null, [], null, "Zone 4: unstable throttle (0.3s)."),
        new("Performance", "Weak zone", "Warning", 0.85, CoachEvidenceSourceType.Analytics, 3, [], 0.31, "Zone 2 brake release costs 0.31s."),
        new("Telemetry", "Live delta", "Warning", 0.92, CoachEvidenceSourceType.Telemetry, 3, [], 0.18, "Live telemetry: losing 0.18s in Sector 2."),
    };
    var message = new CoachMessage(
        "coach",
        "Live telemetry: losing 0.18s in Sector 2. Focus on brake release in Zone 2.",
        [],
        null,
        packets);
    var evidence = new CoachEvidenceBundle(packets);

    var compact = CoachResponsePrioritizer.Apply(message, "where am i losing time", evidence);

    Assert(
        compact.Content.StartsWith(CoachResponsePrioritizer.PrioritiesHeader, StringComparison.Ordinal),
        "Default coach answer should use Top priorities header.");
    Assert(
        compact.Content.Contains(CoachResponsePrioritizer.SupportingEvidenceNote, StringComparison.Ordinal),
        "Default coach answer should note supporting evidence is available.");
    Assert(
        CountNumberedPriorityLines(compact.Content) <= CoachResponsePrioritizer.MaxPriorityPoints,
        "Default coach answer should include at most three numbered priorities.");
    Assert(
        compact.Content.Contains("Live telemetry", StringComparison.OrdinalIgnoreCase),
        "Highest-priority live telemetry should appear in compact answer.");
    Assert(
        compact.EvidencePackets.Count == packets.Length,
        "Prioritization should preserve evidence packets internally.");
}

static void CoachResponsePrioritizerDetailedModePreservesFullAnswer()
{
    var content = "Live telemetry: losing 0.18s in Sector 2."
        + $"{Environment.NewLine}{Environment.NewLine}Evidence:{Environment.NewLine}- Zone 2 brake release costs 0.31s.";
    var message = new CoachMessage("coach", content, [], null, []);
    var evidence = CoachEvidenceBundle.Empty;

    var detailed = CoachResponsePrioritizer.Apply(message, "explain why am i losing time", evidence);

    Assert(
        detailed.Content == content,
        "Detailed mode should preserve the full coach answer and embedded evidence.");
    Assert(
        CoachResponsePrioritizer.IsDetailedModeRequest("show evidence for sector 2"),
        "Show evidence phrasing should enable detailed mode.");
}

static void CoachQueryPipelinePrioritizesCommonCoachQueries()
{
    var coach = new CoachEngine();
    var (session, performance, performanceContext) = SessionWithPerformanceContext();
    var evidence = BuildSampleCoachEvidence(session);
    Assert(performance.Availability == "Available", "Performance analysis should be available for pipeline prioritization.");

    var losingTime = CoachQueryPipeline.Resolve(
        session,
        "where am i losing time",
        coach,
        performanceContext,
        evidence);
    Assert(
        losingTime.FinalDisplayedText.Contains(CoachResponsePrioritizer.PrioritiesHeader, StringComparison.Ordinal),
        "Pipeline losing-time answer should be compact and actionable first.");
    Assert(
        losingTime.FinalWritten.EvidencePackets.Count > 0,
        "Pipeline should retain evidence packets for detailed follow-up.");

    var improvement = CoachQueryPipeline.Resolve(
        session,
        "what should i improve",
        coach,
        performanceContext,
        evidence);
    Assert(
        improvement.FinalDisplayedText.Contains(CoachResponsePrioritizer.PrioritiesHeader, StringComparison.Ordinal)
            || improvement.FinalDisplayedText.Contains(CoachResponsePrioritizer.SupportingEvidenceNote, StringComparison.Ordinal),
        "Pipeline improvement answer should prioritize concise coaching points.");

    var watchContext = performanceContext with
    {
        RacePrepPlan = new RacePrepPlan("GT3", "Monza", "Practice", null, null, null, null, null, null, null)
    };
    var watch = CoachQueryPipeline.Resolve(
        session,
        "what should i watch on this track",
        coach,
        watchContext,
        evidence);
    Assert(
        watch.FinalDisplayedText.Contains(CoachResponsePrioritizer.PrioritiesHeader, StringComparison.Ordinal)
            || watch.FinalDisplayedText.Contains(CoachResponsePrioritizer.SupportingEvidenceNote, StringComparison.Ordinal),
        "Pipeline watch answer should lead with concise track priorities.");

    var detailed = CoachQueryPipeline.Resolve(
        session,
        "explain why am i losing time",
        coach,
        performanceContext,
        evidence);
    Assert(
        !detailed.FinalDisplayedText.StartsWith(CoachResponsePrioritizer.PrioritiesHeader, StringComparison.Ordinal),
        "Detailed mode should skip compact prioritization.");
}

static SessionContextAssessment StableRaceStrategyContext() =>
    new(
        SessionPhase.Race,
        VehicleActivity.OnTrack,
        "Race / On track",
        StrategyConfidenceLevel.High,
        "High",
        true,
        true,
        true,
        true,
        true,
        true,
        true,
        "Stable samples.");

static RaceStrategyIntelligenceRecommendation BuildRaceStrategyIntelligence(
    SessionState session,
    SessionContextAssessment? context = null,
    SessionTyreIntelligence? tyreIntelligence = null,
    LiveRaceContext? raceContext = null,
    string? query = null)
{
    var snapshots = BuildLapIntelligenceSnapshots(session);
    var analytics = new TelemetryAnalyticsService().Analyze(new SessionAnalyticsInput(session, snapshots));
    var lapIntelligence = new LapIntelligenceService().Analyze(new LapIntelligenceInput(session, snapshots));
    var strategy = new StrategyEngine().Analyze(new StrategyInput(session, analytics, lapIntelligence));
    return RaceStrategyIntelligenceService.Build(
        new RaceStrategyIntelligenceInput(
            session,
            session.LatestSnapshot,
            snapshots,
            analytics,
            lapIntelligence,
            strategy,
            tyreIntelligence,
            null,
            null,
            null,
            null,
            context ?? StableRaceStrategyContext(),
            raceContext),
        query);
}

static void RaceStrategyIntelligenceEnoughFuel()
{
    var engine = new EventEngine();
    var session = new SessionState();
    Apply(session, engine, Packet("""{"lap_progress":0.99,"fuel":30.0}"""));
    Apply(session, engine, Packet("""{"lap_progress":0.01,"lap_time_s":90.0,"fuel":28.0}"""));
    Apply(session, engine, Packet("""{"lap_progress":0.99,"fuel":28.0}"""));
    Apply(session, engine, Packet("""{"lap_progress":0.01,"lap_time_s":89.0,"fuel":26.0}"""));

    var intelligence = BuildRaceStrategyIntelligence(session);

    Assert(intelligence.Fuel.HasData, "Race strategy fuel intelligence should be available with fuel samples.");
    Assert(
        intelligence.Fuel.Recommendation.Contains("No fuel saving required", StringComparison.OrdinalIgnoreCase),
        $"Enough fuel scenario should not require fuel saving. Actual: '{intelligence.Fuel.Recommendation}'");
    Assert(
        intelligence.Fuel.CurrentBurnRatePerLap is > 0,
        "Race strategy should report a telemetry-based burn rate.");
    Assert(
        intelligence.Fuel.ProjectedLapsRemaining is > 5,
        "Comfortable fuel load should project more than five laps.");
}

static void RaceStrategyIntelligenceNotEnoughFuel()
{
    var engine = new EventEngine();
    var session = new SessionState();
    Apply(session, engine, Packet("""{"lap_progress":0.99,"fuel":4.0}"""));
    Apply(session, engine, Packet("""{"lap_progress":0.01,"lap_time_s":90.0,"fuel":3.0}"""));
    Apply(session, engine, Packet("""{"lap_progress":0.99,"fuel":3.0}"""));
    Apply(session, engine, Packet("""{"lap_progress":0.01,"lap_time_s":89.0,"fuel":1.0}"""));

    var intelligence = BuildRaceStrategyIntelligence(
        session,
        raceContext: new LiveRaceContext(
            "Monza",
            null,
            "GT3",
            null,
            "Race",
            20,
            12,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            RaceContextConfidence.Partial,
            new RaceFieldDiagnostics([], [], "test")));

    Assert(intelligence.Fuel.HasData, "Low fuel scenario should still compute fuel strategy from telemetry.");
    Assert(
        intelligence.Fuel.FuelSavingRequired || !intelligence.Fuel.CanFinishSafely,
        "Low fuel scenario should flag saving required or unsafe finish.");
}

static void RaceStrategyTyreDegradationWarning()
{
    var engine = new EventEngine();
    var session = new SessionState();
    var snapshots = new List<TelemetrySnapshot>();
    for (var index = 0; index < 12; index++)
    {
        var temp = 80 + index;
        Apply(session, engine, Packet($$"""{"speed_kmh":145,"lap_progress":0.35,"tyre_temp_c":[{{temp}},{{temp + 1}},{{temp + 4}},{{temp + 5}}]}"""));
        if (session.LatestSnapshot is not null)
        {
            snapshots.Add(session.LatestSnapshot);
        }
    }

    Apply(session, engine, Packet("""{"lap_progress":0.99,"fuel":8.0}"""));
    Apply(session, engine, Packet("""{"lap_progress":0.01,"lap_time_s":90.0,"fuel":6.0}"""));
    Apply(session, engine, Packet("""{"lap_progress":0.99,"fuel":6.0}"""));
    Apply(session, engine, Packet("""{"lap_progress":0.01,"lap_time_s":89.5,"fuel":4.0}"""));

    var tyreIntelligence = new TyreIntelligenceService().Analyze(new TyreIntelligenceInput(
        session,
        snapshots,
        session.Events,
        VehicleActivity.OnTrack,
        SessionPhase.Race));
    var intelligence = BuildRaceStrategyIntelligence(session, tyreIntelligence: tyreIntelligence);

    Assert(intelligence.Tyre.HasData, "Tyre strategy should be available with temperature history.");
    Assert(
        intelligence.Tyre.OutlookSummary.Contains("temperature", StringComparison.OrdinalIgnoreCase)
            || intelligence.Tyre.EstimatedPerformanceDropLapsMin is not null,
        "Tyre strategy should warn about degradation or temperature trend.");
}

static void RaceStrategyPitRecommendationAvailable()
{
    var (session, _) = SessionWithFuelEstimate();
    var intelligence = BuildRaceStrategyIntelligence(session);

    Assert(intelligence.Pit.HasData, "Pit recommendation should be available with stable fuel and stint data.");
    Assert(
        intelligence.Pit.EarliestSensibleStopLap is > 0 && intelligence.Pit.LatestSensibleStopLap is > 0,
        "Pit recommendation should include a sensible stop window.");
}

static void RaceStrategyInsufficientDataFallback()
{
    var intelligence = RaceStrategyIntelligenceService.Build(
        new RaceStrategyIntelligenceInput(
            new SessionState(),
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null));
    Assert(
        !intelligence.Fuel.HasData,
        "Insufficient fuel samples should disable fuel strategy.");
    Assert(
        intelligence.Fuel.Availability.Contains("two completed fuel samples", StringComparison.OrdinalIgnoreCase),
        "Insufficient data should return the required fuel sample message.");

    var coach = new CoachEngine();
    var answer = coach.Answer(new SessionState(), "how many laps left");
    Assert(
        answer.Content.Contains("two completed fuel samples", StringComparison.OrdinalIgnoreCase),
        "Coach laps-left query should surface insufficient telemetry fallback.");
}

static void EngineerAiContextContainsStrategyFacts()
{
    var (session, _) = SessionWithFuelEstimate();
    var intelligence = BuildRaceStrategyIntelligence(session);
    var context = new CoachContext(
        SessionContext: StableRaceStrategyContext(),
        RaceStrategyIntelligence: intelligence);
    var aiContext = EngineerAiContextBuilder.Build(
        session,
        "can i finish",
        context,
        CoachEvidenceBundle.Empty);

    Assert(
        aiContext.Facts.Any(fact => fact.Summary == "Strategy summary"),
        "AI context should include strategy summary for strategy queries.");
    Assert(
        aiContext.Facts.Any(fact => fact.Summary == "Fuel outlook"),
        "AI context should include fuel outlook for strategy queries.");
    Assert(
        aiContext.Facts.Any(fact => fact.Summary == "Fuel burn"),
        "AI context should include fuel burn fact for strategy queries.");
}

static void ReviewModeTrackResolutionUsesReviewSessionTrack()
{
    Assert(TrackGuideWebCatalog.TryGetGuide("Monza", out _), "Fixture catalog should provide Monza guide.");

    var (_, trace) = TrackGuideZoneMapper.ResolveTrackNameWithTrace(
        raceContext: LiveRaceContext.Unavailable("test"),
        prepPlan: new RacePrepPlan("GT3", null, "Practice", null, null, null, null, null, null, null),
        reviewSessionTrack: "Monza");
    Assert(trace.ReviewTrack == "Monza", "Review session track should be captured in resolution trace.");
    Assert(trace.TrackName == "Monza", "Review session track should win over empty prep plan.");
    Assert(trace.GuideResolved == "Monza", "Monza guide should resolve from review session track.");
    Assert(trace.GuideSource == "catalog", "Review mode should use catalog guide when cache is empty.");

    var stale = new DriverCoachingRecommendation(
        true,
        "Available",
        DriverProgressTrend.Stable,
        "Weakest area: Zone 1.",
        "Zone 1: abrupt brake release",
        "Zone 5",
        "Zone 1: abrupt brake release",
        "Consistency spread 0.4s",
        null,
        [],
        ["Smooth brake release into Zone 1."],
        [],
        "Weakest area: Zone 1.");

    var (session, _, performanceContext) = SessionWithPerformanceContext();
    var context = new CoachContext(
        RacePrepPlan: new RacePrepPlan("GT3", null, "Practice", null, null, null, null, null, null, null),
        DriverCoaching: stale,
        CachedTrackGuide: null,
        ReviewSessionTrack: "Monza",
        RecentSnapshots: performanceContext.RecentSnapshots);
    var evidence = new CoachEvidenceBuilder().Build(new CoachEvidenceInput(
        session,
        ReviewSessionTrack: "Monza",
        DriverCoaching: stale));

    var pipeline = CoachQueryPipeline.Resolve(
        session,
        "where am i losing time",
        new CoachEngine(),
        context,
        evidence);

    Assert(
        pipeline.FinalTtsPayload.Contains("Rettifilo", StringComparison.OrdinalIgnoreCase)
            || pipeline.FinalDisplayedText.Contains("Rettifilo", StringComparison.OrdinalIgnoreCase),
        "Review mode losing-time answer should map Zone 1 to Rettifilo.");
    Assert(
        !pipeline.FinalTtsPayload.Contains("Zone 1", StringComparison.OrdinalIgnoreCase),
        "Review mode TTS should not expose Zone 1 when review track resolves Monza guide.");
}

static void TrackGuideCatalogMapsRedBullRingAliases()
{
    foreach (var alias in new[]
             {
                 "Red Bull Ring",
                 "RedBullRing",
                 "Spielberg",
                 "RBR",
                 "Red-Bull-Ring",
                 "rb_ring",
                 "ks_red_bull_ring"
             })
    {
        Assert(
            TrackGuideWebCatalog.TryGetGuide(alias, out var guide),
            $"Catalog should resolve Red Bull Ring alias '{alias}'.");
        Assert(
            guide.TrackName == "Red Bull Ring",
            $"Alias '{alias}' should canonicalize to Red Bull Ring.");
    }

    Assert(TrackGuideWebCatalog.TryGetGuide("Spielberg", out var rbrGuide), "Red Bull Ring fixture guide required.");
    Assert(
        TrackGuideZoneMapper.MapZoneLabel("Zone 1", rbrGuide) == "Niki Lauda Kurve",
        "Red Bull Ring Zone 1 should map to Niki Lauda Kurve.");
    Assert(
        TrackGuideZoneMapper.MapZoneLabel("Zone 3", rbrGuide) == "Remus",
        "Red Bull Ring Zone 3 should map to Remus.");
    Assert(
        TrackGuideZoneMapper.MapZoneLabel("Zone 6", rbrGuide) == "Würth",
        "Red Bull Ring Zone 6 should map to Würth.");

    var (_, trace) = TrackGuideZoneMapper.ResolveTrackNameWithTrace(
        reviewSessionTrack: "Spielberg");
    Assert(trace.TrackName == "Red Bull Ring", "Spielberg should canonicalize to Red Bull Ring.");
    Assert(trace.GuideResolved == "Red Bull Ring", "Red Bull Ring guide should resolve from Spielberg.");
    Assert(trace.GuideSource == "catalog", "Red Bull Ring should use catalog source.");
    Assert(trace.GuideStatus == "available", "Red Bull Ring guide status should be available.");
}

static void TrackGuideDoesNotUseMonzaForUnknownTrack()
{
    Assert(
        !TrackGuideWebCatalog.TryGetGuide("Unknown Circuit XYZ", out _),
        "Unknown track should not resolve to any catalog guide.");

    var monzaGuide = TrackGuideWebCatalog.TryGetGuide("Monza", out var guide) ? guide : null;
    Assert(monzaGuide is not null, "Monza fixture guide required.");

    var (resolvedGuide, source) = TrackGuideZoneMapper.ResolveGuideWithSource(
        monzaGuide,
        [TrackGuideMapper.ToKnowledgeSource(monzaGuide!, KnowledgeSourceTypes.Web)],
        "Unknown Circuit XYZ");
    Assert(resolvedGuide is null, "Unknown track must not reuse Monza cached guide.");
    Assert(source == "none", "Unknown track guide source should remain none.");

    var mapped = TrackGuideZoneMapper.MapZoneReferences("Your main loss is in Zone 1.", resolvedGuide);
    Assert(
        mapped.Contains("Zone 1", StringComparison.OrdinalIgnoreCase),
        "Unknown track should keep Zone tokens when guide is unavailable.");

    var (_, trace) = TrackGuideZoneMapper.ResolveTrackNameWithTrace();
    Assert(trace.GuideStatus == "track-unresolved", "Missing track should report track-unresolved status.");
}

static void TrackGuideRejectsMismatchedCachedGuide()
{
    Assert(TrackGuideWebCatalog.TryGetGuide("Monza", out var monzaGuide), "Monza fixture guide required.");
    Assert(TrackGuideWebCatalog.TryGetGuide("Spielberg", out var rbrGuide), "Red Bull Ring fixture guide required.");

    var (guide, source) = TrackGuideZoneMapper.ResolveGuideWithSource(
        monzaGuide,
        null,
        "Red Bull Ring");
    Assert(guide?.TrackName == "Red Bull Ring", "Red Bull Ring track should resolve Red Bull Ring catalog guide.");
    Assert(
        source is "catalog" or "catalog-over-cache",
        "Mismatched Monza cache should be ignored for Red Bull Ring track.");
    Assert(
        TrackGuideZoneMapper.MapZoneLabel("Zone 1", guide) == "Niki Lauda Kurve",
        "Red Bull Ring review should not map zones using Monza corners.");
    Assert(
        TrackGuideZoneMapper.MapZoneLabel("Zone 1", monzaGuide) == "Rettifilo",
        "Monza guide should still map Monza zones correctly.");
}

static void SessionDebriefMapsStoredRawZones()
{
    Assert(TrackGuideWebCatalog.TryGetGuide("Monza", out _), "Fixture catalog should provide Monza guide.");
    var summary = new SessionMemorySummary(
        Guid.NewGuid(),
        "monza|gt3",
        "Monza",
        "GT3",
        "Practice",
        DateTimeOffset.UtcNow.AddDays(-3),
        112.4,
        112.8,
        78,
        2.1,
        null,
        null,
        [],
        ["Zone 4: unstable throttle (0.3s)"],
        ["Zone 6: late braking (0.2s)"],
        [],
        ["Zone 1: unstable braking (0.2s)"],
        []);

    var debrief = SessionDebriefGenerator.Generate(summary);
    var combined = string.Join(" ", debrief.Weaknesses.Concat(debrief.NextSessionActions));
    Assert(
        !combined.Contains("Zone 1", StringComparison.OrdinalIgnoreCase),
        "Debrief should not expose Zone 1 when Monza corner map exists.");
    Assert(
        !combined.Contains("Zone 4", StringComparison.OrdinalIgnoreCase),
        "Debrief should not expose Zone 4 when Monza corner map exists.");
    Assert(
        !combined.Contains("Zone 6", StringComparison.OrdinalIgnoreCase),
        "Debrief should not expose Zone 6 when Monza corner map exists.");
    Assert(
        combined.Contains("Ascari", StringComparison.OrdinalIgnoreCase),
        "Debrief should map Zone 4 to Ascari.");
    Assert(
        combined.Contains("Parabolica", StringComparison.OrdinalIgnoreCase),
        "Debrief should map Zone 6 to Parabolica.");
}

static void LapDeltaSanityRejectsImpossibleTraceDeltas()
{
    Assert(
        !LapDeltaSanity.IsReasonableLapDelta(368.666),
        "368.666s point delta should be rejected as impossible.");
    Assert(
        LapDeltaSanity.IsReasonableLapDelta(1.842, 105),
        "Small point delta within lap duration should remain valid.");

    var selectedStart = DateTimeOffset.UtcNow;
    var bestStart = selectedStart.AddMinutes(10);
    var selected = new[]
    {
        SnapshotAt(selectedStart, 0.0),
        SnapshotAt(selectedStart.AddSeconds(50), 0.5),
        SnapshotAt(selectedStart.AddSeconds(100), 1.0)
    };
    var best = new[]
    {
        SnapshotAt(bestStart, 0.0),
        SnapshotAt(bestStart.AddSeconds(48), 0.5),
        SnapshotAt(bestStart.AddSeconds(96), 1.0)
    };

    var builder = new TelemetryTraceBuilder();
    var session = new SessionState();
    session.ApplySnapshot(selected[0], []);
    session.ApplySnapshot(selected[^1], []);
    var timeline = builder.Build(new TelemetryTimelineInput(
        session,
        selected.Concat(best).ToArray(),
        SelectedLapNumber: 1));

    var deltaRow = timeline.Rows.FirstOrDefault(row => row.Name == "Delta");
    Assert(deltaRow is not null, "Delta trace row should be generated.");
    var worst = deltaRow!.Series.SelectMany(series => series.Points).Max(point => Math.Abs(point.Value));
    Assert(
        worst < 10,
        "Lap-relative delta trace should not inherit absolute timestamp offsets between laps.");
}

static void DriverCoachingAnswerRemapsStaleContextCoaching()
{
    Assert(TrackGuideWebCatalog.TryGetGuide("Monza", out var guide), "Fixture catalog should provide Monza guide.");
    var stale = new DriverCoachingRecommendation(
        true,
        "Available",
        DriverProgressTrend.Stable,
        "Progress is stable versus your recent baseline.",
        "Zone 1: unstable braking",
        "Zone 5",
        "Zone 1: unstable braking",
        "Consistency spread 0.4s",
        null,
        [],
        ["Smooth brake release into Zone 1.", "Smooth brake release into Zone 3."],
        ["Weakest area: Zone 1."],
        "Weakest area: Zone 1. Strongest area: Zone 5.");

    var session = new SessionState();
    var context = new CoachContext(
        RacePrepPlan: new RacePrepPlan("GT3", "Monza", "Practice", null, null, null, null, null, null, null),
        DriverCoaching: stale,
        CachedTrackGuide: null);
    var coach = new CoachEngine();
    var answer = coach.Answer(session, "where am i losing time", context);

    Assert(
        !answer.Content.Contains("Zone 1", StringComparison.OrdinalIgnoreCase),
        "Stale context coaching should be remapped before losing-time answer is returned.");
    Assert(
        answer.Content.Contains("Rettifilo", StringComparison.OrdinalIgnoreCase),
        "Stale Zone 1 coaching should map to Rettifilo on Monza.");
}

static void ZoneMapperPrefersCatalogOverCachedZonePlaceholders()
{
    var cached = new TrackGuide(
        TrackGuide.IdForTrack("Monza"),
        "Monza",
        [],
        null,
        null,
        5,
        [],
        [],
        [],
        [],
        [],
        [],
        [],
        [],
        [],
        [],
        [],
        [
            new TrackGuideCorner("Zone 1", 0.06, 0.14),
            new TrackGuideCorner("Zone 2", 0.20, 0.30),
            new TrackGuideCorner("Zone 3", 0.34, 0.44),
            new TrackGuideCorner("Zone 4", 0.50, 0.62),
            new TrackGuideCorner("Zone 5", 0.85, 0.96)
        ],
        [],
        DateTimeOffset.UtcNow,
        null,
        "stale-cache");

    Assert(
        TrackGuideZoneMapper.UsesZonePlaceholderCorners(cached),
        "Cached guide with Zone corner labels should be detected as placeholders.");

    var (guide, source) = TrackGuideZoneMapper.ResolveGuideWithSource(cached, null, "Monza");
    Assert(
        source == "catalog-over-cache",
        "Zone mapping should prefer catalog over cached placeholder corners.");
    Assert(
        TrackGuideZoneMapper.MapZoneLabel("Zone 1", guide) == "Rettifilo",
        "Catalog guide should map Zone 1 to Rettifilo even when stale cache exists.");

    var staleCoaching = new DriverCoachingRecommendation(
        true,
        "Available",
        DriverProgressTrend.Stable,
        "Weakest area: Zone 1.",
        "Zone 1: unstable braking",
        "Zone 5",
        "Zone 1: unstable braking",
        "Consistency spread 0.4s",
        null,
        [],
        ["Smooth brake release into Zone 3."],
        [],
        "Weakest area: Zone 1.");

    var mapped = TrackGuideZoneMapper.MapDriverCoachingRecommendation(staleCoaching, guide, "Test");
    Assert(
        !mapped.Summary.Contains("Zone 1", StringComparison.OrdinalIgnoreCase),
        "Mapped coaching summary should not retain Zone 1 when catalog guide is used.");
    Assert(
        mapped.TopCoachingTargets.Any(item => item.Contains("Lesmo 2", StringComparison.OrdinalIgnoreCase)),
        "Zone 3 target should map to Lesmo 2 on Monza.");
}

static TelemetrySnapshot SnapshotAt(DateTimeOffset timestamp, double progress) =>
    Packet($$$"""{"lap_progress":{{{progress.ToString(CultureInfo.InvariantCulture)}}},"speed_kmh":180,"throttle":0.5,"brake":0.2}""")
        with
        {
            Timestamp = timestamp,
            Lap = Packet("""{"lap_progress":0.1}""").Lap with { LapNumber = 1, LapProgress = progress }
        };

static void DriverCoachingUsesTrackGuideCornerNames()
{
    Assert(TrackGuideWebCatalog.TryGetGuide("Monza", out var guide), "Fixture catalog should provide Monza guide.");
    var zone4 = new PerformanceZone(4, "Zone 4", 2, 0.50, 0.65);
    var performance = SessionWithPerformanceContext().Performance with
    {
        ZoneMetrics =
        [
            new ZonePerformanceMetric(zone4, 0.18, ["delayed throttle pickup (0.05s/lap)"])
        ],
        MainWeakness = "Zone 4: delayed throttle pickup",
        CoachingMessages =
        [
            "You are losing time on corner exits, mainly from delayed throttle pickup in Zone 4."
        ]
    };

    var coaching = DriverCoachingIntelligenceService.Build(new DriverCoachingInput(
        performance,
        TrackGuide: guide));
    var losingTimeAnswer = DriverCoachingAnswerBuilder.BuildLosingTime(coaching);
    var insight = coaching.CoachingInsights.FirstOrDefault(item =>
        item.Contains("delayed throttle pickup", StringComparison.OrdinalIgnoreCase));

    Assert(
        !losingTimeAnswer.Contains("Zone 4", StringComparison.OrdinalIgnoreCase),
        "Losing-time answer should not expose raw zone identifiers when a corner map exists.");
    Assert(
        losingTimeAnswer.Contains("Ascari", StringComparison.OrdinalIgnoreCase),
        "Losing-time answer should use the mapped Monza corner name.");
    Assert(
        insight is not null && insight.Contains("Ascari", StringComparison.OrdinalIgnoreCase),
        "Driver coaching insights should use mapped corner names.");
    Assert(
        !insight!.Contains("Zone 4", StringComparison.OrdinalIgnoreCase),
        "Driver coaching insights should not expose raw zone identifiers.");

    var (session, _, context) = SessionWithPerformanceContext();
    var coach = new CoachEngine();
    var coachAnswer = coach.Answer(
        session,
        "where am i losing time",
        context with
        {
            DriverPerformance = performance,
            CachedTrackGuide = guide,
            DriverCoaching = coaching
        });
    Assert(
        !coachAnswer.Content.Contains("Zone 4", StringComparison.OrdinalIgnoreCase),
        "Coach losing-time route should not expose raw zone identifiers.");
    Assert(
        coachAnswer.Content.Contains("Ascari", StringComparison.OrdinalIgnoreCase),
        "Coach losing-time route should use mapped corner names.");
}

static void EngineerAiContextIncludesDriverCoachingSummary()
{
    var (session, performance, context) = SessionWithPerformanceContext();
    var coaching = DriverCoachingIntelligenceService.Build(new DriverCoachingInput(performance));
    var enrichedContext = context with { DriverCoaching = coaching };
    var evidence = new CoachEvidenceBuilder().Build(new CoachEvidenceInput(
        session,
        BuildLapIntelligenceSnapshots(session),
        session.Events,
        context.Analytics,
        DriverPerformance: performance,
        DriverCoaching: coaching));

    var aiContext = EngineerAiContextBuilder.Build(session, "what should I improve", enrichedContext, evidence);

    Assert(
        aiContext.Facts.Any(fact =>
            fact.Topic == "DriverCoaching"
                && fact.Summary == "Coaching summary"),
        "AI context should include driver coaching summary facts.");
}

static void TyreAnswerIncludesAllCornersOrUnavailableRear()
{
    var coach = new CoachEngine();
    var session = new SessionState();
    session.ApplySnapshot(Packet("""{"lap_progress":0.2,"tyre_temp_c":[88,91,90,89],"tyre_pressure":[26.1,26.4,26.0,26.2],"tyre_wear":[0.01,0.02,0.01,0.01]}"""), []);

    var fullData = coach.Answer(session, "kakve su gume");
    Assert(fullData.Content.Contains("Front-left", StringComparison.OrdinalIgnoreCase), "Tyre answer should include front-left.");
    Assert(fullData.Content.Contains("Front-right", StringComparison.OrdinalIgnoreCase), "Tyre answer should include front-right.");
    Assert(fullData.Content.Contains("Rear-left", StringComparison.OrdinalIgnoreCase), "Tyre answer should include rear-left.");
    Assert(fullData.Content.Contains("Rear-right", StringComparison.OrdinalIgnoreCase), "Tyre answer should include rear-right.");

    var partialSession = new SessionState();
    partialSession.ApplySnapshot(Packet("""{"lap_progress":0.2,"tyre_temp_c":[52,54]}"""), []);
    var partial = coach.Answer(partialSession, "how are my tyres");
    Assert(
        partial.Content.Contains("Rear tyre data is unavailable", StringComparison.OrdinalIgnoreCase),
        "Missing rear tyre data should be explicit.");
}

static TelemetrySnapshot StampTraceSnapshot(double progress, int lapNumber)
{
    return Packet($$$"""{"lap_progress":{{{progress.ToString(CultureInfo.InvariantCulture)}}},"speed_kmh":140,"throttle":0.6,"brake":0.1,"tyre_temp_c":[80,81,79,80]}""")
        with
        {
            Timestamp = DateTimeOffset.UtcNow.AddSeconds(progress * 100),
            Lap = Packet("""{"lap_progress":0.1}""").Lap with { LapNumber = lapNumber, LapProgress = progress }
        };
}

static bool LooksLikeFuelAnswer(string content)
{
    return content.Contains(" L fuel", StringComparison.Ordinal)
        || content.Contains("liters.", StringComparison.OrdinalIgnoreCase)
        || content.Contains("liters fuel", StringComparison.OrdinalIgnoreCase)
        || content.Contains("goriv", StringComparison.OrdinalIgnoreCase)
        || content.Contains("latest fuel:", StringComparison.OrdinalIgnoreCase);
}

static void EndToEndFixtureReplayVerifiesPipeline()
{
    var fixturePath = SampleThreeLapFixture.ResolvePath();
    Assert(File.Exists(fixturePath), $"Committed fixture not found: {fixturePath}");
    var result = LocalEndToEndReplay.Run(fixturePath);
    LocalEndToEndReplay.AssertPipeline(result);

    var lastSnapshot = result.Snapshots.Last();
    var raceContext = RaceContextService.Build(result.Session, lastSnapshot);
    Assert(raceContext.TrackName == "Spa", "Fixture replay should expose parsed track name.");
    Assert(raceContext.Position == 8, "Fixture replay should expose parsed race position.");
    Assert(raceContext.Confidence != RaceContextConfidence.Unavailable, "Partial race context should not be unavailable.");
    Assert(
        raceContext.Diagnostics.MissingFields.Contains("gap_ahead_s"),
        "Fixture replay should report missing opponent gap fields in diagnostics.");

    var performance = new DriverPerformanceIntelligenceService().Analyze(new DriverPerformanceInput(
        result.Session,
        result.Snapshots,
        result.Session.Events));
    Assert(performance.ValidLapCount >= 2, "Fixture replay should produce multiple valid laps.");
    Assert(performance.Availability == "Available", "Fixture replay should produce performance intelligence.");
    Assert(performance.ZoneCount > 0, "Fixture replay should segment analysis zones.");
}

static void TelemetryTraceBuilderCreatesDeterministicTimeline()
{
    var (session, _) = SessionWithFuelEstimate();
    var snapshots = BuildLapIntelligenceSnapshots(session);
    var builder = new TelemetryTraceBuilder();
    var input = new TelemetryTimelineInput(session, snapshots, session.Events, session.LastLap?.LapNumber, 0.5);
    var first = builder.Build(input);
    var second = builder.Build(input);

    Assert(first.Rows.Count == second.Rows.Count, "Trace rows should be deterministic.");
    Assert(first.Rows.Any(row => row.Name == "Throttle"), "Throttle trace should be generated.");
    Assert(first.Rows.Any(row => row.Name == "Delta"), "Delta trace should be generated when best lap overlay exists.");
    Assert(first.Markers.Any(marker => marker.Category == "Sector"), "Sector markers should be generated.");
    Assert(first.Rows.Sum(row => row.Series.Count) > 0, "Timeline should contain trace series.");
}


static async Task ReceiverAcceptsOnlyValidPacketsOnDefaultEndpoint()
{
    using var portHolder = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));
    var port = ((IPEndPoint)portHolder.Client.LocalEndPoint!).Port;
    portHolder.Close();
    await using var receiver = new TelemetryReceiver("127.0.0.1", port);
    var packets = new List<TelemetryPacketResult>();
    var snapshots = new List<TelemetrySnapshot>();
    var validSnapshotReceived = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

    receiver.PacketProcessed += (_, packet) =>
    {
        lock (packets)
        {
            packets.Add(packet);
        }
    };
    receiver.SnapshotReceived += (_, snapshot) =>
    {
        lock (snapshots)
        {
            snapshots.Add(snapshot);
        }

        validSnapshotReceived.TrySetResult();
    };

    await receiver.StartAsync();

    using var sender = new UdpClient();
    await SendUdp(sender, port, """{"schema":"acevo_engineer.simhub_datacore","schema_version":1""");
    await SendUdp(sender, port, """{"schema":"wrong","schema_version":1,"speed_kmh":50}""");
    await SendUdp(sender, port, """{"schema":"acevo_engineer.simhub_datacore","schema_version":1,"speed_kmh":123.4,"tyre_temp_c":[88,89,87,86]}""");

    var completed = await Task.WhenAny(validSnapshotReceived.Task, Task.Delay(TimeSpan.FromSeconds(3)));
    Assert(completed == validSnapshotReceived.Task, $"Receiver did not emit a valid snapshot on 127.0.0.1:{port}.");
    await Task.Delay(150);

    receiver.Stop();

    lock (packets)
    {
        Assert(packets.Count >= 3, "Receiver should process raw UDP packets.");
        Assert(packets.Count(item => item.IsValid) == 1, "Receiver should count only one valid packet.");
        Assert(packets.Count(item => !item.IsValid) >= 2, "Receiver should count invalid packets.");
        Assert(packets.Any(item => item.Warning?.Contains("Invalid JSON", StringComparison.Ordinal) == true), "Invalid JSON warning was not reported.");
        Assert(packets.Any(item => item.Warning?.Contains("Unsupported schema", StringComparison.Ordinal) == true), "Wrong schema warning was not reported.");
    }

    lock (snapshots)
    {
        Assert(snapshots.Count == 1, "Receiver should emit snapshots only for valid packets.");
        Assert(snapshots[0].Car.SpeedKmh == 123.4, "Receiver snapshot did not carry normalized telemetry.");
    }
}

static string CaptureFileWithPackets(IEnumerable<string> rawPackets)
{
    var path = Path.Combine(Path.GetTempPath(), $"race-engineer-replay-{Guid.NewGuid():N}.jsonl");
    var capture = new RawPacketCapture(path, maxPackets: 100);
    capture.SetEnabled(true);
    var timestamp = DateTimeOffset.UtcNow;
    foreach (var raw in rawPackets)
    {
        var valid = SimHubPacketParser.TryParse(raw, out var snapshot, out var warning);
        capture.Record(new TelemetryPacketResult(timestamp, valid, snapshot, warning, raw, snapshot?.Source.Schema ?? SimHubPacketParser.ReadEnvelope(raw).Schema, snapshot?.Source.SchemaVersion ?? SimHubPacketParser.ReadEnvelope(raw).SchemaVersion));
        timestamp = timestamp.AddMilliseconds(100);
    }

    return path;
}

static TelemetryPacketResult PacketResult(DateTimeOffset timestamp, bool valid, string raw, string? warning = null)
{
    var envelope = SimHubPacketParser.ReadEnvelope(raw);
    SimHubPacketParser.TryParse(raw, out var snapshot, out var parseWarning);
    return new TelemetryPacketResult(timestamp, valid, valid ? snapshot : null, warning ?? parseWarning, raw, snapshot?.Source.Schema ?? envelope.Schema, snapshot?.Source.SchemaVersion ?? envelope.SchemaVersion);
}

static string ValidRaw(string jsonBody)
{
    var body = jsonBody.Trim();
    if (body.StartsWith('{') && body.EndsWith('}'))
    {
        body = body[1..^1];
    }

    return $$"""{"schema":"acevo_engineer.simhub_datacore","schema_version":1,{{body}}}""";
}

static async Task SendUdp(UdpClient sender, int port, string json)
{
    var bytes = Encoding.UTF8.GetBytes(json);
    await sender.SendAsync(bytes, new IPEndPoint(IPAddress.Parse("127.0.0.1"), port));
}

static TelemetrySnapshot Packet(string jsonBody)
{
    var body = jsonBody.Trim();
    if (body.StartsWith('{') && body.EndsWith('}'))
    {
        body = body[1..^1];
    }

    return SimHubPacketParser.Parse($$"""{"schema":"acevo_engineer.simhub_datacore","schema_version":1,{{body}}}""");
}

static TelemetrySnapshot PacketAt(string jsonBody, DateTimeOffset timestamp)
{
    return Packet(jsonBody) with { Timestamp = timestamp };
}

static void Apply(SessionState session, EventEngine engine, TelemetrySnapshot snapshot)
{
    session.ApplySnapshot(snapshot, engine.Process(snapshot));
}

static (SessionState Session, EventEngine Engine) SessionWithFuelEstimate()
{
    var engine = new EventEngine();
    var session = new SessionState();
    Apply(session, engine, Packet("""{"lap_progress":0.99,"fuel":10.0}"""));
    Apply(session, engine, Packet("""{"lap_progress":0.01,"lap_time_s":90.0,"fuel":8.0}"""));
    Apply(session, engine, Packet("""{"lap_progress":0.99,"fuel":8.0}"""));
    Apply(session, engine, Packet("""{"lap_progress":0.01,"lap_time_s":89.0,"fuel":5.0}"""));
    return (session, engine);
}

static TelemetryEvent Event(EventType type, EventSeverity severity, DateTimeOffset timestamp)
{
    return new TelemetryEvent(
        Guid.NewGuid(),
        type,
        severity,
        0.9,
        timestamp,
        0.5,
        1,
        new Dictionary<string, object?> { ["test"] = true },
        "Test action.");
}

static void Assert(bool condition, string message)
{
    if (!condition)
    {
        throw new InvalidOperationException(message);
    }
}

sealed class SlowMockEngineerAiProvider : IEngineerAiProvider
{
    public string Name => "slow-mock";

    public bool IsEnabled => true;

    public async Task<EngineerAiResult> GenerateAnswerAsync(EngineerAiRequest request, CancellationToken cancellationToken)
    {
        await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken);
        return EngineerAiResult.Succeeded("Too late.", Name);
    }
}

sealed class MockOpenAiHttpHandler : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var payload =
            """{"choices":[{"message":{"content":"Braking looks stable on the latest lap."}}]}""";
        return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(payload, Encoding.UTF8, "application/json")
        });
    }
}
