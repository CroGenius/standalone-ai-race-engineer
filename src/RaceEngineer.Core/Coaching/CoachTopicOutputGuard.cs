namespace RaceEngineer.Core.Coaching;

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

    public static CoachMessage EnforceTopicIsolation(CoachQueryTopic topic, CoachMessage message)
    {
        var action = DrivingTechniqueOutputSanitizer.ExtractActionText(message.Content);
        var cleaned = topic switch
        {
            CoachQueryTopic.FuelStrategy => SanitizeFuelStrategy(action),
            CoachQueryTopic.PushConfidence => SanitizePushConfidence(action),
            CoachQueryTopic.Tyre => SanitizeTyreCondition(action),
            CoachQueryTopic.Throttle => SanitizeThrottle(action),
            CoachQueryTopic.Braking => SanitizeBraking(action),
            _ => action
        };

        if (string.Equals(cleaned, action, StringComparison.Ordinal))
        {
            return message;
        }

        return message with { Content = cleaned };
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
