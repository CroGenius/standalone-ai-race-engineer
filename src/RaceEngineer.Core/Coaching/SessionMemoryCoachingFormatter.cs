using System.Globalization;
using System.Text.RegularExpressions;
using RaceEngineer.Core.Knowledge;
using RaceEngineer.Core.RaceAwareness;

namespace RaceEngineer.Core.Coaching;

public sealed record SessionWeaknessInsight(
    string Weakness,
    string Explanation,
    string Recommendation);

public static class SessionMemoryCoachingFormatter
{
    private static readonly Regex LossSecondsPattern = new(@"\((?<loss>[0-9]+(?:\.[0-9]+)?)s\)", RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex ZoneBehaviorPattern = new(
        @"^(?<location>[^:]+):\s*(?<behavior>.+?)(?:\s*\((?<loss>[0-9]+(?:\.[0-9]+)?)s\))?$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);
    private static readonly Regex InLocationPattern = new(
        @"(?<behavior>.+?)\s+in\s+(?<location>.+)$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    public static string BuildStruggleAnswer(SessionMemorySummary stored, TrackGuide? guide)
    {
        var insights = CollectInsights(stored, guide);
        if (insights.Count == 0)
        {
            return $"I do not have stored struggle points for your previous {stored.TrackName} session yet.";
        }

        var primary = insights[0];
        return
            $"During your previous {stored.TrackName} session your biggest weakness was {primary.Weakness}. " +
            $"{primary.Explanation} {primary.Recommendation}";
    }

    public static string BuildWatchAnswer(SessionMemorySummary stored, TrackGuide? guide)
    {
        var insights = CollectInsights(stored, guide);
        if (insights.Count == 0)
        {
            return $"I do not have stored watch points for your previous {stored.TrackName} session yet.";
        }

        var watchItems = insights
            .Take(3)
            .Select(item => item.Weakness)
            .ToArray();
        return
            $"From your previous {stored.TrackName} session, watch for {string.Join("; ", watchItems)}. " +
            $"{insights[0].Recommendation}";
    }

    public static string BuildImprovementFromStored(SessionMemorySummary stored, TrackGuide? guide)
    {
        var insights = CollectInsights(stored, guide);
        if (insights.Count == 0)
        {
            return $"Stored session data for {stored.TrackName} does not list improvement targets yet.";
        }

        var focus = insights.Take(3).Select(item => item.Weakness).ToArray();
        return
            $"From your previous {stored.TrackName} session, focus on {string.Join("; ", focus)}. " +
            $"{insights[0].Recommendation}";
    }

    public static IReadOnlyList<SessionWeaknessInsight> CollectInsights(
        SessionMemorySummary stored,
        TrackGuide? guide)
    {
        var rawItems = stored.MainTimeLossZones
            .Concat(stored.BrakingWeaknesses)
            .Concat(stored.ThrottleWeaknesses)
            .Concat(stored.ImprovementTargets)
            .Concat(stored.RepeatedWeaknesses ?? [])
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Distinct(StringComparer.OrdinalIgnoreCase);

        return rawItems
            .Select(item => FormatWeaknessItem(item, guide, stored.TrackName))
            .Where(item => !string.IsNullOrWhiteSpace(item.Weakness))
            .Take(4)
            .ToArray();
    }

    public static string FormatWeaknessItemForDisplay(string raw, TrackGuide? guide, string trackName) =>
        FormatWeaknessItem(raw, guide, trackName).Weakness;

    private static SessionWeaknessInsight FormatWeaknessItem(string raw, TrackGuide? guide, string trackName)
    {
        var cleaned = raw.Trim();
        var (location, behavior, lossSeconds) = ParseRawWeakness(cleaned);
        var humanLocation = ResolveHumanLocation(location, guide);
        var humanBehavior = HumanizeBehavior(behavior);
        var weakness = BuildWeaknessPhrase(humanBehavior, humanLocation);
        var explanation = lossSeconds is { } loss
            ? $"That cost roughly {loss.ToString("0.0", CultureInfo.InvariantCulture)}s per lap in that session."
            : $"That showed up repeatedly in your previous {trackName} session.";
        var recommendation = BuildRecommendation(humanBehavior, humanLocation);
        return new SessionWeaknessInsight(weakness, explanation, recommendation);
    }

    private static (string Location, string Behavior, double? LossSeconds) ParseRawWeakness(string raw)
    {
        var lossSeconds = TryParseLossSeconds(raw);
        var withoutLoss = LossSecondsPattern.Replace(raw, string.Empty).Trim();

        var zoneMatch = ZoneBehaviorPattern.Match(withoutLoss);
        if (zoneMatch.Success)
        {
            return (
                zoneMatch.Groups["location"].Value.Trim(),
                zoneMatch.Groups["behavior"].Value.Trim(),
                lossSeconds ?? TryParseDouble(zoneMatch.Groups["loss"].Value));
        }

        var inMatch = InLocationPattern.Match(withoutLoss);
        if (inMatch.Success)
        {
            return (
                inMatch.Groups["location"].Value.Trim(),
                inMatch.Groups["behavior"].Value.Trim(),
                lossSeconds);
        }

        return (string.Empty, withoutLoss, lossSeconds);
    }

    private static string BuildWeaknessPhrase(string behavior, string location)
    {
        if (string.IsNullOrWhiteSpace(location))
        {
            return behavior;
        }

        if (behavior.Contains("brak", StringComparison.OrdinalIgnoreCase))
        {
            return $"{behavior} into {location}";
        }

        if (behavior.Contains("throttle", StringComparison.OrdinalIgnoreCase)
            || behavior.Contains("traction", StringComparison.OrdinalIgnoreCase))
        {
            return $"{behavior} exiting {location}";
        }

        return $"{behavior} in {location}";
    }

    private static string BuildRecommendation(string behavior, string location)
    {
        if (behavior.Contains("brak", StringComparison.OrdinalIgnoreCase))
        {
            return string.IsNullOrWhiteSpace(location)
                ? "Focus on smoother brake release and consistent entry speed."
                : $"Focus on smoother brake release into {location}.";
        }

        if (behavior.Contains("throttle", StringComparison.OrdinalIgnoreCase)
            || behavior.Contains("traction", StringComparison.OrdinalIgnoreCase)
            || behavior.Contains("hesitation", StringComparison.OrdinalIgnoreCase))
        {
            return string.IsNullOrWhiteSpace(location)
                ? "Focus on smoother throttle pickup on corner exit."
                : $"Focus on smoother throttle pickup exiting {location}.";
        }

        if (behavior.Contains("consistency", StringComparison.OrdinalIgnoreCase)
            || behavior.Contains("spread", StringComparison.OrdinalIgnoreCase))
        {
            return "Focus on repeating your best lap rhythm lap after lap.";
        }

        return string.IsNullOrWhiteSpace(location)
            ? "Focus on one clean repeat of your best sequence before pushing harder."
            : $"Focus on reducing time loss through {location}.";
    }

    private static string ResolveHumanLocation(string location, TrackGuide? guide)
    {
        if (string.IsNullOrWhiteSpace(location))
        {
            return string.Empty;
        }

        var trimmed = location.Trim();
        if (guide?.Corners is { Count: > 0 })
        {
            var turnMatch = Regex.Match(trimmed, @"\b(?:turn|t)\s*(?<num>\d+)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
            if (turnMatch.Success
                && int.TryParse(turnMatch.Groups["num"].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var turnNumber))
            {
                var namedTurn = guide.Corners.FirstOrDefault(corner =>
                    corner.Name.Contains($"Turn {turnNumber}", StringComparison.OrdinalIgnoreCase)
                    || corner.Name.Contains($"T{turnNumber}", StringComparison.OrdinalIgnoreCase));
                if (namedTurn is not null)
                {
                    return namedTurn.Name;
                }
            }

            var zoneMatch = Regex.Match(trimmed, @"\bzone\s*(?<num>\d+)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
            if (zoneMatch.Success
                && int.TryParse(zoneMatch.Groups["num"].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var zoneNumber)
                && zoneNumber > 0
                && zoneNumber <= guide.Corners.Count)
            {
                return guide.Corners[zoneNumber - 1].Name;
            }

            var directCorner = guide.Corners.FirstOrDefault(corner =>
                trimmed.Contains(corner.Name, StringComparison.OrdinalIgnoreCase));
            if (directCorner is not null)
            {
                return directCorner.Name;
            }
        }

        if (Regex.IsMatch(trimmed, @"\bzone\s*\d+\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
        {
            return "that section of the lap";
        }

        if (trimmed.Contains("sector", StringComparison.OrdinalIgnoreCase))
        {
            var sectorNote = guide?.SectorNotes.FirstOrDefault(note =>
                note.Contains(trimmed, StringComparison.OrdinalIgnoreCase));
            if (!string.IsNullOrWhiteSpace(sectorNote))
            {
                return sectorNote.Split('.')[0].Trim();
            }

            return trimmed.Replace("delta", "pace", StringComparison.OrdinalIgnoreCase).Trim();
        }

        return trimmed;
    }

    private static string HumanizeBehavior(string behavior)
    {
        if (string.IsNullOrWhiteSpace(behavior))
        {
            return "inconsistent driving";
        }

        var normalized = behavior.Trim();
        normalized = Regex.Replace(normalized, @"([a-z])([A-Z])", "$1 $2");
        normalized = normalized.Replace('_', ' ');
        normalized = Regex.Replace(normalized, @"\s+", " ").Trim();

        var replacements = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["unstablebraking"] = "unstable braking",
            ["unstable braking"] = "unstable braking",
            ["abruptbrakerelease"] = "abrupt brake release",
            ["heavybraking"] = "heavy braking",
            ["unstable throttle"] = "unstable throttle application",
            ["time loss"] = "time loss",
            ["hesitation on throttle"] = "hesitant throttle application",
            ["sector 2 delta"] = "Sector 2 pace loss",
            ["sector 2 pace loss"] = "Sector 2 pace loss"
        };

        foreach (var pair in replacements)
        {
            if (normalized.Contains(pair.Key, StringComparison.OrdinalIgnoreCase))
            {
                normalized = Regex.Replace(
                    normalized,
                    Regex.Escape(pair.Key),
                    pair.Value,
                    RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
            }
        }

        return char.ToLowerInvariant(normalized[0]) + normalized[1..];
    }

    private static double? TryParseLossSeconds(string raw)
    {
        var match = LossSecondsPattern.Match(raw);
        return match.Success ? TryParseDouble(match.Groups["loss"].Value) : null;
    }

    private static double? TryParseDouble(string value) =>
        double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : null;
}
