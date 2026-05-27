namespace RaceEngineer.Core.Profile;

public sealed record CoachingPreferencesPayload(
    CoachPreferencesRecord? Coach = null,
    StrategyPreferencesRecord? Strategy = null);
