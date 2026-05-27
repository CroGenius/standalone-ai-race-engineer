namespace RaceEngineer.Core.Profile;

public sealed record UserPreferencesBundle(
    DriverProfileRecord Driver,
    CoachPreferencesRecord Coach,
    StrategyPreferencesRecord Strategy)
{
    public static UserPreferencesBundle FromAppSettings(AppSettings appSettings)
    {
        return new UserPreferencesBundle(
            DriverProfileRecord.Default,
            CoachPreferencesRecord.FromAppSettings(appSettings),
            StrategyPreferencesRecord.Default);
    }
}
