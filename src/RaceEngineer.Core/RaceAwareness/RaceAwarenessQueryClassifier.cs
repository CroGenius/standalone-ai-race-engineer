using RaceEngineer.Core.Coaching;

namespace RaceEngineer.Core.RaceAwareness;

public static class RaceAwarenessQueryClassifier
{
    private static readonly string[] CarIdentityPhrases =
    [
        "which car am i in",
        "what car am i in",
        "which car am i driving",
        "what car am i driving",
        "what car is this",
        "which car is this"
    ];

    private static readonly string[] TrackIdentityPhrases =
    [
        "which track am i on",
        "what track am i on",
        "which track is this",
        "what track is this",
        "which circuit am i on",
        "what circuit am i on",
        "which circuit is this",
        "what circuit is this",
        "where am i racing",
        "what track am i driving",
        "which track am i driving"
    ];

    private static readonly string[] GapAheadPhrases =
    [
        "gap ahead",
        "car ahead",
        "who is ahead",
        "closing on",
        "am i gaining"
    ];

    private static readonly string[] GapBehindPhrases =
    [
        "gap behind",
        "car behind",
        "who is behind"
    ];

    private static readonly string[] SessionTypePhrases =
    [
        "session type",
        "what session is this",
        "is this practice",
        "is this qualifying",
        "is this a race",
        "practice or race",
        "qualifying or race"
    ];

    private static readonly string[] OpponentCountPhrases =
    [
        "how many cars",
        "how many opponents",
        "total cars",
        "how many drivers",
        "field size"
    ];

    private static readonly string[] PositionPhrases =
    [
        "what is my position",
        "my position",
        "what position am i",
        "koja mi je pozicija",
        "koja je pozicija",
        "koji sam",
        "pozicija",
        "race position"
    ];

    private static readonly string[] NonIdentityTrackPhrases =
    [
        "watch for at",
        "tell me about",
        "about this track",
        "track guide",
        "track notes",
        "how did i drive",
        "faster than last time",
        "last session at",
        "fuel use here",
        "compare to last time"
    ];

    public static RaceAwarenessRoutingResult Classify(string question, LiveRaceContext? raceContext = null)
    {
        var text = question.Trim().ToLowerInvariant();
        var subtopic = ClassifySubtopic(text);
        return RaceAwarenessRoutingResult.ForSubtopic(subtopic, raceContext);
    }

    public static RaceAwarenessSubtopic ClassifySubtopic(string question)
    {
        var text = question.Trim().ToLowerInvariant();

        if (ContainsAny(text, CoachQueryPhrases.TrackMemory))
        {
            return RaceAwarenessSubtopic.HistoricalComparison;
        }

        if (ContainsAny(text, TrackIdentityPhrases) || LooksLikeTrackIdentity(text))
        {
            return RaceAwarenessSubtopic.TrackIdentity;
        }

        if (ContainsAny(text, CarIdentityPhrases) || LooksLikeCarIdentity(text))
        {
            return RaceAwarenessSubtopic.CarIdentity;
        }

        if (ContainsAny(text, GapAheadPhrases))
        {
            return RaceAwarenessSubtopic.GapAhead;
        }

        if (ContainsAny(text, GapBehindPhrases))
        {
            return RaceAwarenessSubtopic.GapBehind;
        }

        if (ContainsAny(text, PositionPhrases) && !LooksLikeTrackIdentity(text))
        {
            return RaceAwarenessSubtopic.Position;
        }

        if (ContainsAny(text, SessionTypePhrases))
        {
            return RaceAwarenessSubtopic.SessionType;
        }

        if (ContainsAny(text, OpponentCountPhrases))
        {
            return RaceAwarenessSubtopic.OpponentCount;
        }

        if (ContainsAny(text, CoachQueryPhrases.RaceAwareness))
        {
            return RaceAwarenessSubtopic.RaceContext;
        }

        return RaceAwarenessSubtopic.RaceContext;
    }

    public static bool LooksLikeTrackIdentity(string text)
    {
        var normalized = text.Trim().ToLowerInvariant();
        if (ContainsAny(normalized, NonIdentityTrackPhrases))
        {
            return false;
        }

        if (ContainsAny(normalized, TrackIdentityPhrases))
        {
            return true;
        }

        return (normalized.Contains("which track", StringComparison.Ordinal)
                || normalized.Contains("what track", StringComparison.Ordinal)
                || normalized.Contains("which circuit", StringComparison.Ordinal)
                || normalized.Contains("what circuit", StringComparison.Ordinal)
                || normalized.Contains("where am i racing", StringComparison.Ordinal)
                || normalized.Contains("track am i on", StringComparison.Ordinal)
                || normalized.Contains("circuit am i on", StringComparison.Ordinal)
                || normalized.Contains("track am i driving", StringComparison.Ordinal))
            && !ContainsPositionIntent(normalized);
    }

    public static bool LooksLikeCarIdentity(string text)
    {
        var normalized = text.Trim().ToLowerInvariant();
        if (ContainsAny(normalized, CarIdentityPhrases))
        {
            return true;
        }

        return (normalized.Contains("which car", StringComparison.Ordinal)
                || normalized.Contains("what car", StringComparison.Ordinal)
                || normalized.Contains("car am i in", StringComparison.Ordinal)
                || normalized.Contains("car am i driving", StringComparison.Ordinal))
            && !normalized.Contains("car ahead", StringComparison.Ordinal)
            && !normalized.Contains("car behind", StringComparison.Ordinal);
    }

    public static bool LooksLikeTrackRelatedQuery(string question)
    {
        var text = question.Trim().ToLowerInvariant();
        if (ContainsAny(text, NonIdentityTrackPhrases))
        {
            return false;
        }

        return ClassifySubtopic(text) == RaceAwarenessSubtopic.TrackIdentity
            || LooksLikeTrackIdentity(text);
    }

    public static bool LooksLikePositionQuery(string question)
    {
        var text = question.Trim().ToLowerInvariant();
        return ClassifySubtopic(text) == RaceAwarenessSubtopic.Position;
    }

    private static bool ContainsPositionIntent(string text) =>
        text.Contains("position", StringComparison.Ordinal)
            || text.Contains("pozicija", StringComparison.Ordinal)
            || text.Contains("place am i", StringComparison.Ordinal);

    public static CoachQueryTopic ToPrimaryTopic(RaceAwarenessSubtopic subtopic) =>
        subtopic switch
        {
            RaceAwarenessSubtopic.TrackIdentity => CoachQueryTopic.TrackIdentity,
            RaceAwarenessSubtopic.CarIdentity => CoachQueryTopic.CarIdentity,
            RaceAwarenessSubtopic.Position => CoachQueryTopic.Position,
            RaceAwarenessSubtopic.HistoricalComparison => CoachQueryTopic.TrackMemory,
            _ => CoachQueryTopic.RaceAwareness
        };

    private static bool ContainsAny(string text, IEnumerable<string> phrases)
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
