namespace RaceEngineer.Core.Coaching;

using RaceEngineer.Core.RaceAwareness;

public enum CoachQueryTopic
{
    Unknown,
    Position,
    Tyre,
    PushConfidence,
    LapTime,
    LosingTime,
    Braking,
    Throttle,
    Improvement,
    LapComparison,
    RacePace,
    Incidents,
    Pit,
    Strategy,
    FuelAmount,
    FuelConsumption,
    FuelStrategy,
    RaceAwareness,
    TrackIdentity,
    CarIdentity,
    TrackMemory
}

public static class CoachQueryTopicClassifier
{
    public static string NormalizeQuery(string question) =>
        question.ReplaceLineEndings(" ").Trim().ToLowerInvariant();

    public static CoachQueryTopic ClassifyPrimary(string question)
    {
        var text = NormalizeQuery(question);

        if (ContainsAny(text, CoachQueryPhrases.TrackMemory))
        {
            return CoachQueryTopic.TrackMemory;
        }

        if (ContainsAny(text, CoachQueryPhrases.Position))
        {
            return CoachQueryTopic.Position;
        }

        if (ContainsAny(text, CoachQueryPhrases.PushConfidence))
        {
            return CoachQueryTopic.PushConfidence;
        }

        if (ContainsAny(text, CoachQueryPhrases.Tyre) || text.Contains("gume", StringComparison.Ordinal))
        {
            return CoachQueryTopic.Tyre;
        }

        if (ContainsAny(text, CoachQueryPhrases.LapTime)
            || text.Contains("vrijeme kruga", StringComparison.Ordinal)
            || text.Contains("vreme kruga", StringComparison.Ordinal))
        {
            return CoachQueryTopic.LapTime;
        }

        if (ContainsAny(text, CoachQueryPhrases.LosingTime))
        {
            return CoachQueryTopic.LosingTime;
        }

        if (ContainsAny(text, CoachQueryPhrases.Braking))
        {
            return CoachQueryTopic.Braking;
        }

        if (ContainsAny(text, CoachQueryPhrases.Throttle))
        {
            return CoachQueryTopic.Throttle;
        }

        if (ContainsAny(text, CoachQueryPhrases.Improvement))
        {
            return CoachQueryTopic.Improvement;
        }

        if (ContainsAny(text, "compare my laps", "compare laps", "lap comparison"))
        {
            return CoachQueryTopic.LapComparison;
        }

        if (ContainsAny(text, CoachQueryPhrases.RacePace))
        {
            return CoachQueryTopic.RacePace;
        }

        if (ContainsAny(text, CoachQueryPhrases.Incidents))
        {
            return CoachQueryTopic.Incidents;
        }

        if (ContainsAny(text, CoachQueryPhrases.Pit))
        {
            return CoachQueryTopic.Pit;
        }

        if (ContainsAny(text, CoachQueryPhrases.FuelStrategy))
        {
            return CoachQueryTopic.FuelStrategy;
        }

        if (ContainsAny(text, CoachQueryPhrases.FuelConsumption))
        {
            return CoachQueryTopic.FuelConsumption;
        }

        if (ContainsAny(text, CoachQueryPhrases.FuelAmount) || text.Contains("goriv", StringComparison.Ordinal))
        {
            if (!ContainsAny(text, "fuel plan", "fuel strategy"))
            {
                return CoachQueryTopic.FuelAmount;
            }
        }

        if (ContainsAny(text, CoachQueryPhrases.Strategy))
        {
            return CoachQueryTopic.Strategy;
        }

        if (ContainsAny(text, CoachQueryPhrases.RaceAwareness))
        {
            return CoachQueryTopic.RaceAwareness;
        }

        if (RaceAwarenessQueryClassifier.IsTrackIdentityQuery(text))
        {
            return CoachQueryTopic.TrackIdentity;
        }

        if (RaceAwarenessQueryClassifier.IsCarIdentityQuery(text))
        {
            return CoachQueryTopic.CarIdentity;
        }

        return CoachQueryTopic.Unknown;
    }

    public static bool IsTechnicalTopic(string text)
    {
        var normalized = NormalizeQuery(text);
        return ContainsAny(normalized, CoachQueryPhrases.Tyre)
            || normalized.Contains("gume", StringComparison.Ordinal)
            || ContainsAny(normalized, CoachQueryPhrases.LosingTime)
            || ContainsAny(normalized, CoachQueryPhrases.Braking)
            || ContainsAny(normalized, CoachQueryPhrases.Throttle)
            || ContainsAny(normalized, CoachQueryPhrases.Improvement)
            || ContainsAny(normalized, CoachQueryPhrases.RacePace)
            || ContainsAny(normalized, CoachQueryPhrases.PushConfidence)
            || ContainsAny(normalized, CoachQueryPhrases.LapTime)
            || normalized.Contains("vrijeme kruga", StringComparison.Ordinal)
            || normalized.Contains("vreme kruga", StringComparison.Ordinal)
            || ContainsAny(normalized, "compare my laps", "compare laps", "lap comparison");
    }

    public static bool BlocksIdentityRouting(CoachQueryTopic topic) =>
        topic is CoachQueryTopic.Tyre
            or CoachQueryTopic.PushConfidence
            or CoachQueryTopic.LosingTime
            or CoachQueryTopic.Braking
            or CoachQueryTopic.Throttle
            or CoachQueryTopic.Improvement
            or CoachQueryTopic.LapComparison
            or CoachQueryTopic.RacePace;

    public static CoachEvidenceTopic? ToEvidenceTopic(CoachQueryTopic topic) =>
        topic switch
        {
            CoachQueryTopic.LosingTime => CoachEvidenceTopic.LosingTime,
            CoachQueryTopic.Braking => CoachEvidenceTopic.Braking,
            CoachQueryTopic.Throttle => CoachEvidenceTopic.Throttle,
            CoachQueryTopic.Improvement => CoachEvidenceTopic.Improvement,
            CoachQueryTopic.LapComparison => CoachEvidenceTopic.LapComparison,
            CoachQueryTopic.RacePace => CoachEvidenceTopic.RacePace,
            CoachQueryTopic.Incidents => CoachEvidenceTopic.Incidents,
            CoachQueryTopic.FuelAmount or CoachQueryTopic.FuelConsumption or CoachQueryTopic.FuelStrategy => CoachEvidenceTopic.Fuel,
            CoachQueryTopic.Tyre => CoachEvidenceTopic.Tyres,
            CoachQueryTopic.PushConfidence => CoachEvidenceTopic.Tyres,
            CoachQueryTopic.Strategy or CoachQueryTopic.Pit => CoachEvidenceTopic.Strategy,
            CoachQueryTopic.RaceAwareness => CoachEvidenceTopic.RaceAwareness,
            CoachQueryTopic.TrackIdentity => CoachEvidenceTopic.RaceAwareness,
            CoachQueryTopic.CarIdentity => CoachEvidenceTopic.RaceAwareness,
            CoachQueryTopic.TrackMemory => CoachEvidenceTopic.TrackMemory,
            _ => null
        };

    public static bool IsFuelTopic(CoachQueryTopic topic) =>
        topic is CoachQueryTopic.FuelAmount or CoachQueryTopic.FuelConsumption or CoachQueryTopic.FuelStrategy;

    public static bool AllowsFuelEvidence(CoachQueryTopic topic) =>
        IsFuelTopic(topic) || topic is CoachQueryTopic.Strategy or CoachQueryTopic.Pit or CoachQueryTopic.Unknown;

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
