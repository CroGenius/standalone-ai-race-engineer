namespace RaceEngineer.Core.Coaching;

public static class DriverCoachingAnswerBuilder
{
    public static string Build(string question, DriverCoachingRecommendation coaching)
    {
        if (!coaching.HasData)
        {
            return coaching.Availability;
        }

        var text = question.ToLowerInvariant();
        if (ContainsAny(text, "where am i losing", "losing time", "lose time", "gdje gubim", "time loss", "which sector", "what sector"))
        {
            return BuildLosingTime(coaching);
        }

        if (ContainsAny(text, "am i improving", "getting faster", "getting slower"))
        {
            return BuildProgress(coaching);
        }

        if (ContainsAny(text, "how is my consistency", "my consistency", "consistency"))
        {
            return BuildConsistency(coaching);
        }

        if (ContainsAny(text, "weakest point", "biggest weakness", "main weakness"))
        {
            return BuildWeakestPoint(coaching);
        }

        if (ContainsAny(text, "better than last", "doing better", "compared to last session", "better than before"))
        {
            return BuildBetterThanLastTime(coaching);
        }

        if (ContainsAny(text, "what should i improve", "what should i work on", "what to improve", "sto trebam popraviti"))
        {
            return BuildImprovement(coaching);
        }

        return coaching.Summary;
    }

    public static string BuildLosingTime(DriverCoachingRecommendation coaching)
    {
        if (coaching.TopCoachingTargets.FirstOrDefault(item =>
                item.Contains("throttle", StringComparison.OrdinalIgnoreCase)) is { } throttleTarget)
        {
            return throttleTarget;
        }

        if (!string.IsNullOrWhiteSpace(coaching.WeakestArea))
        {
            return $"Your main loss is in {coaching.WeakestArea}.";
        }

        return coaching.CoachingInsights.FirstOrDefault()
            ?? coaching.Summary;
    }

    public static string BuildImprovement(DriverCoachingRecommendation coaching)
    {
        if (coaching.TopCoachingTargets.Count > 0)
        {
            return $"Focus on: {string.Join("; ", coaching.TopCoachingTargets)}.";
        }

        return coaching.BiggestWeakness is { } weakness
            ? $"Your main improvement target is {weakness}."
            : coaching.Summary;
    }

    public static string BuildProgress(DriverCoachingRecommendation coaching) =>
        !string.IsNullOrWhiteSpace(coaching.PreviousSessionDeltaSummary)
            ? $"{coaching.ProgressTrendSummary} {coaching.PreviousSessionDeltaSummary}"
            : coaching.ProgressTrendSummary;

    public static string BuildConsistency(DriverCoachingRecommendation coaching)
    {
        var consistency = coaching.ConsistencySummary ?? "Consistency data is limited.";
        if (coaching.ProgressTrend == DriverProgressTrend.Inconsistent)
        {
            return $"Consistency is inconsistent; {consistency}";
        }

        if (coaching.ProgressTrend == DriverProgressTrend.Improving)
        {
            return $"Consistency is improving, but {consistency.ToLowerInvariant()}";
        }

        return consistency;
    }

    public static string BuildWeakestPoint(DriverCoachingRecommendation coaching) =>
        coaching.BiggestWeakness
            ?? coaching.WeakestArea
            ?? coaching.RepeatedWeaknesses.FirstOrDefault()
            ?? "No clear weakest point yet; keep building clean laps.";

    public static string BuildBetterThanLastTime(DriverCoachingRecommendation coaching)
    {
        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(coaching.PreviousSessionDeltaSummary))
        {
            parts.Add(coaching.PreviousSessionDeltaSummary);
        }

        if (!string.IsNullOrWhiteSpace(coaching.StrongestArea))
        {
            parts.Add($"Strongest area today is {coaching.StrongestArea}.");
        }

        if (coaching.ProgressTrend == DriverProgressTrend.Improving)
        {
            parts.Add(coaching.ProgressTrendSummary);
        }

        return parts.Count > 0
            ? string.Join(" ", parts)
            : "Not enough previous-session data to compare yet.";
    }

    private static bool ContainsAny(string text, params string[] phrases) =>
        phrases.Any(phrase => text.Contains(phrase, StringComparison.Ordinal));
}
