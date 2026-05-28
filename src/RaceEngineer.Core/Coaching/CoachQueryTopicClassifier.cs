namespace RaceEngineer.Core.Coaching;

public enum CoachQueryTopic
{
    Unknown,
    Position,
    Tyre,
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
    Fuel
}

public static class CoachQueryTopicClassifier
{
    public static CoachQueryTopic ClassifyPrimary(string question)
    {
        var text = question.ToLowerInvariant();

        if (ContainsAny(text, CoachQueryPhrases.Position))
        {
            return CoachQueryTopic.Position;
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

        if (ContainsAny(text, CoachQueryPhrases.Strategy))
        {
            return CoachQueryTopic.Strategy;
        }

        if (ContainsAny(text, CoachQueryPhrases.Fuel) || text.Contains("goriv", StringComparison.Ordinal))
        {
            if (!ContainsAny(text, "fuel plan", "fuel strategy"))
            {
                return CoachQueryTopic.Fuel;
            }
        }

        return CoachQueryTopic.Unknown;
    }

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
            CoachQueryTopic.Fuel => CoachEvidenceTopic.Fuel,
            CoachQueryTopic.Tyre => CoachEvidenceTopic.Tyres,
            CoachQueryTopic.Strategy or CoachQueryTopic.Pit => CoachEvidenceTopic.Strategy,
            _ => null
        };

    public static bool AllowsFuelEvidence(CoachQueryTopic topic) =>
        topic is CoachQueryTopic.Fuel or CoachQueryTopic.Strategy or CoachQueryTopic.Pit or CoachQueryTopic.Unknown;

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
