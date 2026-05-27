namespace RaceEngineer.Core.Session;

public sealed record CompletedLap(
    int LapNumber,
    DateTimeOffset StartedAt,
    DateTimeOffset EndedAt,
    TimeSpan? Duration,
    bool IsValid,
    double? FuelStart,
    double? FuelEnd)
{
    public double? FuelUsed => FuelStart.HasValue && FuelEnd.HasValue && FuelStart.Value >= FuelEnd.Value
        ? FuelStart.Value - FuelEnd.Value
        : null;
}

