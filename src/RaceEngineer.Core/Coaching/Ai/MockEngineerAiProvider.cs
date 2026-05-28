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

        var topic = CoachQueryTopicClassifier.ClassifyPrimary(request.Question);
        var topicFacts = FilterFactsByTopic(request.Context.Facts, topic).Take(3).ToArray();
        if (topicFacts.Length == 0)
        {
            topicFacts = request.Context.Facts.Take(3).ToArray();
        }

        var answer = topic switch
        {
            CoachQueryTopic.Position => FormatPositionAnswer(request.Context),
            CoachQueryTopic.Tyre => FormatTyreAnswer(topicFacts),
            CoachQueryTopic.LapTime => FormatLapTimeAnswer(topicFacts),
            CoachQueryTopic.LosingTime => BuildTopicAnswer("Your biggest time loss is in", topicFacts),
            CoachQueryTopic.Braking => BuildTopicAnswer("Braking from telemetry:", topicFacts),
            CoachQueryTopic.Throttle => BuildTopicAnswer("Throttle from telemetry:", topicFacts),
            CoachQueryTopic.RacePace => BuildTopicAnswer("Race pace from telemetry:", topicFacts),
            CoachQueryTopic.Improvement => BuildTopicAnswer("Improve next on", topicFacts),
            CoachQueryTopic.Fuel => FormatFuelOnly(request.Context, FilterFactsByTopic(request.Context.Facts, CoachQueryTopic.Fuel)),
            CoachQueryTopic.Strategy or CoachQueryTopic.Pit => FormatStrategyAnswer(request, topicFacts),
            _ => BuildTopicAnswer("Telemetry shows", topicFacts)
        };

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

    private static string FormatPositionAnswer(EngineerAiContext context)
    {
        return context.CurrentLap is null && context.FuelLiters is null
            ? "Position data is unavailable."
            : "Position data is unavailable.";
    }

    private static string FormatTyreAnswer(IReadOnlyList<EngineerAiFact> facts)
    {
        var tyreFacts = facts.Where(IsTyreFact).ToArray();
        if (tyreFacts.Length == 0)
        {
            return "Tyre data is not reliable yet.";
        }

        return BuildTopicAnswer("Tyres from telemetry:", tyreFacts);
    }

    private static string FormatLapTimeAnswer(IReadOnlyList<EngineerAiFact> facts)
    {
        var lapFacts = facts.Where(IsLapFact).ToArray();
        if (lapFacts.Length == 0)
        {
            return "No valid lap time yet.";
        }

        return BuildTopicAnswer("Lap time from telemetry:", lapFacts);
    }

    private static string FormatStrategyAnswer(EngineerAiRequest request, IReadOnlyList<EngineerAiFact> facts)
    {
        if (!request.Context.StrategyCalloutsAllowed)
        {
            return ContainsAny(request.Question.ToLowerInvariant(), CoachQueryPhrases.Fuel)
                || request.Question.Contains("goriv", StringComparison.OrdinalIgnoreCase)
                ? FormatFuelOnly(request.Context, FilterFactsByTopic(request.Context.Facts, CoachQueryTopic.Fuel))
                : "Strategy confidence is too low for a pit call.";
        }

        var strategyFacts = facts.Where(IsStrategyFact).ToArray();
        var fuelFacts = FilterFactsByTopic(request.Context.Facts, CoachQueryTopic.Fuel);
        if (strategyFacts.Length == 0 && fuelFacts.Count == 0)
        {
            return "Strategy is unavailable.";
        }

        if (strategyFacts.Length == 0)
        {
            return FormatFuelOnly(request.Context, fuelFacts);
        }

        var strategy = strategyFacts[0];
        return fuelFacts.Count > 0
            ? $"{FormatFuelOnly(request.Context, fuelFacts)} {strategy.Summary}: {strategy.Detail}."
            : $"{strategy.Summary}: {strategy.Detail}.";
    }

    private static string BuildTopicAnswer(string lead, IReadOnlyList<EngineerAiFact> facts)
    {
        if (facts.Count == 0)
        {
            return "Telemetry evidence is unavailable.";
        }

        var factText = string.Join("; ", facts.Select(fact => $"{fact.Summary} ({fact.Detail})"));
        return $"{lead} {factText}.";
    }

    private static string FormatFuelOnly(EngineerAiContext context, IReadOnlyList<EngineerAiFact> facts)
    {
        var fuelFact = facts.FirstOrDefault(IsFuelFact);

        if (context.FuelLiters is { } fuel)
        {
            var prefix = $"You currently have {fuel.ToString("0.0", CultureInfo.InvariantCulture)} L fuel.";
            return fuelFact is null ? prefix : $"{prefix} {fuelFact.Summary}: {fuelFact.Detail}.";
        }

        return fuelFact is null
            ? "Fuel data is unavailable."
            : $"{fuelFact.Summary}: {fuelFact.Detail}.";
    }

    private static IReadOnlyList<EngineerAiFact> FilterFactsByTopic(IReadOnlyList<EngineerAiFact> facts, CoachQueryTopic topic)
    {
        return topic switch
        {
            CoachQueryTopic.Fuel => facts.Where(IsFuelFact).ToArray(),
            CoachQueryTopic.Strategy or CoachQueryTopic.Pit => facts.Where(fact => IsStrategyFact(fact) || IsFuelFact(fact)).ToArray(),
            CoachQueryTopic.Tyre => facts.Where(IsTyreFact).ToArray(),
            CoachQueryTopic.LapTime => facts.Where(IsLapFact).ToArray(),
            CoachQueryTopic.Braking => facts.Where(IsBrakingFact).ToArray(),
            CoachQueryTopic.Throttle => facts.Where(IsThrottleFact).ToArray(),
            CoachQueryTopic.RacePace => facts.Where(IsPaceFact).ToArray(),
            CoachQueryTopic.LosingTime or CoachQueryTopic.LapComparison => facts.Where(IsLapComparisonFact).ToArray(),
            CoachQueryTopic.Improvement => facts.Where(IsImprovementFact).ToArray(),
            CoachQueryTopic.Position => [],
            _ => facts.Where(fact => !IsFuelFact(fact)).ToArray()
        };
    }

    private static bool IsFuelFact(EngineerAiFact fact)
    {
        var key = $"{fact.Topic} {fact.Summary} {fact.Detail}".ToLowerInvariant();
        return key.Contains("fuel") || key.Contains("goriv");
    }

    private static bool IsStrategyFact(EngineerAiFact fact)
    {
        var key = $"{fact.Topic} {fact.Summary}".ToLowerInvariant();
        return key.Contains("strategy") || key.Contains("pit");
    }

    private static bool IsTyreFact(EngineerAiFact fact)
    {
        var key = $"{fact.Topic} {fact.Summary} {fact.Detail}".ToLowerInvariant();
        return key.Contains("tyre") || key.Contains("tire") || key.Contains("gum");
    }

    private static bool IsLapFact(EngineerAiFact fact)
    {
        var key = $"{fact.Topic} {fact.Summary} {fact.Detail}".ToLowerInvariant();
        return key.Contains("lap") || key.Contains("time:");
    }

    private static bool IsBrakingFact(EngineerAiFact fact)
    {
        var key = $"{fact.Topic} {fact.Summary} {fact.Detail}".ToLowerInvariant();
        return key.Contains("brak");
    }

    private static bool IsThrottleFact(EngineerAiFact fact)
    {
        var key = $"{fact.Topic} {fact.Summary} {fact.Detail}".ToLowerInvariant();
        return key.Contains("throttle") || key.Contains("gas");
    }

    private static bool IsPaceFact(EngineerAiFact fact)
    {
        var key = $"{fact.Topic} {fact.Summary}".ToLowerInvariant();
        return key.Contains("pace") || key.Contains("tempo");
    }

    private static bool IsLapComparisonFact(EngineerAiFact fact)
    {
        var key = $"{fact.Topic} {fact.Summary}".ToLowerInvariant();
        return key.Contains("sector") || key.Contains("delta") || key.Contains("lap");
    }

    private static bool IsImprovementFact(EngineerAiFact fact)
    {
        var key = $"{fact.Topic} {fact.Summary}".ToLowerInvariant();
        return key.Contains("improvement") || key.Contains("weakness");
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
