namespace RaceEngineer.Core.Telemetry.Iracing;

public enum IracingSessionConnectionState
{
    Disconnected,
    SdkUnavailable,
    SessionNotActive,
    Connected
}

public sealed record IracingCarTelemetry(
    double? SpeedMps,
    double? Rpm,
    int? Gear,
    double? Throttle,
    double? Brake,
    double? Steering,
    double? FuelLevel);

public sealed record IracingSessionTelemetry(
    string? TrackName,
    string? TrackDisplayName,
    string? CarName,
    string? CarClass,
    string? SessionType,
    int? SessionNum,
    int? SessionLapsTotal,
    double? SessionTimeRemainingSeconds,
    int? SessionFlags);

public sealed record IracingRaceTelemetry(
    int? Position,
    int? ClassPosition,
    int? TotalCars,
    int? Lap,
    double? LapCurrentTimeSeconds,
    double? LapDistPct,
    double? GapAheadSeconds,
    double? GapBehindSeconds,
    string? CarAheadName,
    string? CarBehindName,
    int? Incidents);

public sealed record IracingPitTelemetry(
    bool? OnPitRoad,
    bool? InPitStall,
    bool? PitStopActive);

public sealed record IracingFlagsTelemetry(
    string? SessionFlags,
    string? PlayerCarFlags);

public sealed record IracingTelemetryFrame(
    DateTimeOffset Timestamp,
    IracingCarTelemetry Car,
    IracingSessionTelemetry Session,
    IracingRaceTelemetry Race,
    IracingPitTelemetry Pit,
    IracingFlagsTelemetry Flags);
