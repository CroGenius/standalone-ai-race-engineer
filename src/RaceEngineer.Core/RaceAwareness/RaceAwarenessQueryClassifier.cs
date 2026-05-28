using RaceEngineer.Core.Coaching;

namespace RaceEngineer.Core.RaceAwareness;

public static class RaceAwarenessQueryClassifier
{
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
        "which track am i driving",
        "koja je staza",
        "koja je ova staza",
        "na kojoj sam stazi",
        "na kojoj stazi sam"
    ];

    private static readonly string[] CarIdentityPhrases =
    [
        "which car am i in",
        "what car am i in",
        "which car am i driving",
        "what car am i driving",
        "what car is this",
        "which car is this",
        "koji auto vozim",
        "koji auto je ovo",
        "u kojem sam autu",
        "koje auto vozim"
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
        var subtopic = ClassifySubtopic(question);
        return RaceAwarenessRoutingResult.ForSubtopic(subtopic, raceContext);
    }

    public static RaceAwarenessSubtopic ClassifySubtopic(string question)
    {
        var text = question.Trim().ToLowerInvariant();

        if (ContainsAny(text, CoachQueryPhrases.TrackMemory))
        {
            return RaceAwarenessSubtopic.HistoricalComparison;
        }

        if (IsTrackIdentityQuery(text))
        {
            return RaceAwarenessSubtopic.TrackIdentity;
        }

        if (IsCarIdentityQuery(text))
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

        if (ContainsAny(text, PositionPhrases) && !IsTrackIdentityQuery(text))
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

    public static bool IsTrackIdentityQuery(string text)
    {
        var normalized = text.Trim().ToLowerInvariant();
        if (ContainsAny(normalized, NonIdentityTrackPhrases))
        {
            return false;
        }

        if (ContainsAny(normalized, TrackIdentityPhrases)
            || ContainsAny(normalized, CoachQueryPhrases.TrackIdentity))
        {
            return true;
        }

        return LooksLikeTrackIdentity(normalized);
    }

    public static bool IsCarIdentityQuery(string text)
    {
        var normalized = text.Trim().ToLowerInvariant();
        if (IsTrackIdentityQuery(normalized))
        {
            return false;
        }

        if (ContainsAny(normalized, CarIdentityPhrases)
            || ContainsAny(normalized, CoachQueryPhrases.CarIdentity))
        {
            return true;
        }

        return LooksLikeCarIdentity(normalized);
    }

    public static bool LooksLikeTrackIdentity(string text)
    {
        var normalized = text.Trim().ToLowerInvariant();
        if (ContainsAny(normalized, NonIdentityTrackPhrases) || ContainsCarIdentityKeyword(normalized))
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
                || normalized.Contains("track am i driving", StringComparison.Ordinal)
                || normalized.Contains("koja je staza", StringComparison.Ordinal)
                || normalized.Contains("na kojoj sam stazi", StringComparison.Ordinal)
                || normalized.Contains("na kojoj stazi sam", StringComparison.Ordinal))
            && ContainsTrackIdentityKeyword(normalized)
            && !ContainsPositionIntent(normalized);
    }

    public static bool LooksLikeCarIdentity(string text)
    {
        var normalized = text.Trim().ToLowerInvariant();
        if (IsTrackIdentityQuery(normalized))
        {
            return false;
        }

        if (ContainsAny(normalized, CarIdentityPhrases))
        {
            return true;
        }

        return (normalized.Contains("which car", StringComparison.Ordinal)
                || normalized.Contains("what car", StringComparison.Ordinal)
                || normalized.Contains("car am i in", StringComparison.Ordinal)
                || normalized.Contains("car am i driving", StringComparison.Ordinal)
                || normalized.Contains("koji auto", StringComparison.Ordinal)
                || normalized.Contains("u kojem sam autu", StringComparison.Ordinal))
            && ContainsCarIdentityKeyword(normalized)
            && !normalized.Contains("car ahead", StringComparison.Ordinal)
            && !normalized.Contains("car behind", StringComparison.Ordinal);
    }

    public static bool LooksLikeTrackRelatedQuery(string question) =>
        IsTrackIdentityQuery(question);

    public static bool LooksLikeCarRelatedQuery(string question) =>
        IsCarIdentityQuery(question);

    public static bool LooksLikePositionQuery(string question) =>
        ClassifySubtopic(question) == RaceAwarenessSubtopic.Position;

    public static CoachQueryTopic ToPrimaryTopic(RaceAwarenessSubtopic subtopic) =>
        subtopic switch
        {
            RaceAwarenessSubtopic.TrackIdentity => CoachQueryTopic.TrackIdentity,
            RaceAwarenessSubtopic.CarIdentity => CoachQueryTopic.CarIdentity,
            RaceAwarenessSubtopic.Position => CoachQueryTopic.Position,
            RaceAwarenessSubtopic.HistoricalComparison => CoachQueryTopic.TrackMemory,
            _ => CoachQueryTopic.RaceAwareness
        };

    private static bool ContainsTrackIdentityKeyword(string text) =>
        text.Contains("track", StringComparison.Ordinal)
            || text.Contains("circuit", StringComparison.Ordinal)
            || text.Contains("staza", StringComparison.Ordinal)
            || text.Contains("stazi", StringComparison.Ordinal);

    private static bool ContainsCarIdentityKeyword(string text) =>
        text.Contains(" car", StringComparison.Ordinal)
            || text.StartsWith("car ", StringComparison.Ordinal)
            || text.Contains("auto", StringComparison.Ordinal);

    private static bool ContainsPositionIntent(string text) =>
        text.Contains("position", StringComparison.Ordinal)
            || text.Contains("pozicija", StringComparison.Ordinal)
            || text.Contains("place am i", StringComparison.Ordinal);

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
