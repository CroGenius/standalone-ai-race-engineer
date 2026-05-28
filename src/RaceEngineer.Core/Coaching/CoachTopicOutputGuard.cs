namespace RaceEngineer.Core.Coaching;

using RaceEngineer.Core.RaceAwareness;

public static class CoachTopicOutputGuard
{
    private static readonly string[] FuelForbiddenTerms =
    [
        "brake",
        "braking",
        "throttle",
        "tyre",
        "tire",
        "grip",
        "sliding",
        "corner",
        "InvalidLap"
    ];

    private static readonly string[] PushForbiddenLeadTerms =
    [
        "Front-left",
        "Front-right",
        "Rear-left",
        "Rear-right",
        "All four tyres"
    ];

    public static CoachMessage EnforceTopicIsolation(
        CoachQueryTopic topic,
        CoachMessage message,
        LiveRaceContext? raceContext = null)
    {
        message = RejectIdentityBleed(topic, message, raceContext);

        var action = DrivingTechniqueOutputSanitizer.ExtractActionText(message.Content);
        var cleaned = topic switch
        {
            CoachQueryTopic.FuelStrategy => SanitizeFuelStrategy(action),
            CoachQueryTopic.PushConfidence => SanitizePushConfidence(action),
            CoachQueryTopic.Tyre => SanitizeTyreCondition(action),
            CoachQueryTopic.Throttle => SanitizeThrottle(action),
            CoachQueryTopic.Braking => SanitizeBraking(action),
            CoachQueryTopic.TrackIdentity => SanitizeTrackIdentity(action),
            CoachQueryTopic.CarIdentity => SanitizeCarIdentity(action),
            CoachQueryTopic.Position => SanitizePosition(action),
            CoachQueryTopic.RaceAwareness => SanitizeRaceAwareness(action),
            _ => action
        };

        if (string.Equals(cleaned, action, StringComparison.Ordinal))
        {
            return message;
        }

        return message with { Content = cleaned };
    }

    public static CoachMessage RejectIdentityBleed(
        CoachQueryTopic topic,
        CoachMessage message,
        LiveRaceContext? raceContext = null)
    {
        if (!CoachQueryTopicClassifier.BlocksIdentityRouting(topic))
        {
            return message;
        }

        var action = DrivingTechniqueOutputSanitizer.ExtractActionText(message.Content);
        if (!CoachIdentityAnswerGuard.LooksLikeIdentityAnswer(action, raceContext))
        {
            return message;
        }

        return message with { Content = CoachIdentityAnswerGuard.ReplacementForTopic(topic) };
    }

    private static string SanitizeFuelStrategy(string content)
    {
        if (!ContainsAny(content, FuelForbiddenTerms))
        {
            return content;
        }

        foreach (var term in FuelForbiddenTerms)
        {
            if (content.Contains(term, StringComparison.OrdinalIgnoreCase))
            {
                return "Fuel finish answer was blocked because unrelated telemetry leaked into the strategy response.";
            }
        }

        return content;
    }

    private static string SanitizePushConfidence(string content)
    {
        if (ContainsAny(content, PushForbiddenLeadTerms) && !content.Contains("grip", StringComparison.OrdinalIgnoreCase))
        {
            return "Grip confidence is still building. Push progressively until the car feels stable.";
        }

        if (content.Contains("latest fuel", StringComparison.OrdinalIgnoreCase)
            || content.Contains("laps remaining on current fuel", StringComparison.OrdinalIgnoreCase))
        {
            return "Grip confidence is still building. Push progressively until the car feels stable.";
        }

        return content;
    }

    private static string SanitizeTyreCondition(string content)
    {
        if (content.Contains("Safe to push", StringComparison.OrdinalIgnoreCase)
            || content.Contains("Push gradually", StringComparison.OrdinalIgnoreCase)
            || content.Contains("push harder", StringComparison.OrdinalIgnoreCase))
        {
            return content.Replace("Safe to push now.", "", StringComparison.OrdinalIgnoreCase).Trim();
        }

        return content;
    }

    private static string SanitizeThrottle(string content)
    {
        if (ContainsAny(content, "latest fuel", "fuel risk", "pit recommendation", "brake stability"))
        {
            return "Throttle traces are available, but unrelated strategy telemetry leaked into the answer.";
        }

        return content;
    }

    private static string SanitizeBraking(string content)
    {
        if (ContainsAny(content, "latest fuel", "fuel risk", "tyre readiness", "throttle smoothness"))
        {
            return "Braking feedback is available, but unrelated telemetry leaked into the answer.";
        }

        return content;
    }

    private static string SanitizeTrackIdentity(string content)
    {
        if (ContainsPositionLeak(content) && !ContainsTrackAnswer(content))
        {
            return RaceAwarenessAnswerBuilder.TrackUnavailableMessage;
        }

        return content;
    }

    private static string SanitizeCarIdentity(string content)
    {
        if (ContainsPositionLeak(content) && !ContainsCarAnswer(content))
        {
            return RaceAwarenessAnswerBuilder.CarUnavailableMessage;
        }

        return content;
    }

    private static string SanitizePosition(string content)
    {
        if (ContainsTrackLeak(content) && !ContainsPositionAnswer(content))
        {
            return RaceAwarenessAnswerBuilder.PositionUnavailableMessage;
        }

        return content;
    }

    private static string SanitizeRaceAwareness(string content) => content;

    private static bool ContainsTrackAnswer(string content) =>
        content.Contains("You are on ", StringComparison.OrdinalIgnoreCase)
            || content.Contains("Track is ", StringComparison.OrdinalIgnoreCase)
            || content.Contains(RaceAwarenessAnswerBuilder.TrackUnavailableMessage, StringComparison.OrdinalIgnoreCase);

    private static bool ContainsCarAnswer(string content) =>
        content.Contains(RaceAwarenessAnswerBuilder.CarUnavailableMessage, StringComparison.OrdinalIgnoreCase)
            || (content.Contains('.', StringComparison.Ordinal)
                && !ContainsTrackAnswer(content)
                && !ContainsPositionAnswer(content));

    private static bool ContainsPositionAnswer(string content) =>
        content.Contains("You are P", StringComparison.OrdinalIgnoreCase)
            || content.Contains(RaceAwarenessAnswerBuilder.PositionUnavailableMessage, StringComparison.OrdinalIgnoreCase);

    private static bool ContainsPositionLeak(string content) =>
        content.Contains("You are P", StringComparison.OrdinalIgnoreCase)
            || content.Contains("Race position", StringComparison.OrdinalIgnoreCase);

    private static bool ContainsTrackLeak(string content) =>
        content.Contains("You are on ", StringComparison.OrdinalIgnoreCase)
            || content.Contains("Track is ", StringComparison.OrdinalIgnoreCase)
            || content.Contains("track:", StringComparison.OrdinalIgnoreCase);

    private static bool ContainsCarLeak(string content) =>
        content.Contains("car:", StringComparison.OrdinalIgnoreCase)
            || content.Contains("Car is ", StringComparison.OrdinalIgnoreCase);

    private static bool ContainsAny(string content, params string[] terms)
    {
        foreach (var term in terms)
        {
            if (content.Contains(term, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }
}
