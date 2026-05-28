using RaceEngineer.Core.RaceAwareness;
using RaceEngineer.Core.Session;

namespace RaceEngineer.Core.Coaching;

public static class RaceAwarenessAnswerValidator
{
    public static CoachMessage Enforce(
        string query,
        CoachMessage message,
        SessionState session,
        CoachContext? context)
    {
        var routing = RaceAwarenessQueryClassifier.Classify(query, context?.RaceContext);
        if (!RequiresValidation(routing.Subtopic, query))
        {
            return message;
        }

        if (ViolatesTrackIdentityContract(routing.Subtopic, query, message.Content))
        {
            var corrected = RaceAwarenessAnswerBuilder.BuildTrackIdentity(
                context?.RaceContext,
                routing,
                context?.RacePrepPlan?.Track);
            return ToCoachMessage(corrected, message.Uncertainty);
        }

        if (ViolatesPositionContract(routing.Subtopic, query, message.Content))
        {
            var corrected = RaceAwarenessAnswerBuilder.BuildPosition(session, context?.RaceContext, routing);
            return ToCoachMessage(corrected, message.Uncertainty);
        }

        return message;
    }

    public static bool ViolatesTrackIdentityContract(
        RaceAwarenessSubtopic subtopic,
        string query,
        string content)
    {
        if (subtopic != RaceAwarenessSubtopic.TrackIdentity
            && !RaceAwarenessQueryClassifier.LooksLikeTrackRelatedQuery(query))
        {
            return false;
        }

        if (ContainsTrackAnswer(content))
        {
            return ContainsPositionLeak(content);
        }

        return !ContainsUnavailableTrackMessage(content) && ContainsPositionLeak(content);
    }

    public static bool ViolatesPositionContract(
        RaceAwarenessSubtopic subtopic,
        string query,
        string content)
    {
        if (subtopic != RaceAwarenessSubtopic.Position
            && !RaceAwarenessQueryClassifier.LooksLikePositionQuery(query))
        {
            return false;
        }

        if (RaceAwarenessQueryClassifier.LooksLikeTrackIdentity(query))
        {
            return false;
        }

        return ContainsTrackLeak(content) && !ContainsPositionAnswer(content);
    }

    private static bool RequiresValidation(RaceAwarenessSubtopic subtopic, string query) =>
        subtopic is RaceAwarenessSubtopic.TrackIdentity
            or RaceAwarenessSubtopic.Position
            or RaceAwarenessSubtopic.GapAhead
            or RaceAwarenessSubtopic.GapBehind
            or RaceAwarenessSubtopic.SessionType
            or RaceAwarenessSubtopic.OpponentCount
            or RaceAwarenessSubtopic.RaceContext
        || RaceAwarenessQueryClassifier.LooksLikeTrackRelatedQuery(query)
        || RaceAwarenessQueryClassifier.LooksLikePositionQuery(query);

    private static bool ContainsTrackAnswer(string content) =>
        content.Contains("You are on ", StringComparison.OrdinalIgnoreCase)
            || content.Contains("Track is ", StringComparison.OrdinalIgnoreCase);

    private static bool ContainsUnavailableTrackMessage(string content) =>
        content.Contains(RaceAwarenessAnswerBuilder.TrackUnavailableMessage, StringComparison.OrdinalIgnoreCase);

    private static bool ContainsPositionAnswer(string content) =>
        content.Contains("You are P", StringComparison.OrdinalIgnoreCase)
            || content.Contains(RaceAwarenessAnswerBuilder.PositionUnavailableMessage, StringComparison.OrdinalIgnoreCase)
            || content.Contains("Position data is unavailable", StringComparison.OrdinalIgnoreCase);

    private static bool ContainsPositionLeak(string content) =>
        content.Contains("You are P", StringComparison.OrdinalIgnoreCase)
            || content.Contains("Race position", StringComparison.OrdinalIgnoreCase)
            || content.Contains("position is P", StringComparison.OrdinalIgnoreCase);

    private static bool ContainsTrackLeak(string content) =>
        content.Contains("You are on ", StringComparison.OrdinalIgnoreCase)
            || content.Contains("Track is ", StringComparison.OrdinalIgnoreCase)
            || content.Contains("track name", StringComparison.OrdinalIgnoreCase);

    private static CoachMessage ToCoachMessage(RaceAwarenessAnswer answer, string? uncertainty) =>
        new("coach", answer.Content, [], uncertainty ?? answer.Routing.FallbackReason, []);
}
