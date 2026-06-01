namespace RaceEngineer.Core.Knowledge;

public sealed record WebResearchOptions(
    bool Enabled,
    bool AllowWebResearchDuringDriving,
    int ResearchRefreshDays)
{
    public static WebResearchOptions Disabled { get; } = new(false, false, 30);

    public static WebResearchOptions FromAppSettings(AppSettings settings) =>
        new(
            settings.WebResearchEnabled || settings.TrackResearchEnabled,
            settings.AllowWebResearchDuringDriving || settings.AllowTrackResearchDuringDriving,
            ClampRefreshDays(settings.ResearchRefreshDays > 0
                ? settings.ResearchRefreshDays
                : settings.TrackGuideRefreshDays));

    public bool WebFetchAllowed => Enabled;

    private static int ClampRefreshDays(int days) => days is < 1 or > 365 ? 30 : days;
}

public sealed record WebResearchContext(
    SessionContext.VehicleActivity Activity,
    bool IsReviewMode)
{
    public bool IsActiveDriving =>
        !IsReviewMode
        && Activity is SessionContext.VehicleActivity.OnTrack
            or SessionContext.VehicleActivity.OutLap
            or SessionContext.VehicleActivity.InLap;
}
