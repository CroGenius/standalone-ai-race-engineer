using RaceEngineer.Core.RaceAwareness;

namespace RaceEngineer.Core.Coaching;

public sealed record CoachQueryRuntimeTrace(
    string Transcript,
    CoachQueryTopic Topic,
    string SessionMode,
    int CompletedLaps,
    bool GateBlocked,
    string? GateMessage,
    string DeterministicAnswer,
    string PrimaryAnswer,
    string SpokenSummary,
    string FinalTtsPayload,
    string FinalDisplayedText,
    string AnswerSource,
    string? SelectedEvidenceType,
    string? FallbackReason,
    string AssemblyVersion,
    RaceAwarenessSubtopic? RaceAwarenessSubtopic = null,
    string? RaceAwarenessSelectedFields = null,
    string? RaceAwarenessMissingFields = null,
    string? RaceAwarenessFallbackReason = null);
