namespace RaceEngineer.Core.Telemetry;

public sealed record SourceInfo(
    string Provider,
    string? Schema,
    int? SchemaVersion);

public sealed record CarState(
    double? SpeedKmh,
    double? Rpm,
    int? Gear,
    object? Damage);

public sealed record DriverInputs(
    double? Throttle,
    double? Brake,
    double? Steering);

public sealed record LapState(
    int? LapTimeMs,
    double? LapTimeS,
    double? LapProgress,
    int? LapNumber,
    bool? ValidLap);

public sealed record RaceState(
    int? Position,
    object? Flags);

public sealed record TyreBrakeFuelState(
    double? Fuel,
    IReadOnlyList<double?>? TyreTempC,
    IReadOnlyList<double?>? TyrePressure,
    IReadOnlyList<double?>? BrakeTempC,
    IReadOnlyList<double?>? TyreWear);

public sealed record TelemetrySnapshot(
    Guid Id,
    DateTimeOffset Timestamp,
    SourceInfo Source,
    CarState Car,
    DriverInputs Inputs,
    LapState Lap,
    RaceState Race,
    TyreBrakeFuelState Condition)
{
    public static TelemetrySnapshot Empty(string provider = "manual") =>
        new(
            Guid.NewGuid(),
            DateTimeOffset.UtcNow,
            new SourceInfo(provider, null, null),
            new CarState(null, null, null, null),
            new DriverInputs(null, null, null),
            new LapState(null, null, null, null, null),
            new RaceState(null, null),
            new TyreBrakeFuelState(null, null, null, null, null));
}

