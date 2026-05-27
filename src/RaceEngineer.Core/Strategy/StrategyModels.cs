namespace RaceEngineer.Core.Strategy;

public enum FuelRiskLevel
{
    Unknown,
    Low,
    Moderate,
    High,
    Critical
}

public enum PitRecommendation
{
    Unknown,
    StayOut,
    PrepareToPit,
    PitNow
}

public enum TyreRiskLevel
{
    Unknown,
    Low,
    Moderate,
    High,
    Critical
}

public sealed record FuelPredictionMetric(
    double? FuelUsedPerLap,
    double? LapsRemaining,
    double? EstimatedFinishFuel,
    FuelRiskLevel RiskLevel,
    string Availability);

public sealed record PitStrategyMetric(
    int? EstimatedPitWindowStartLap,
    int? EstimatedPitWindowEndLap,
    int? StintLengthEstimateLaps,
    PitRecommendation Recommendation,
    double? MinimumFuelToFinish,
    string RecommendationReason,
    string Availability);

public sealed record TyreRiskMetric(
    TyreRiskLevel RiskLevel,
    double RiskScore0To100,
    IReadOnlyList<string> Factors,
    string Availability);

public sealed record StrategyCalloutSignal(string Message, string ReasonCode);

public sealed record SessionStrategy(
    FuelPredictionMetric Fuel,
    PitStrategyMetric Pit,
    TyreRiskMetric TyreRisk,
    string Summary,
    StrategyCalloutSignal? CalloutSignal)
{
    public static SessionStrategy Empty { get; } = new(
        new FuelPredictionMetric(null, null, null, FuelRiskLevel.Unknown, "No fuel samples yet."),
        new PitStrategyMetric(null, null, null, PitRecommendation.Unknown, null, "Unavailable.", "Need strategy inputs."),
        new TyreRiskMetric(TyreRiskLevel.Unknown, 0, [], "Need completed laps."),
        "Strategy unavailable until fuel usage and lap data are available.",
        null);
}

public sealed record StrategyInput(
    Session.SessionState Session,
    Analytics.SessionTelemetryAnalytics? Analytics = null,
    Analytics.SessionLapIntelligence? LapIntelligence = null,
    Coaching.RacePrepPlan? PrepPlan = null,
    IReadOnlyList<Events.TelemetryEvent>? Events = null,
    Profile.StrategyPreferencesRecord? Preferences = null);

public sealed class StrategyEngineOptions
{
    public double FuelCriticalLevel { get; init; } = 3.0;
    public double FuelHighLevel { get; init; } = 5.0;
    public double FuelModerateLapsRemaining { get; init; } = 4.0;
    public double FuelHighLapsRemaining { get; init; } = 2.0;
    public double FuelCriticalLapsRemaining { get; init; } = 1.0;
    public int DefaultTargetStintLaps { get; init; } = 15;
    public int PitWindowLeadLaps { get; init; } = 3;
    public double TyreRiskModerateScore { get; init; } = 25;
    public double TyreRiskHighScore { get; init; } = 50;
    public double TyreRiskCriticalScore { get; init; } = 70;
    public int PitWindowShiftLaps { get; init; }
}
