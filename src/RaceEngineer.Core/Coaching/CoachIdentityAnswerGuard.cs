using RaceEngineer.Core.Analytics;
using RaceEngineer.Core.RaceAwareness;

namespace RaceEngineer.Core.Coaching;

public static class CoachIdentityAnswerGuard
{
    public static bool LooksLikeTrackIdentityAnswer(string content, LiveRaceContext? raceContext = null)
    {
        if (content.Contains("You are on ", StringComparison.OrdinalIgnoreCase)
            || content.Contains("Track is ", StringComparison.OrdinalIgnoreCase)
            || content.Contains(RaceAwarenessAnswerBuilder.TrackUnavailableMessage, StringComparison.OrdinalIgnoreCase)
            || content.Contains("track:", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return raceContext?.TrackName is { Length: > 0 } track
            && content.Contains(track, StringComparison.OrdinalIgnoreCase)
            && !LooksLikeBareCarNameAnswer(content);
    }

    public static bool LooksLikeCarIdentityAnswer(string content, LiveRaceContext? raceContext = null)
    {
        if (content.Contains("car:", StringComparison.OrdinalIgnoreCase)
            || content.Contains("Car is ", StringComparison.OrdinalIgnoreCase)
            || content.Contains(RaceAwarenessAnswerBuilder.CarUnavailableMessage, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (LooksLikeBareCarNameAnswer(content))
        {
            return true;
        }

        return raceContext?.CarName is { Length: > 0 } car
            && content.Contains(car, StringComparison.OrdinalIgnoreCase)
            && !LooksLikeTrackIdentityAnswer(content, raceContext);
    }

    public static bool LooksLikeIdentityAnswer(string content, LiveRaceContext? raceContext = null) =>
        LooksLikeTrackIdentityAnswer(content, raceContext)
            || LooksLikeCarIdentityAnswer(content, raceContext);

    public static bool LooksLikeBareCarNameAnswer(string content)
    {
        var action = DrivingTechniqueOutputSanitizer.ExtractActionText(content).Trim();
        if (!action.EndsWith('.'))
        {
            return false;
        }

        var sentence = action.TrimEnd('.').Trim();
        if (sentence.Length == 0)
        {
            return false;
        }

        if (ContainsTechnicalCoachPhrasing(sentence))
        {
            return false;
        }

        return sentence.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length <= 6;
    }

    public static string ReplacementForTopic(CoachQueryTopic topic) =>
        topic switch
        {
            CoachQueryTopic.Tyre or CoachQueryTopic.PushConfidence =>
                "Tyre status is unavailable.",
            CoachQueryTopic.LosingTime
                or CoachQueryTopic.Improvement
                or CoachQueryTopic.LapComparison
                or CoachQueryTopic.RacePace
                or CoachQueryTopic.Braking
                or CoachQueryTopic.Throttle =>
                SessionDriverPerformance.NeedCleanLapMessage,
            _ => SessionDriverPerformance.NeedCleanLapMessage
        };

    private static bool ContainsTechnicalCoachPhrasing(string sentence) =>
        sentence.Contains("tyre", StringComparison.OrdinalIgnoreCase)
            || sentence.Contains("tire", StringComparison.OrdinalIgnoreCase)
            || sentence.Contains("gum", StringComparison.OrdinalIgnoreCase)
            || sentence.Contains("fuel", StringComparison.OrdinalIgnoreCase)
            || sentence.Contains("brak", StringComparison.OrdinalIgnoreCase)
            || sentence.Contains("throttle", StringComparison.OrdinalIgnoreCase)
            || sentence.Contains("sector", StringComparison.OrdinalIgnoreCase)
            || sentence.Contains("lap", StringComparison.OrdinalIgnoreCase)
            || sentence.Contains("losing", StringComparison.OrdinalIgnoreCase)
            || sentence.Contains("You are on ", StringComparison.OrdinalIgnoreCase)
            || sentence.Contains("You are P", StringComparison.OrdinalIgnoreCase)
            || sentence.Contains("Position", StringComparison.OrdinalIgnoreCase);
}
