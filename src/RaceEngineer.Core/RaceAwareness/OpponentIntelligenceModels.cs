namespace RaceEngineer.Core.RaceAwareness;

public enum GapTrendDirection
{
    Unavailable,
    Stable,
    Gaining,
    Losing
}

public enum BattleConfidence
{
    Low,
    Medium,
    High
}

public enum RaceBattleType
{
    Unavailable,
    IsolatedRunning,
    TrainOfCars,
    AttackOpportunity,
    DefensiveSituation
}

public sealed record OpponentSnapshot(
    int? Position,
    int? TotalCars,
    string? CarAhead,
    string? CarBehind,
    double? GapAheadSeconds,
    double? GapBehindSeconds,
    int? CurrentLap)
{
    public bool HasOpponentData =>
        GapAheadSeconds.HasValue
        || GapBehindSeconds.HasValue
        || !string.IsNullOrWhiteSpace(CarAhead)
        || !string.IsNullOrWhiteSpace(CarBehind)
        || Position.HasValue;
}

public sealed record GapTrend(
    GapTrendDirection AheadDirection,
    GapTrendDirection BehindDirection,
    double? GapAheadChangePerLapSeconds,
    double? GapBehindChangePerLapSeconds,
    BattleConfidence Confidence,
    string Summary)
{
    public static GapTrend Unavailable { get; } = new(
        GapTrendDirection.Unavailable,
        GapTrendDirection.Unavailable,
        null,
        null,
        BattleConfidence.Low,
        "Gap trend is unavailable from telemetry.");
}

public sealed record RaceBattle(
    RaceBattleType Type,
    BattleConfidence Confidence,
    string StatusLabel,
    string Summary)
{
    public static RaceBattle Unavailable { get; } = new(
        RaceBattleType.Unavailable,
        BattleConfidence.Low,
        "unavailable",
        "Race battle status is unavailable from telemetry.");
}

public sealed record OpponentHistory(
    IReadOnlyList<OpponentSnapshot> RecentSnapshots,
    GapTrend GapTrend,
    RaceBattle Battle);

public sealed record OpponentStrategyInsight(
    bool HasData,
    string? UndercutOpportunity,
    string? OvercutOpportunity,
    string? RiskAssessment);

public sealed record OpponentIntelligenceRecommendation(
    bool HasOpponentData,
    OpponentSnapshot? Current,
    GapTrend GapTrend,
    RaceBattle Battle,
    OpponentStrategyInsight StrategyInsight,
    string? AttackZoneRecommendation,
    string? DefendZoneRecommendation,
    IReadOnlyList<string> StoredOvertakeZones,
    IReadOnlyList<string> StoredDefensiveWeaknesses,
    IReadOnlyList<string> StoredBattleOutcomes,
    string Summary)
{
    public static OpponentIntelligenceRecommendation Unavailable { get; } = new(
        false,
        null,
        GapTrend.Unavailable,
        RaceBattle.Unavailable,
        new OpponentStrategyInsight(false, null, null, null),
        null,
        null,
        [],
        [],
        [],
        "Opponent data is unavailable from telemetry.");
}

public sealed record OpponentIntelligenceInput(
    LiveRaceContext? RaceContext,
    IReadOnlyList<Telemetry.TelemetrySnapshot>? RecentSnapshots = null,
    Knowledge.TrackGuide? TrackGuide = null,
    Strategy.SessionStrategy? Strategy = null,
    TrackMemoryRecord? TrackMemory = null);
