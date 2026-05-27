namespace RaceEngineer.Core.Telemetry;

public sealed record TelemetryPacketResult(
    DateTimeOffset Timestamp,
    bool IsValid,
    TelemetrySnapshot? Snapshot,
    string? Warning,
    string? RawJson = null,
    string? Schema = null,
    int? SchemaVersion = null);

public sealed record TelemetryPacketEnvelope(string? Schema, int? SchemaVersion);
