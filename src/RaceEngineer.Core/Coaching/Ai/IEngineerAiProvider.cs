namespace RaceEngineer.Core.Coaching.Ai;

public interface IEngineerAiProvider
{
    string Name { get; }

    bool IsEnabled { get; }

    Task<EngineerAiResult> GenerateAnswerAsync(EngineerAiRequest request, CancellationToken cancellationToken);
}
