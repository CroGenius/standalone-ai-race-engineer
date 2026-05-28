using System.Globalization;
using RaceEngineer.Core.SessionContext;

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
            if (DrivingTechniqueGate.IsDrivingTechniqueTopic(topic))
            {
                return Task.FromResult(EngineerAiResult.Failed("No technique evidence available."));
            }

            topicFacts = request.Context.Facts
                .Where(fact => !IsInvalidLapOrFlagsFact(fact))
                .Take(3)
                .ToArray();
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
            CoachQueryTopic.FuelAmount => FormatFuelAmountAnswer(request.Context, FilterFactsByTopic(request.Context.Facts, CoachQueryTopic.FuelAmount)),
            CoachQueryTopic.FuelConsumption => FormatFuelConsumptionAnswer(request.Context, FilterFactsByTopic(request.Context.Facts, CoachQueryTopic.FuelConsumption)),
            CoachQueryTopic.FuelStrategy => FormatFuelStrategyAnswer(request.Context, FilterFactsByTopic(request.Context.Facts, CoachQueryTopic.FuelStrategy)),
            CoachQueryTopic.Strategy or CoachQueryTopic.Pit => FormatStrategyAnswer(request, topicFacts),
            CoachQueryTopic.TrackIdentity => FormatTrackIdentityAnswer(topicFacts),
            CoachQueryTopic.CarIdentity => FormatCarIdentityAnswer(topicFacts),
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

    private static string FormatTrackIdentityAnswer(IReadOnlyList<EngineerAiFact> facts)
    {
        var trackFact = facts.FirstOrDefault(fact =>
            fact.Summary.Contains("Track identity", StringComparison.OrdinalIgnoreCase)
                || fact.Detail.Contains("track:", StringComparison.OrdinalIgnoreCase)
                || fact.Detail.Contains("Track is ", StringComparison.OrdinalIgnoreCase));
        if (trackFact is null)
        {
            return "Track name is unavailable from telemetry.";
        }

        if (trackFact.Detail.Contains("Track is ", StringComparison.OrdinalIgnoreCase))
        {
            var track = trackFact.Detail["Track is ".Length..].Trim().TrimEnd('.');
            return string.IsNullOrWhiteSpace(track)
                ? "Track name is unavailable from telemetry."
                : $"You are on {track}.";
        }

        var legacyTrack = trackFact.Detail.Replace("track:", "", StringComparison.OrdinalIgnoreCase).Trim();
        return string.IsNullOrWhiteSpace(legacyTrack)
            ? "Track name is unavailable from telemetry."
            : $"You are on {legacyTrack.TrimEnd('.')}.";
    }

    private static string FormatCarIdentityAnswer(IReadOnlyList<EngineerAiFact> facts)
    {
        var carFact = facts.FirstOrDefault(fact =>
            fact.Summary.Contains("Car identity", StringComparison.OrdinalIgnoreCase)
                || fact.Detail.Contains("car:", StringComparison.OrdinalIgnoreCase));
        if (carFact is null)
        {
            return "Car name is unavailable from telemetry.";
        }

        if (carFact.Detail.Contains("car:", StringComparison.OrdinalIgnoreCase))
        {
            var car = carFact.Detail.Replace("car:", "", StringComparison.OrdinalIgnoreCase).Trim();
            return string.IsNullOrWhiteSpace(car)
                ? "Car name is unavailable from telemetry."
                : $"{car.TrimEnd('.')}.";
        }

        return string.IsNullOrWhiteSpace(carFact.Detail)
            ? "Car name is unavailable from telemetry."
            : $"{carFact.Detail.Trim().TrimEnd('.')}.";
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
            return ContainsAny(request.Question.ToLowerInvariant(), CoachQueryPhrases.FuelAmount)
                || ContainsAny(request.Question.ToLowerInvariant(), CoachQueryPhrases.FuelConsumption)
                || request.Question.Contains("goriv", StringComparison.OrdinalIgnoreCase)
                ? FormatFuelAmountAnswer(request.Context, FilterFactsByTopic(request.Context.Facts, CoachQueryTopic.FuelAmount))
                : "Strategy confidence is too low for a pit call.";
        }

        var strategyFacts = facts.Where(IsStrategyFact).ToArray();
        var fuelFacts = FilterFactsByTopic(request.Context.Facts, CoachQueryTopic.FuelStrategy);
        if (strategyFacts.Length == 0 && fuelFacts.Count == 0)
        {
            return "Strategy is unavailable.";
        }

        if (strategyFacts.Length == 0)
        {
            return FormatFuelStrategyAnswer(request.Context, fuelFacts);
        }

        var strategy = strategyFacts[0];
        return fuelFacts.Count > 0
            ? $"{FormatFuelStrategyAnswer(request.Context, fuelFacts)} {strategy.Summary}: {strategy.Detail}."
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

    private static string FormatFuelAmountAnswer(EngineerAiContext context, IReadOnlyList<EngineerAiFact> facts)
    {
        if (context.FuelLiters is not { } fuel)
        {
            return "Fuel data is unavailable.";
        }

        var prefix = $"You have {fuel.ToString("0.0", CultureInfo.InvariantCulture)} liters.";
        var consumption = facts.FirstOrDefault(fact => fact.Summary.Contains("per lap", StringComparison.OrdinalIgnoreCase));
        return consumption is null
            ? prefix
            : $"{prefix} Fuel use is about {ExtractNumericDetail(consumption.Detail)} L/lap.";
    }

    private static string FormatFuelConsumptionAnswer(EngineerAiContext context, IReadOnlyList<EngineerAiFact> facts)
    {
        var consumption = facts.FirstOrDefault(fact => fact.Summary.Contains("per lap", StringComparison.OrdinalIgnoreCase));
        if (consumption is not null)
        {
            return $"Fuel use is about {ExtractNumericDetail(consumption.Detail)} L/lap.";
        }

        return "Fuel consumption is unavailable.";
    }

    private static string FormatFuelStrategyAnswer(EngineerAiContext context, IReadOnlyList<EngineerAiFact> facts)
    {
        var laps = facts.FirstOrDefault(fact => fact.Summary.Contains("Laps remaining", StringComparison.OrdinalIgnoreCase));
        var risk = facts.FirstOrDefault(fact => fact.Summary.Contains("Fuel risk", StringComparison.OrdinalIgnoreCase));
        var parts = new List<string>();
        if (laps is not null)
        {
            parts.Add(laps.Detail.TrimEnd('.'));
        }

        if (risk is not null)
        {
            parts.Add(risk.Detail.TrimEnd('.'));
        }

        return parts.Count == 0
            ? "Fuel strategy is unavailable."
            : string.Join(" ", parts);
    }

    private static string FormatFuelOnly(EngineerAiContext context, IReadOnlyList<EngineerAiFact> facts)
    {
        return FormatFuelAmountAnswer(context, facts);
    }

    private static string ExtractNumericDetail(string detail)
    {
        var match = System.Text.RegularExpressions.Regex.Match(detail, @"\d+(?:\.\d+)?");
        return match.Success ? match.Value : detail;
    }

    private static IReadOnlyList<EngineerAiFact> FilterFactsByTopic(IReadOnlyList<EngineerAiFact> facts, CoachQueryTopic topic)
    {
        return topic switch
        {
            CoachQueryTopic.FuelAmount => facts.Where(IsFuelLevelFact).Concat(facts.Where(IsFuelFact)).DistinctBy(f => f.Summary).ToArray(),
            CoachQueryTopic.FuelConsumption => facts.Where(IsFuelConsumptionFact).ToArray(),
            CoachQueryTopic.FuelStrategy => facts.Where(fact => IsFuelStrategyFact(fact) || IsFuelConsumptionFact(fact)).ToArray(),
            CoachQueryTopic.Strategy or CoachQueryTopic.Pit => facts.Where(fact => IsStrategyFact(fact) || IsFuelFact(fact)).ToArray(),
            CoachQueryTopic.Tyre => facts.Where(IsTyreFact).ToArray(),
            CoachQueryTopic.LapTime => facts.Where(IsLapFact).ToArray(),
            CoachQueryTopic.Braking => facts.Where(IsBrakingFact).ToArray(),
            CoachQueryTopic.Throttle => facts.Where(IsThrottleFact).ToArray(),
            CoachQueryTopic.RacePace => facts.Where(IsPaceFact).ToArray(),
            CoachQueryTopic.LosingTime or CoachQueryTopic.LapComparison => facts.Where(IsLapComparisonFact).ToArray(),
            CoachQueryTopic.Improvement => facts.Where(IsImprovementFact).ToArray(),
            CoachQueryTopic.TrackIdentity => facts.Where(fact =>
                fact.Summary.Contains("Track identity", StringComparison.OrdinalIgnoreCase)
                    || fact.Detail.Contains("Track is ", StringComparison.OrdinalIgnoreCase)).ToArray(),
            CoachQueryTopic.CarIdentity => facts.Where(fact =>
                fact.Summary.Contains("Car identity", StringComparison.OrdinalIgnoreCase)).ToArray(),
            CoachQueryTopic.Position => [],
            _ => facts.Where(fact => !IsFuelFact(fact)).ToArray()
        };
    }

    private static bool IsFuelLevelFact(EngineerAiFact fact)
    {
        var key = $"{fact.Topic} {fact.Summary} {fact.Detail}".ToLowerInvariant();
        return key.Contains("latest fuel") || key.Contains("fuel level");
    }

    private static bool IsFuelConsumptionFact(EngineerAiFact fact)
    {
        var key = $"{fact.Topic} {fact.Summary} {fact.Detail}".ToLowerInvariant();
        return key.Contains("per lap") || key.Contains("consumption") || key.Contains("potro");
    }

    private static bool IsFuelStrategyFact(EngineerAiFact fact)
    {
        var key = $"{fact.Topic} {fact.Summary} {fact.Detail}".ToLowerInvariant();
        return key.Contains("laps remaining") || key.Contains("fuel risk") || key.Contains("finish");
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
        if (IsInvalidLapOrFlagsFact(fact))
        {
            return false;
        }

        var key = $"{fact.Topic} {fact.Summary} {fact.Detail}".ToLowerInvariant();
        return key.Contains("brak");
    }

    private static bool IsThrottleFact(EngineerAiFact fact)
    {
        if (IsInvalidLapOrFlagsFact(fact))
        {
            return false;
        }

        var key = $"{fact.Topic} {fact.Summary} {fact.Detail}".ToLowerInvariant();
        return key.Contains("throttle") || key.Contains("gas");
    }

    private static bool IsPaceFact(EngineerAiFact fact)
    {
        if (IsInvalidLapOrFlagsFact(fact))
        {
            return false;
        }

        var key = $"{fact.Topic} {fact.Summary}".ToLowerInvariant();
        return key.Contains("pace") || key.Contains("tempo");
    }

    private static bool IsLapComparisonFact(EngineerAiFact fact)
    {
        if (IsInvalidLapOrFlagsFact(fact))
        {
            return false;
        }

        var key = $"{fact.Topic} {fact.Summary}".ToLowerInvariant();
        return key.Contains("sector") || key.Contains("delta") || key.Contains("lap");
    }

    private static bool IsImprovementFact(EngineerAiFact fact)
    {
        if (IsInvalidLapOrFlagsFact(fact))
        {
            return false;
        }

        var key = $"{fact.Topic} {fact.Summary}".ToLowerInvariant();
        return key.Contains("improvement") || key.Contains("weakness");
    }

    private static bool IsInvalidLapOrFlagsFact(EngineerAiFact fact) =>
        fact.Summary.Contains("InvalidLapOrFlags", StringComparison.OrdinalIgnoreCase)
            || fact.Detail.Contains("InvalidLapOrFlags", StringComparison.OrdinalIgnoreCase);

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
