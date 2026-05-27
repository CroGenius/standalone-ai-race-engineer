namespace RaceEngineer.Core.Profile;

public sealed record StrategyPreferencesRecord(
    double FuelSafetyMarginLaps = 1.0,
    string PitRecommendationAggressiveness = "normal",
    string TyreRiskSensitivity = "normal",
    string PitStrategyPreference = "balanced")
{
    public static StrategyPreferencesRecord Default { get; } = new();
}
