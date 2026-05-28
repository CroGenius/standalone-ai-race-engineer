using RaceEngineer.Core;

namespace RaceEngineer.Core.Coaching.Ai;

public sealed record EngineerAiOptions(
    bool Enabled,
    string Provider,
    string Model,
    string Endpoint,
    int MaxResponseWords,
    int TimeoutSeconds)
{
    public static EngineerAiOptions FromAppSettings(AppSettings settings)
    {
        return new EngineerAiOptions(
            settings.AiEngineerEnabled,
            settings.AiProvider,
            settings.AiModel,
            settings.AiEndpoint,
            settings.AiMaxResponseWords,
            settings.AiTimeoutSeconds);
    }
}

public sealed record EngineerAiFact(
    string Topic,
    string Summary,
    string Detail,
    double Confidence,
    string Source,
    int? LapNumber);

public sealed record EngineerAiContext(
    string Question,
    string SessionMode,
    string StrategyConfidence,
    bool StrategyCalloutsAllowed,
    int? CurrentLap,
    double? FuelLiters,
    IReadOnlyList<EngineerAiFact> Facts,
    IReadOnlyList<string> Guardrails);

public sealed record EngineerAiRequest(
    string Question,
    EngineerAiContext Context,
    int MaxResponseWords);

public sealed record EngineerAiResult(
    bool Success,
    string? Answer,
    string? Uncertainty,
    string Source,
    string? FailureReason)
{
    public static EngineerAiResult Disabled(string reason) =>
        new(false, null, null, "disabled", reason);

    public static EngineerAiResult Failed(string reason, string source = "ai") =>
        new(false, null, null, source, reason);

    public static EngineerAiResult Succeeded(string answer, string source, string? uncertainty = null) =>
        new(true, answer, uncertainty, source, null);
}
