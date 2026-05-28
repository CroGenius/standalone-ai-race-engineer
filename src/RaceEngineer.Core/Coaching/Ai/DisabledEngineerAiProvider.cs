namespace RaceEngineer.Core.Coaching.Ai;

public sealed class DisabledEngineerAiProvider : IEngineerAiProvider
{
    public static DisabledEngineerAiProvider Instance { get; } = new();

    public string Name => "disabled";

    public bool IsEnabled => false;

    public Task<EngineerAiResult> GenerateAnswerAsync(EngineerAiRequest request, CancellationToken cancellationToken) =>
        Task.FromResult(EngineerAiResult.Disabled("AI engineer is disabled."));
}
