using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Text;
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
TelemetryTraceBuilderCreatesDeterministicTimeline();
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

    Assert(answer.Content.Contains("max tyre temp: 91.0 C", StringComparison.Ordinal), "Tyre answer should include factual max tyre temperature.");
    Assert(answer.Content.Contains("Evidence:", StringComparison.Ordinal), "Tyre answer should include evidence bullets.");
}

static void CoachTyreStatusWithoutDataSaysUnavailable()
{
    var coach = new CoachEngine();
    var session = new SessionState();
    session.ApplySnapshot(Packet("""{"lap_progress":0.2,"speed_kmh":100}"""), []);

    var answer = coach.Answer(session, "tyre status");

    Assert(answer.Content.Contains("unavailable", StringComparison.OrdinalIgnoreCase), "Missing tyre data should be explicit.");
    Assert(answer.Uncertainty is not null, "Missing tyre data should set uncertainty.");
}

static void CoachFuelStatusUsesLatestFuelAndEstimate()
{
    var coach = new CoachEngine();
    var (session, _) = SessionWithFuelEstimate();

    var answer = coach.Answer(session, "fuel status");

    Assert(answer.Content.Contains("latest fuel: 5.0", StringComparison.Ordinal), "Fuel answer should include latest fuel.");
    Assert(answer.Content.Contains("fuel used per lap: 2.50", StringComparison.Ordinal), "Fuel answer should include fuel-per-lap estimate when reliable.");
    Assert(answer.Content.Contains("estimated laps remaining: 2.0", StringComparison.Ordinal), "Fuel answer should include estimated laps remaining when reliable.");
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

    var spoken = voice.Speak("Fuel is low. Start saving.");

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
    Assert(output.SpokenTexts.Count == 1, "Spoken query should produce one spoken response when unmuted.");
    Assert(!output.SpokenTexts[0].Contains("Evidence:", StringComparison.Ordinal), "Spoken response should be shorter than written chat response.");
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
        RacePrepPlan: new RacePrepPlan(null, "Monza", "Practice", null, null, null, null, null, null, null),
        KnowledgeSources: [
            KnowledgeSource.ManualNote("Monza guide", "Watch front locking into heavy braking zones.", track: "Monza", category: "track")
        ]);

    var answer = coach.Answer(new SessionState(), "What should I watch for at Monza?", context);

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
    await using var receiver = new TelemetryReceiver();
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
    await SendUdp(sender, """{"schema":"acevo_engineer.simhub_datacore","schema_version":1""");
    await SendUdp(sender, """{"schema":"wrong","schema_version":1,"speed_kmh":50}""");
    await SendUdp(sender, """{"schema":"acevo_engineer.simhub_datacore","schema_version":1,"speed_kmh":123.4,"tyre_temp_c":[88,89,87,86]}""");

    var completed = await Task.WhenAny(validSnapshotReceived.Task, Task.Delay(TimeSpan.FromSeconds(3)));
    Assert(completed == validSnapshotReceived.Task, "Receiver did not emit a valid snapshot on 127.0.0.1:20999.");
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

static async Task SendUdp(UdpClient sender, string json)
{
    var bytes = Encoding.UTF8.GetBytes(json);
    await sender.SendAsync(bytes, new IPEndPoint(IPAddress.Parse("127.0.0.1"), 20999));
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
