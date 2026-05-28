using RaceEngineer.Core.Analytics;
using RaceEngineer.Core.Coaching;
using RaceEngineer.Core.Events;
using RaceEngineer.Core.Session;
using RaceEngineer.Core.Strategy;
using RaceEngineer.Core.Telemetry;
using RaceEngineer.Core.TelemetryVisualization;

namespace RaceEngineer.SmokeTests;

internal sealed record LocalEndToEndReplayResult(
    PacketReplayResult Replay,
    SessionState Session,
    int EventsProduced,
    IReadOnlyList<TelemetrySnapshot> Snapshots,
    SessionTelemetryAnalytics Analytics,
    SessionLapIntelligence LapIntelligence,
    SessionStrategy Strategy,
    TelemetryTimeline Timeline,
    CoachEvidenceBundle Evidence,
    IReadOnlyDictionary<string, CoachMessage> CoachAnswers);

internal static class LocalEndToEndReplay
{
    private static readonly string[] CoachQuestions =
    [
        "where am I losing time?",
        "how is my braking?",
        "how is my throttle?",
        "what should I improve?",
        "what is my strategy?"
    ];

    public static LocalEndToEndReplayResult Run(string fixturePath)
    {
        var engine = new EventEngine();
        var session = new SessionState();
        var snapshots = new List<TelemetrySnapshot>();
        var eventsProduced = 0;

        var replay = new PacketReplayTool().Replay(
            fixturePath,
            eventEngine: engine,
            session: session,
            onSnapshotReceived: snapshot =>
            {
                var events = engine.Process(snapshot);
                eventsProduced += events.Count;
                session.ApplySnapshot(snapshot, events);
                snapshots.Add(snapshot);
            });

        var analysisSnapshots = PrepareAnalysisSnapshots(session, snapshots);
        var analytics = new TelemetryAnalyticsService().Analyze(new SessionAnalyticsInput(session, snapshots));
        var lapIntelligence = new LapIntelligenceService().Analyze(new LapIntelligenceInput(
            session,
            analysisSnapshots,
            session.LastLap?.LapNumber));
        var strategy = new StrategyEngine().Analyze(new StrategyInput(
            session,
            analytics,
            lapIntelligence,
            null,
            session.Events));
        var timeline = new TelemetryTraceBuilder().Build(new TelemetryTimelineInput(
            session,
            analysisSnapshots,
            session.Events,
            session.LastLap?.LapNumber));
        var evidence = new CoachEvidenceBuilder().Build(new CoachEvidenceInput(
            session,
            snapshots,
            session.Events,
            analytics,
            lapIntelligence,
            null,
            null,
            strategy,
            timeline));

        var coach = new CoachEngine();
        var answers = CoachQuestions.ToDictionary(
            question => question,
            question => coach.Answer(session, question, evidence: evidence));

        return new LocalEndToEndReplayResult(
            replay,
            session,
            eventsProduced,
            snapshots,
            analytics,
            lapIntelligence,
            strategy,
            timeline,
            evidence,
            answers);
    }

    public static void AssertPipeline(LocalEndToEndReplayResult result)
    {
        Assert(result.Replay.PacketsRead > 0, "Fixture replay should read packets.");
        Assert(result.Replay.ValidPackets > 0, "Fixture replay should parse valid packets.");
        Assert(result.Snapshots.Count == result.Replay.ValidPackets, "Replay should collect parsed snapshots.");
        Assert(result.EventsProduced > 0, "Replay should produce telemetry events.");
        Assert(result.Session.CompletedLaps.Count >= 3, "Fixture should complete at least 3 laps.");
        Assert(result.Session.Events.Any(item => item.Type == EventType.InvalidLapOrFlags), "Fixture should include invalid lap / flag evidence.");
        Assert(result.Session.BestLap?.Duration == TimeSpan.FromSeconds(88), "Fixture best lap should be 88.0 seconds.");

        Assert(result.Analytics.LapConsistency.SampleCount >= 2, "Analytics should compute lap consistency samples.");
        Assert(result.Analytics.BestVsAverage.BestLapSeconds is > 0, "Analytics should compute best vs average.");
        Assert(result.LapIntelligence.LapComparison.BestLapSeconds is > 0, "Lap intelligence should compute lap comparison.");
        Assert(result.Strategy.Fuel.FuelUsedPerLap is > 0, "Strategy should compute fuel used per lap from fixture.");
        Assert(result.Strategy.Pit.Recommendation != PitRecommendation.Unknown, "Strategy should produce a pit recommendation.");
        Assert(
            result.LapIntelligence.SectorDeltas.Sectors.Count > 0
                || result.LapIntelligence.CoachingInsights.Count > 0
                || result.LapIntelligence.PaceDecay.Availability == "Available",
            $"Lap intelligence should compute non-empty results (sectors={result.LapIntelligence.SectorDeltas.Sectors.Count}, insights={result.LapIntelligence.CoachingInsights.Count}, pace={result.LapIntelligence.PaceDecay.Availability}).");
        Assert(result.Timeline.Rows.Count >= 4, "Telemetry timeline should generate trace rows.");
        Assert(result.Evidence.Packets.Count > 0, "Coach evidence builder should produce packets.");

        foreach (var question in CoachQuestions)
        {
            var answer = result.CoachAnswers[question];
            Assert(answer.EvidencePackets.Count > 0, $"Coach answer for '{question}' should include evidence packets.");
        }
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }

    private static IReadOnlyList<TelemetrySnapshot> PrepareAnalysisSnapshots(
        SessionState session,
        IReadOnlyList<TelemetrySnapshot> snapshots)
    {
        var prepared = new List<TelemetrySnapshot>();
        var validLaps = session.CompletedLaps
            .Where(lap => lap.IsValid && lap.Duration.HasValue)
            .OrderBy(lap => lap.LapNumber)
            .ToArray();

        foreach (var lap in validLaps)
        {
            var lapSnapshots = snapshots
                .Where(item => item.Timestamp >= lap.StartedAt
                    && item.Timestamp <= lap.EndedAt
                    && item.Lap.LapProgress.HasValue)
                .ToArray();
            var hasNearFinish = lapSnapshots.Any(item => item.Lap.LapProgress >= 0.90);

            foreach (var snapshot in lapSnapshots)
            {
                if (hasNearFinish && snapshot.Lap.LapProgress <= 0.10)
                {
                    continue;
                }

                prepared.Add(snapshot with
                {
                    Lap = snapshot.Lap with { LapNumber = lap.LapNumber }
                });
            }
        }

        return prepared;
    }
}
