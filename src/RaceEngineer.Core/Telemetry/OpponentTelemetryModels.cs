namespace RaceEngineer.Core.Telemetry;

public sealed record OpponentFieldCandidate(
    string JsonPath,
    string SampleValue,
    string MatchedKeyword);

public sealed record OpponentTelemetrySourceReport(
    IReadOnlyList<string> PresentFields,
    IReadOnlyList<string> MissingFields,
    IReadOnlyDictionary<string, string> ResolvedPropertyNames,
    IReadOnlyList<OpponentFieldCandidate> CandidateRawFields,
    string Summary);
