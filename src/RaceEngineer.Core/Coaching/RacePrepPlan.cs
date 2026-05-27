namespace RaceEngineer.Core.Coaching;

public sealed record RacePrepPlan(
    string? Car,
    string? Track,
    string? SessionType,
    string? TargetStintLength,
    string? FuelPlan,
    string? TyrePlan,
    string? PracticeGoal,
    string? DriverReminders,
    string? SetupNotes,
    string? StrategyNotes)
{
    public bool IsEmpty =>
        string.IsNullOrWhiteSpace(Car)
        && string.IsNullOrWhiteSpace(Track)
        && string.IsNullOrWhiteSpace(SessionType)
        && string.IsNullOrWhiteSpace(TargetStintLength)
        && string.IsNullOrWhiteSpace(FuelPlan)
        && string.IsNullOrWhiteSpace(TyrePlan)
        && string.IsNullOrWhiteSpace(PracticeGoal)
        && string.IsNullOrWhiteSpace(DriverReminders)
        && string.IsNullOrWhiteSpace(SetupNotes)
        && string.IsNullOrWhiteSpace(StrategyNotes);
}

public sealed record PostSessionReport(string Markdown);
