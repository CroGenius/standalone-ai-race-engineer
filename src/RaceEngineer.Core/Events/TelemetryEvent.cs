namespace RaceEngineer.Core.Events;

public enum EventSeverity
{
    Info,
    Warning,
    Critical
}

public enum EventType
{
    HeavyBraking,
    UnstableBraking,
    AbruptBrakeRelease,
    ThrottleHesitation,
    EarlyThrottleWithSteering,
    SteeringOveruse,
    TractionLoss,
    TyreOverheating,
    BrakeOverheating,
    LowFuel,
    InvalidLapOrFlags,
    LapStart,
    LapEnd
}

public sealed record TelemetryEvent(
    Guid Id,
    EventType Type,
    EventSeverity Severity,
    double Confidence,
    DateTimeOffset Timestamp,
    double? LapProgress,
    int? LapNumber,
    IReadOnlyDictionary<string, object?> Evidence,
    string SuggestedAction);
