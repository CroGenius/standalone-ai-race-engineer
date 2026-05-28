namespace RaceEngineer.Core.Coaching;

public sealed record CoachAnswerTrace(
    string Query,
    CoachQueryTopic Topic,
    bool GateBlocked,
    string? GateMessage,
    string AnswerSource,
    string? SelectedEvidenceType,
    string? FallbackReason);

public static class CoachQueryDiagnosticLog
{
    public static event Action<CoachAnswerTrace>? TraceRaised;
    public static event Action<CoachQueryRuntimeTrace>? RuntimeTraceRaised;

    public static void Raise(CoachAnswerTrace trace) => TraceRaised?.Invoke(trace);

    public static void RaiseRuntime(CoachQueryRuntimeTrace trace) => RuntimeTraceRaised?.Invoke(trace);
}
