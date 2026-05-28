using System.Globalization;

namespace RaceEngineer.Core.Coaching.Ai;

public sealed class MockEngineerAiProvider : IEngineerAiProvider
{
    public string Name => "mock";

    public bool IsEnabled => true;

    public Task<EngineerAiResult> GenerateAnswerAsync(EngineerAiRequest request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (request.Context.Facts.Count == 0)
        {
            return Task.FromResult(EngineerAiResult.Failed("No telemetry evidence was provided."));
        }

        var text = request.Question.ToLowerInvariant();
        var topFacts = request.Context.Facts.Take(3).ToArray();
        var lead = topFacts[0];

        string answer;
        if (ContainsAny(text, CoachQueryPhrases.LosingTime))
        {
            answer = BuildTopicAnswer(
                request.Context,
                "Najveći gubitak vremena je u",
                "Your biggest time loss is in",
                topFacts);
        }
        else if (ContainsAny(text, CoachQueryPhrases.Braking))
        {
            answer = BuildTopicAnswer(
                request.Context,
                "Kočenje prema telemetriji:",
                "Braking from telemetry:",
                topFacts);
        }
        else if (ContainsAny(text, CoachQueryPhrases.Fuel)
            || text.Contains("goriv", StringComparison.Ordinal)
            || ContainsAny(text, CoachQueryPhrases.Strategy))
        {
            if (!request.Context.StrategyCalloutsAllowed
                && (ContainsAny(text, CoachQueryPhrases.Strategy) || ContainsAny(text, CoachQueryPhrases.Pit)))
            {
                answer = text.Contains("goriv", StringComparison.Ordinal) || ContainsAny(text, CoachQueryPhrases.Fuel)
                    ? FormatFuelOnly(request.Context, topFacts)
                    : "Nemam dovoljno pouzdanu strategiju iz telemetrije.";
            }
            else
            {
                answer = FormatFuelAndStrategy(request.Context, topFacts);
            }
        }
        else
        {
            answer = $"{lead.Summary}: {lead.Detail}";
        }

        var validated = EngineerAiResponseValidator.Validate(answer, request.Context, request.MaxResponseWords);
        if (validated is null)
        {
            return Task.FromResult(EngineerAiResult.Failed("Mock answer failed validation."));
        }

        return Task.FromResult(EngineerAiResult.Succeeded(
            validated,
            Name,
            EngineerAiResponseValidator.BuildUncertainty(request.Context)));
    }

    private static string BuildTopicAnswer(
        EngineerAiContext context,
        string croatianLead,
        string englishLead,
        IReadOnlyList<EngineerAiFact> facts)
    {
        var lead = IsCroatian(context.Question) ? croatianLead : englishLead;
        var factText = string.Join("; ", facts.Select(fact => $"{fact.Summary} ({fact.Detail})"));
        return $"{lead} {factText}.";
    }

    private static string FormatFuelOnly(EngineerAiContext context, IReadOnlyList<EngineerAiFact> facts)
    {
        var fuelFact = facts.FirstOrDefault(fact =>
            fact.Topic.Contains("Fuel", StringComparison.OrdinalIgnoreCase)
                || fact.Summary.Contains("fuel", StringComparison.OrdinalIgnoreCase)
                || fact.Summary.Contains("goriv", StringComparison.OrdinalIgnoreCase));

        if (context.FuelLiters is { } fuel)
        {
            var prefix = IsCroatian(context.Question)
                ? $"Trenutno imaš {fuel.ToString("0.0", CultureInfo.InvariantCulture)} L goriva."
                : $"You currently have {fuel.ToString("0.0", CultureInfo.InvariantCulture)} L fuel.";
            return fuelFact is null ? prefix : $"{prefix} {fuelFact.Summary}: {fuelFact.Detail}.";
        }

        return fuelFact is null
            ? "Podaci o gorivu nisu dostupni."
            : $"{fuelFact.Summary}: {fuelFact.Detail}.";
    }

    private static string FormatFuelAndStrategy(EngineerAiContext context, IReadOnlyList<EngineerAiFact> facts)
    {
        var fuel = FormatFuelOnly(context, facts);
        var strategyFact = facts.FirstOrDefault(fact =>
            fact.Topic.Contains("Strategy", StringComparison.OrdinalIgnoreCase)
                || fact.Summary.Contains("strategy", StringComparison.OrdinalIgnoreCase)
                || fact.Summary.Contains("Pit", StringComparison.OrdinalIgnoreCase));

        if (strategyFact is null)
        {
            return fuel;
        }

        return $"{fuel} {strategyFact.Summary}: {strategyFact.Detail}.";
    }

    private static bool IsCroatian(string question)
    {
        return question.Contains("gdje", StringComparison.OrdinalIgnoreCase)
            || question.Contains("kako", StringComparison.OrdinalIgnoreCase)
            || question.Contains("imam", StringComparison.OrdinalIgnoreCase)
            || question.Contains("goriv", StringComparison.OrdinalIgnoreCase)
            || question.Contains("gubim", StringComparison.OrdinalIgnoreCase);
    }

    private static bool ContainsAny(string text, params string[] phrases)
    {
        foreach (var phrase in phrases)
        {
            if (text.Contains(phrase, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }
}
