namespace RaceEngineer.Core.Profile;

public sealed record DriverProfileRecord(
    string DriverId = "default",
    string? DriverName = null,
    string PreferredLanguage = "auto",
    string PreferredUnits = "metric",
    string ExperienceLevel = "intermediate",
    string DrivingStyle = "balanced",
    IReadOnlyList<string>? Strengths = null,
    IReadOnlyList<string>? FocusAreas = null,
    IReadOnlyList<string>? Reminders = null)
{
    public static DriverProfileRecord Default { get; } = new();
}
