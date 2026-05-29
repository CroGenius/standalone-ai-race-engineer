using RaceEngineer.Core;
using RaceEngineer.Core.SessionContext;

namespace RaceEngineer.Core.Knowledge;

public sealed record TrackResearchOptions(
    bool Enabled,
    string Provider,
    bool AllowTrackResearchDuringDriving,
    int TrackGuideRefreshDays)
{
    public static TrackResearchOptions Disabled { get; } = new(false, "disabled", false, 30);

    public static TrackResearchOptions FromAppSettings(AppSettings settings) =>
        new(
            settings.TrackResearchEnabled,
            NormalizeProvider(settings.TrackResearchProvider),
            settings.AllowTrackResearchDuringDriving,
            ClampRefreshDays(settings.TrackGuideRefreshDays));

    public bool WebFetchAllowed => Enabled && Provider == "web";

    private static string NormalizeProvider(string? configuredProvider)
    {
        if (string.IsNullOrWhiteSpace(configuredProvider))
        {
            return "disabled";
        }

        var normalized = configuredProvider.Trim().ToLowerInvariant();
        return normalized is "disabled" or "local" or "web" ? normalized : "disabled";
    }

    private static int ClampRefreshDays(int days) => days is < 1 or > 365 ? 30 : days;
}

public sealed record TrackResearchContext(
    VehicleActivity Activity,
    bool IsReviewMode)
{
    public bool IsActiveDriving =>
        !IsReviewMode
        && Activity is VehicleActivity.OnTrack
            or VehicleActivity.OutLap
            or VehicleActivity.InLap;
}
