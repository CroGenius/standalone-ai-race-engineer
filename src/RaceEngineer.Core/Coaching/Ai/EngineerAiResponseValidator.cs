namespace RaceEngineer.Core.Coaching.Ai;

public static class EngineerAiResponseValidator
{
    public static string? Validate(string answer, EngineerAiContext context, int maxResponseWords)
    {
        if (string.IsNullOrWhiteSpace(answer))
        {
            return null;
        }

        var trimmed = RaceSafeText(answer.Trim(), maxResponseWords);
        if (trimmed.Length == 0)
        {
            return null;
        }

        if (IsStrategyQuestion(context.Question) && !context.StrategyCalloutsAllowed)
        {
            return null;
        }

        if (context.Facts.Count == 0)
        {
            return null;
        }

        return trimmed;
    }

    public static string BuildUncertainty(EngineerAiContext context)
    {
        if (context.Facts.Count == 0)
        {
            return "Telemetry evidence unavailable.";
        }

        var averageConfidence = context.Facts.Average(fact => fact.Confidence);
        if (averageConfidence < 0.45)
        {
            return "Low-confidence telemetry evidence.";
        }

        return "AI-assisted from structured telemetry evidence.";
    }

    private static bool IsStrategyQuestion(string question)
    {
        var text = question.ToLowerInvariant();
        return CoachQueryPhrases.Strategy.Any(phrase => text.Contains(phrase, StringComparison.Ordinal))
            || CoachQueryPhrases.Pit.Any(phrase => text.Contains(phrase, StringComparison.Ordinal));
    }

    private static string RaceSafeText(string text, int maxWords)
    {
        var words = text.Split([' ', '\r', '\n', '\t'], StringSplitOptions.RemoveEmptyEntries);
        if (words.Length <= maxWords)
        {
            return text;
        }

        return string.Join(' ', words.Take(maxWords));
    }
}
