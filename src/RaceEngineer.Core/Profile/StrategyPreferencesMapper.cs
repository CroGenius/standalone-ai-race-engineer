using RaceEngineer.Core.Coaching;

namespace RaceEngineer.Core.Profile;

public static class StrategyPreferencesMapper
{
    public static Strategy.StrategyEngineOptions ToEngineOptions(StrategyPreferencesRecord preferences)
    {
        var normalized = UserPreferencesNormalizer.NormalizeStrategy(preferences);
        var pitAggression = normalized.PitRecommendationAggressiveness;
        var tyreSensitivity = normalized.TyreRiskSensitivity;
        var pitPreference = normalized.PitStrategyPreference;
        var margin = normalized.FuelSafetyMarginLaps;

        return new Strategy.StrategyEngineOptions
        {
            FuelModerateLapsRemaining = 4.0 + margin + PitOffset(pitAggression, conservative: 1.0, aggressive: -0.5),
            FuelHighLapsRemaining = 2.0 + margin + PitOffset(pitAggression, conservative: 0.5, aggressive: -0.5),
            FuelCriticalLapsRemaining = 1.0 + (margin * 0.5),
            PitWindowLeadLaps = PitWindowLead(pitAggression, pitPreference),
            PitWindowShiftLaps = PitWindowShift(pitPreference),
            TyreRiskModerateScore = TyreThreshold(tyreSensitivity, low: 35, normal: 25, high: 15),
            TyreRiskHighScore = TyreThreshold(tyreSensitivity, low: 60, normal: 50, high: 40),
            TyreRiskCriticalScore = TyreThreshold(tyreSensitivity, low: 85, normal: 70, high: 55)
        };
    }

    public static TimeSpan CalloutCooldown(StrategyPreferencesRecord preferences, CoachPreferencesRecord coachPreferences)
    {
        var baseSeconds = 25d;
        var strategyFactor = preferences.PitRecommendationAggressiveness switch
        {
            "conservative" => 1.15,
            "aggressive" => 0.85,
            _ => 1.0
        };
        var coachFactor = coachPreferences.CalloutAggressiveness switch
        {
            "low" => 1.4,
            "high" => 0.75,
            _ => 1.0
        };
        return TimeSpan.FromSeconds(baseSeconds * strategyFactor * coachFactor);
    }

    private static double PitOffset(string aggressiveness, double conservative, double aggressive)
    {
        return aggressiveness switch
        {
            "conservative" => conservative,
            "aggressive" => aggressive,
            _ => 0.0
        };
    }

    private static int PitWindowLead(string aggressiveness, string pitPreference)
    {
        var lead = aggressiveness switch
        {
            "conservative" => 2,
            "aggressive" => 4,
            _ => 3
        };

        return pitPreference switch
        {
            "undercut" => lead + 1,
            "overcut" => Math.Max(1, lead - 1),
            _ => lead
        };
    }

    private static int PitWindowShift(string pitPreference)
    {
        return pitPreference switch
        {
            "undercut" => -1,
            "overcut" => 1,
            _ => 0
        };
    }

    private static double TyreThreshold(string sensitivity, double low, double normal, double high)
    {
        return sensitivity switch
        {
            "low" => low,
            "high" => high,
            _ => normal
        };
    }
}
