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

        if (ViolatesTrackIdentityContract(routing.Subtopic, query, message.Content, context?.RaceContext))
        {
            var corrected = RaceAwarenessAnswerBuilder.BuildTrackIdentity(
                context?.RaceContext,
                RaceAwarenessRoutingResult.ForSubtopic(RaceAwarenessSubtopic.TrackIdentity, context?.RaceContext),
                context?.RacePrepPlan?.Track);
            return ToCoachMessage(corrected, message.Uncertainty);
        }

        if (ViolatesCarIdentityContract(routing.Subtopic, query, message.Content, context?.RaceContext))
        {
            var corrected = RaceAwarenessAnswerBuilder.BuildCarIdentity(
                context?.RaceContext,
                RaceAwarenessRoutingResult.ForSubtopic(RaceAwarenessSubtopic.CarIdentity, context?.RaceContext),
                context?.RacePrepPlan?.Car);
            return ToCoachMessage(corrected, message.Uncertainty);
        }

        if (ViolatesPositionContract(routing.Subtopic, query, message.Content))
        {
            var corrected = RaceAwarenessAnswerBuilder.BuildPosition(
                session,
                context?.RaceContext,
                RaceAwarenessRoutingResult.ForSubtopic(RaceAwarenessSubtopic.Position, context?.RaceContext));
            return ToCoachMessage(corrected, message.Uncertainty);
        }

        return message;
    }

    public static bool ViolatesTrackIdentityContract(
        RaceAwarenessSubtopic subtopic,
        string query,
        string content,
        LiveRaceContext? raceContext)
    {
        if (subtopic != RaceAwarenessSubtopic.TrackIdentity
            && !RaceAwarenessQueryClassifier.LooksLikeTrackRelatedQuery(query))
        {
            return false;
        }

        if (ContainsValidTrackAnswer(content) && !ContainsCarLeak(content, raceContext))
        {
            return false;
        }

        if (ContainsUnavailableTrackMessage(content) && !ContainsCarLeak(content, raceContext))
        {
            return false;
        }

        return true;
    }

    public static bool ViolatesCarIdentityContract(
        RaceAwarenessSubtopic subtopic,
        string query,
        string content,
        LiveRaceContext? raceContext)
    {
        if (subtopic != RaceAwarenessSubtopic.CarIdentity
            && !RaceAwarenessQueryClassifier.LooksLikeCarRelatedQuery(query))
        {
            return false;
        }

        if (ContainsValidCarAnswer(content) && !ContainsTrackLeak(content, raceContext))
        {
            return false;
        }

        if (ContainsUnavailableCarMessage(content) && !ContainsTrackLeak(content, raceContext))
        {
            return false;
        }

        return true;
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

        if (RaceAwarenessQueryClassifier.LooksLikeTrackRelatedQuery(query)
            || RaceAwarenessQueryClassifier.LooksLikeCarRelatedQuery(query))
        {
            return false;
        }

        return ContainsTrackLeak(content, null) && !ContainsPositionAnswer(content);
    }

    private static bool RequiresValidation(RaceAwarenessSubtopic subtopic, string query) =>
        subtopic is RaceAwarenessSubtopic.TrackIdentity
            or RaceAwarenessSubtopic.CarIdentity
            or RaceAwarenessSubtopic.Position
            or RaceAwarenessSubtopic.GapAhead
            or RaceAwarenessSubtopic.GapBehind
            or RaceAwarenessSubtopic.SessionType
            or RaceAwarenessSubtopic.OpponentCount
            or RaceAwarenessSubtopic.RaceContext
        || RaceAwarenessQueryClassifier.LooksLikeTrackRelatedQuery(query)
        || RaceAwarenessQueryClassifier.LooksLikeCarRelatedQuery(query)
        || RaceAwarenessQueryClassifier.LooksLikePositionQuery(query);

    private static bool ContainsValidTrackAnswer(string content) =>
        content.Contains("You are on ", StringComparison.OrdinalIgnoreCase)
            || content.Contains("Track is ", StringComparison.OrdinalIgnoreCase);

    private static bool ContainsValidCarAnswer(string content) =>
        content.Contains("car:", StringComparison.OrdinalIgnoreCase)
            || (content.Contains('.', StringComparison.Ordinal)
                && !ContainsValidTrackAnswer(content)
                && !ContainsUnavailableTrackMessage(content)
                && !ContainsPositionAnswer(content));

    private static bool ContainsUnavailableTrackMessage(string content) =>
        content.Contains(RaceAwarenessAnswerBuilder.TrackUnavailableMessage, StringComparison.OrdinalIgnoreCase);

    private static bool ContainsUnavailableCarMessage(string content) =>
        content.Contains(RaceAwarenessAnswerBuilder.CarUnavailableMessage, StringComparison.OrdinalIgnoreCase);

    private static bool ContainsPositionAnswer(string content) =>
        content.Contains("You are P", StringComparison.OrdinalIgnoreCase)
            || content.Contains(RaceAwarenessAnswerBuilder.PositionUnavailableMessage, StringComparison.OrdinalIgnoreCase)
            || content.Contains("Position data is unavailable", StringComparison.OrdinalIgnoreCase);

    private static bool ContainsCarLeak(string content, LiveRaceContext? raceContext) =>
        content.Contains("car:", StringComparison.OrdinalIgnoreCase)
            || content.Contains("Car is ", StringComparison.OrdinalIgnoreCase)
            || (raceContext?.CarName is { Length: > 0 } car
                && content.Contains(car, StringComparison.OrdinalIgnoreCase)
                && !ContainsValidTrackAnswer(content));

    private static bool ContainsTrackLeak(string content, LiveRaceContext? raceContext) =>
        content.Contains("You are on ", StringComparison.OrdinalIgnoreCase)
            || content.Contains("Track is ", StringComparison.OrdinalIgnoreCase)
            || content.Contains("track:", StringComparison.OrdinalIgnoreCase)
            || (raceContext?.TrackName is { Length: > 0 } track
                && content.Contains(track, StringComparison.OrdinalIgnoreCase)
                && !ContainsValidCarAnswer(content));

    private static CoachMessage ToCoachMessage(RaceAwarenessAnswer answer, string? uncertainty) =>
        new("coach", answer.Content, [], uncertainty ?? answer.Routing.FallbackReason, []);
}
