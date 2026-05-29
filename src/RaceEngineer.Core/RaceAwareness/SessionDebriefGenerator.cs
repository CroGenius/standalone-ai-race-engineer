using System.Globalization;
using System.Text;

namespace RaceEngineer.Core.RaceAwareness;

public static class SessionDebriefGenerator
{
    public static SessionDebrief Generate(SessionMemorySummary summary, SessionMemoryBuildInput? input = null)
    {
        var strengths = BuildStrengths(summary, input);
        var weaknesses = BuildWeaknesses(summary);
        var fuelAnalysis = BuildFuelAnalysis(summary);
        var tyreAnalysis = BuildTyreAnalysis(summary);
        var brakingAnalysis = BuildBrakingAnalysis(summary);
        var throttleAnalysis = BuildThrottleAnalysis(summary);
        var consistencyAnalysis = BuildConsistencyAnalysis(summary);
        var strategyNotes = summary.StrategyNotes.Count > 0
            ? string.Join(" ", summary.StrategyNotes)
            : "No strategy notes were recorded for this session.";
        var nextActions = summary.ImprovementTargets.Count > 0
            ? summary.ImprovementTargets.Take(3).ToArray()
            : weaknesses.Take(3).ToArray();

        return new SessionDebrief(
            strengths,
            weaknesses,
            fuelAnalysis,
            tyreAnalysis,
            brakingAnalysis,
            throttleAnalysis,
            consistencyAnalysis,
            strategyNotes,
            nextActions,
            BuildMarkdown(
                summary,
                strengths,
                weaknesses,
                fuelAnalysis,
                tyreAnalysis,
                brakingAnalysis,
                throttleAnalysis,
                consistencyAnalysis,
                strategyNotes,
                nextActions));
    }

    private static IReadOnlyList<string> BuildStrengths(SessionMemorySummary summary, SessionMemoryBuildInput? input)
    {
        var strengths = new List<string>();
        if (summary.ConsistencyScore is >= 75)
        {
            strengths.Add($"Consistency score was {summary.ConsistencyScore.Value.ToString("0", CultureInfo.InvariantCulture)}/100.");
        }

        if (summary.Incidents.Count == 0)
        {
            strengths.Add("No recurring incident patterns were recorded.");
        }

        strengths.AddRange(input?.Analytics?.DriverProfile.Strengths ?? []);
        if (summary.BestLapSeconds.HasValue && summary.AverageCleanLapSeconds.HasValue
            && summary.BestLapSeconds.Value + 0.75 >= summary.AverageCleanLapSeconds.Value)
        {
            strengths.Add("Best lap stayed close to the average clean lap.");
        }

        return DistinctNonEmpty(strengths).Take(4).ToArray();
    }

    private static IReadOnlyList<string> BuildWeaknesses(SessionMemorySummary summary)
    {
        var weaknesses = new List<string>();
        weaknesses.AddRange(summary.BrakingWeaknesses);
        weaknesses.AddRange(summary.ThrottleWeaknesses);
        weaknesses.AddRange(summary.MainTimeLossZones);
        weaknesses.AddRange(summary.Incidents);
        weaknesses.AddRange(summary.ImprovementTargets);
        return DistinctNonEmpty(weaknesses).Take(6).ToArray();
    }

    private static string BuildFuelAnalysis(SessionMemorySummary summary) =>
        summary.FuelUsedPerLap is { } fuel
            ? $"Fuel use was about {SessionMemoryFormatting.FormatFuelPerLap(fuel)}."
            : "Fuel use per lap is unavailable for this session.";

    private static string BuildTyreAnalysis(SessionMemorySummary summary)
    {
        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(summary.TyreWarmupNotes))
        {
            parts.Add($"Warmup: {summary.TyreWarmupNotes}");
        }

        if (!string.IsNullOrWhiteSpace(summary.TyreDegradationNotes))
        {
            parts.Add($"Degradation/fade: {summary.TyreDegradationNotes}");
        }

        return parts.Count > 0
            ? string.Join(" ", parts)
            : "Tyre warmup and degradation notes are unavailable for this session.";
    }

    private static string BuildBrakingAnalysis(SessionMemorySummary summary) =>
        summary.BrakingWeaknesses.Count > 0
            ? string.Join("; ", summary.BrakingWeaknesses)
            : "No braking weaknesses were recorded for this session.";

    private static string BuildThrottleAnalysis(SessionMemorySummary summary) =>
        summary.ThrottleWeaknesses.Count > 0
            ? string.Join("; ", summary.ThrottleWeaknesses)
            : "No throttle weaknesses were recorded for this session.";

    private static string BuildConsistencyAnalysis(SessionMemorySummary summary) =>
        summary.ConsistencyScore is { } score
            ? $"Consistency score was {score.ToString("0", CultureInfo.InvariantCulture)}/100."
            : "Consistency score is unavailable for this session.";

    private static string BuildMarkdown(
        SessionMemorySummary summary,
        IReadOnlyList<string> strengths,
        IReadOnlyList<string> weaknesses,
        string fuelAnalysis,
        string tyreAnalysis,
        string brakingAnalysis,
        string throttleAnalysis,
        string consistencyAnalysis,
        string strategyNotes,
        IReadOnlyList<string> nextActions)
    {
        var builder = new StringBuilder();
        builder.AppendLine("# Session Debrief");
        builder.AppendLine();
        builder.AppendLine("## Session");
        builder.AppendLine($"- Track: {summary.TrackName}");
        builder.AppendLine($"- Car: {summary.CarName}");
        builder.AppendLine($"- Session type: {summary.SessionType ?? "unavailable"}");
        builder.AppendLine($"- Best lap: {SessionMemoryFormatting.FormatLapTime(summary.BestLapSeconds)}");
        builder.AppendLine($"- Average clean lap: {SessionMemoryFormatting.FormatLapTime(summary.AverageCleanLapSeconds)}");
        builder.AppendLine();
        builder.AppendLine("## Strengths");
        AppendBullets(builder, strengths, "No clear strengths were recorded.");
        builder.AppendLine("## Weaknesses");
        AppendBullets(builder, weaknesses, "No clear weaknesses were recorded.");
        builder.AppendLine("## Fuel analysis");
        builder.AppendLine($"- {fuelAnalysis}");
        builder.AppendLine("## Tyre analysis");
        builder.AppendLine($"- {tyreAnalysis}");
        builder.AppendLine("## Braking analysis");
        builder.AppendLine($"- {brakingAnalysis}");
        builder.AppendLine("## Throttle analysis");
        builder.AppendLine($"- {throttleAnalysis}");
        builder.AppendLine("## Consistency");
        builder.AppendLine($"- {consistencyAnalysis}");
        builder.AppendLine("## Strategy notes");
        builder.AppendLine($"- {strategyNotes}");
        builder.AppendLine("## Top 3 next-session actions");
        AppendBullets(builder, nextActions, "No next-session actions were recorded.");
        return builder.ToString().Trim();
    }

    private static void AppendBullets(StringBuilder builder, IReadOnlyList<string> items, string emptyMessage)
    {
        if (items.Count == 0)
        {
            builder.AppendLine($"- {emptyMessage}");
            return;
        }

        foreach (var item in items)
        {
            builder.AppendLine($"- {item}");
        }
    }

    private static IReadOnlyList<string> DistinctNonEmpty(IEnumerable<string> items) =>
        items.Where(item => !string.IsNullOrWhiteSpace(item)).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
}
