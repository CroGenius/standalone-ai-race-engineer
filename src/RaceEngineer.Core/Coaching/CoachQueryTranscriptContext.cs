using RaceEngineer.Core.Voice;

namespace RaceEngineer.Core.Coaching;

public sealed record CoachQueryTranscriptContext(
    string? RawTranscript = null,
    string? NormalizedTranscript = null,
    float? Confidence = null,
    TranscriptGateDecision? GateDecision = null);
