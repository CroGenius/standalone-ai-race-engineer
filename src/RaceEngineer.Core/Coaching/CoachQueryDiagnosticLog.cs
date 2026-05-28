namespace RaceEngineer.Core.Coaching;

using RaceEngineer.Core.Coaching.Ai;

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
    public static event Action<EngineerAiDiagnosticTrace>? AiTraceRaised;

    public static void Raise(CoachAnswerTrace trace) => TraceRaised?.Invoke(trace);

    public static void RaiseRuntime(CoachQueryRuntimeTrace trace) => RuntimeTraceRaised?.Invoke(trace);

    public static void RaiseAi(EngineerAiDiagnosticTrace trace) => AiTraceRaised?.Invoke(trace);
}
