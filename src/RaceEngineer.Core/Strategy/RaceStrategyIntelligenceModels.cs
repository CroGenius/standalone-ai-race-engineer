using RaceEngineer.Core.Coaching;
using RaceEngineer.Core.Knowledge;
using RaceEngineer.Core.RaceAwareness;

namespace RaceEngineer.Core.Strategy;

public enum RaceStrategyConfidenceLevel
{
    Low,
    Medium,
    High
}

public sealed record FuelStrategyRecommendation(
    bool HasData,
    string Availability,
    double? CurrentBurnRatePerLap,
    double? ConservativeBurnRatePerLap,
    double? FuelRemainingLiters,
    double? ProjectedLapsRemaining,
    double? ProjectedFuelAtFinish,
    double? FuelTargetPerLap,
    double? FuelMarginLiters,
    bool CanFinishSafely,
    bool FuelSavingRequired,
    string Recommendation,
    RaceStrategyConfidenceLevel Confidence,
    string ConfidenceLabel,
    IReadOnlyList<string> EvidenceLines)
{
    public static FuelStrategyRecommendation Unavailable(string reason) => new(
        false,
        reason,
        null,
        null,
        null,
        null,
        null,
        null,
        null,
        false,
        false,
        reason,
        RaceStrategyConfidenceLevel.Low,
        "Low",
        [reason]);
}

public sealed record TyreStrategyRecommendation(
    bool HasData,
    string Availability,
    string WearTrend,
    string TemperatureTrend,
    string PressureTrend,
    string DegradationRisk,
    string OutlookSummary,
    int? EstimatedPerformanceDropLapsMin,
    int? EstimatedPerformanceDropLapsMax,
    bool StintViable,
    RaceStrategyConfidenceLevel Confidence,
    string ConfidenceLabel,
    IReadOnlyList<string> EvidenceLines)
{
    public static TyreStrategyRecommendation Unavailable(string reason) => new(
        false,
        reason,
        "Unknown",
        "Unknown",
        "Unknown",
        "Unknown",
        reason,
        null,
        null,
        false,
        RaceStrategyConfidenceLevel.Low,
        "Low",
        [reason]);
}

public sealed record PitStrategyRecommendation(
    bool HasData,
    string Availability,
    int? EarliestSensibleStopLap,
    int? LatestSensibleStopLap,
    PitRecommendation Recommendation,
    string CurrentRisk,
    string ExpectedGainLoss,
    string RecommendationSummary,
    RaceStrategyConfidenceLevel Confidence,
    string ConfidenceLabel,
    IReadOnlyList<string> EvidenceLines)
{
    public static PitStrategyRecommendation Unavailable(string reason) => new(
        false,
        reason,
        null,
        null,
        PitRecommendation.Unknown,
        "Unknown",
        "Unavailable without supporting telemetry.",
        reason,
        RaceStrategyConfidenceLevel.Low,
        "Low",
        [reason]);
}

public sealed record StintRiskAssessment(
    string OverallRisk,
    string FuelRisk,
    string TyreRisk,
    string Summary,
    RaceStrategyConfidenceLevel Confidence,
    string ConfidenceLabel);

public sealed record RaceStrategyIntelligenceRecommendation(
    bool HasData,
    string Availability,
    FuelStrategyRecommendation Fuel,
    TyreStrategyRecommendation Tyre,
    PitStrategyRecommendation Pit,
    StintRiskAssessment StintRisk,
    string StrategySummary,
    RaceStrategyConfidenceLevel Confidence,
    string ConfidenceLabel,
    IReadOnlyList<string> EvidenceLines)
{
    public const string InsufficientFuelSamplesMessage =
        "Fuel strategy unavailable. Need at least two completed fuel samples.";

    public static RaceStrategyIntelligenceRecommendation Unavailable(string reason) => new(
        false,
        reason,
        FuelStrategyRecommendation.Unavailable(reason),
        TyreStrategyRecommendation.Unavailable(reason),
        PitStrategyRecommendation.Unavailable(reason),
        new StintRiskAssessment("Unknown", "Unknown", "Unknown", reason, RaceStrategyConfidenceLevel.Low, "Low"),
        reason,
        RaceStrategyConfidenceLevel.Low,
        "Low",
        [reason]);
}

public sealed record RaceStrategyIntelligenceInput(
    Session.SessionState Session,
    Telemetry.TelemetrySnapshot? LatestSnapshot,
    IReadOnlyList<Telemetry.TelemetrySnapshot>? RecentSnapshots,
    Analytics.SessionTelemetryAnalytics? Analytics,
    Analytics.SessionLapIntelligence? LapIntelligence,
    Strategy.SessionStrategy? Strategy,
    Analytics.SessionTyreIntelligence? TyreIntelligence,
    StrategyKnowledgeRecommendation? StrategyKnowledge,
    Knowledge.TrackCarKnowledgeRecommendation? TrackCarKnowledge,
    SessionMemorySummary? PreviousSessionMemory,
    Knowledge.WebResearchBundle? CachedWebResearch,
    SessionContext.SessionContextAssessment? SessionContext,
    RaceAwareness.LiveRaceContext? RaceContext,
    int? RequestedLapCount = null);
