using System.Text;
using System.Text.RegularExpressions;

namespace RaceEngineer.Core.Coaching;

public static class CoachResponsePrioritizer
{
    public const int MaxPriorityPoints = 3;
    public const string PrioritiesHeader = "Top priorities:";
    public const string SupportingEvidenceNote = "Supporting evidence available.";

    public static bool IsDetailedModeRequest(string? query)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return false;
        }

        var text = query.ToLowerInvariant();
        return text.Contains("explain", StringComparison.Ordinal)
            || text.Contains("show evidence", StringComparison.Ordinal)
            || text.Contains("show me evidence", StringComparison.Ordinal)
            || text.Contains("full evidence", StringComparison.Ordinal)
            || text.Contains("why ", StringComparison.Ordinal)
            || text.Contains(" why", StringComparison.Ordinal)
            || text.StartsWith("why", StringComparison.Ordinal)
            || text.Contains("details", StringComparison.Ordinal)
            || text.Contains("detail ", StringComparison.Ordinal)
            || text.EndsWith(" detail", StringComparison.Ordinal);
    }

    public static bool ShouldPrioritizeResponse(string? query)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return false;
        }

        return CoachQueryTopicClassifier.ClassifyPrimary(query) is CoachQueryTopic.LosingTime
            or CoachQueryTopic.Braking
            or CoachQueryTopic.Throttle
            or CoachQueryTopic.Improvement
            or CoachQueryTopic.LapComparison
            or CoachQueryTopic.RacePace
            or CoachQueryTopic.TrackMemory
            or CoachQueryTopic.TrackGuide
            or CoachQueryTopic.Incidents
            or CoachQueryTopic.FuelStrategy
            or CoachQueryTopic.Strategy
            or CoachQueryTopic.Pit;
    }

    public static bool IsCompactPresentation(CoachMessage message) =>
        message.Content.StartsWith(PrioritiesHeader, StringComparison.Ordinal);

    public static CoachMessage Apply(
        CoachMessage message,
        string query,
        CoachEvidenceBundle? evidence,
        bool? detailedMode = null)
    {
        if (detailedMode ?? IsDetailedModeRequest(query))
        {
            return message;
        }

        if (!ShouldPrioritizeResponse(query))
        {
            return message;
        }

        if (ShouldSkipPrioritization(message))
        {
            return message;
        }

        var points = CollectPriorityPoints(message, evidence);
        if (points.Count == 0)
        {
            return message;
        }

        return message with { Content = FormatPriorities(points) };
    }

    private static bool ShouldSkipPrioritization(CoachMessage message)
    {
        if (string.IsNullOrWhiteSpace(message.Content))
        {
            return true;
        }

        var content = message.Content;
        if (content.Contains("I didn't catch that", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (content.Contains("Drive a clean lap first", StringComparison.OrdinalIgnoreCase)
            || content.Contains("No braking data yet", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return !string.IsNullOrWhiteSpace(message.Uncertainty)
            && content.Contains("unavailable", StringComparison.OrdinalIgnoreCase)
            && content.Length < 260;
    }

    private static IReadOnlyList<string> CollectPriorityPoints(
        CoachMessage message,
        CoachEvidenceBundle? evidence)
    {
        var ranked = new List<(int Tier, double Confidence, string Text)>();

        var primary = ExtractPrimaryAction(message.Content);
        if (!string.IsNullOrWhiteSpace(primary))
        {
            ranked.Add((0, 0.99, primary));
        }

        if (evidence is not null)
        {
            foreach (var packet in evidence.Packets)
            {
                var point = FormatEvidencePoint(packet);
                if (string.IsNullOrWhiteSpace(point))
                {
                    continue;
                }

                ranked.Add((ClassifyEvidenceTier(packet), packet.Confidence, point));
            }
        }

        var embeddedEvidence = ExtractEmbeddedEvidenceLines(message.Content);
        foreach (var line in embeddedEvidence)
        {
            ranked.Add((6, 0.5, line));
        }

        return ranked
            .OrderBy(item => item.Tier)
            .ThenByDescending(item => item.Confidence)
            .Select(item => NormalizePoint(item.Text))
            .Where(item => item.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(MaxPriorityPoints)
            .ToArray();
    }

    private static int ClassifyEvidenceTier(CoachEvidencePacket packet)
    {
        if (packet.SourceType == CoachEvidenceSourceType.Telemetry)
        {
            return 0;
        }

        if (packet.Category is "DriverCoaching" or "Performance" or "LosingTime" or "DeltaTrace" or "Sector")
        {
            return 1;
        }

        if (packet.Category is "TyreIntelligence" or "Tyre" or "Event" or "Incident" or "Strategy"
            || packet.Severity.Equals("Warning", StringComparison.OrdinalIgnoreCase))
        {
            return 2;
        }

        if (packet.Category is "TrackMemory" or "SessionMemory")
        {
            return 3;
        }

        if (packet.Category == "WebResearch")
        {
            return 4;
        }

        if (packet.Category is "TrackCarKnowledge" or "TrackGuide" or "StrategyKnowledge" or "Knowledge")
        {
            return 5;
        }

        return 6;
    }

    private static string FormatEvidencePoint(CoachEvidencePacket packet)
    {
        var explanation = string.IsNullOrWhiteSpace(packet.Explanation) ? packet.Summary : packet.Explanation;
        explanation = StripSourcePrefix(explanation);
        if (string.IsNullOrWhiteSpace(explanation))
        {
            return string.Empty;
        }

        var label = ClassifyEvidenceTier(packet) switch
        {
            0 => "Live telemetry",
            3 => "Stored session memory",
            4 => "Cached research",
            5 => "Catalog knowledge",
            _ => null
        };

        return label is null ? Shorten(explanation) : $"{label}: {Shorten(explanation)}";
    }

    private static string ExtractPrimaryAction(string content)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return string.Empty;
        }

        var main = content;
        var evidenceIndex = main.IndexOf($"{Environment.NewLine}{Environment.NewLine}Evidence:", StringComparison.Ordinal);
        if (evidenceIndex >= 0)
        {
            main = main[..evidenceIndex];
        }

        main = main.Trim();
        if (main.StartsWith("Track-car knowledge for", StringComparison.OrdinalIgnoreCase)
            || main.StartsWith("Stored knowledge:", StringComparison.OrdinalIgnoreCase)
            || main.StartsWith("Cached research:", StringComparison.OrdinalIgnoreCase)
            || main.StartsWith("Cached track guide for", StringComparison.OrdinalIgnoreCase))
        {
            return ExtractActionSentence(main);
        }

        if (main.Contains("Live telemetry:", StringComparison.OrdinalIgnoreCase))
        {
            var live = main.Split("Live telemetry:", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (live.Length > 1)
            {
                return $"Live telemetry: {Shorten(live[^1])}";
            }
        }

        return ExtractActionSentence(main);
    }

    private static IEnumerable<string> ExtractEmbeddedEvidenceLines(string content)
    {
        var evidenceIndex = content.IndexOf($"{Environment.NewLine}{Environment.NewLine}Evidence:", StringComparison.Ordinal);
        if (evidenceIndex < 0)
        {
            yield break;
        }

        var block = content[(evidenceIndex + $"{Environment.NewLine}{Environment.NewLine}Evidence:".Length)..];
        foreach (var rawLine in block.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var line = rawLine.TrimStart('-', ' ').Trim();
            if (line.Length == 0 || line.StartsWith("unavailable", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            yield return Shorten(line);
        }
    }

    private static string ExtractActionSentence(string text)
    {
        var cleaned = Regex.Replace(text, @"\s+", " ").Trim();
        if (cleaned.Length == 0)
        {
            return string.Empty;
        }

        if (cleaned.Length <= 180)
        {
            return cleaned;
        }

        var sentence = Regex.Split(cleaned, @"(?<=[.!?])\s+")
            .FirstOrDefault(item => item.Length > 12);
        return Shorten(sentence ?? cleaned);
    }

    private static string StripSourcePrefix(string value)
    {
        var trimmed = value.Trim();
        if (trimmed.StartsWith("Stored knowledge:", StringComparison.OrdinalIgnoreCase))
        {
            return trimmed["Stored knowledge:".Length..].Trim();
        }

        if (trimmed.StartsWith("Cached research", StringComparison.OrdinalIgnoreCase))
        {
            return trimmed;
        }

        return trimmed;
    }

    private static string NormalizePoint(string value) =>
        Regex.Replace(value.Trim(), @"\s+", " ");

    private static string Shorten(string value)
    {
        var trimmed = value.Trim();
        return trimmed.Length <= 160 ? trimmed : trimmed[..157] + "...";
    }

    public static string FormatPriorities(IReadOnlyList<string> points)
    {
        var builder = new StringBuilder();
        builder.AppendLine(PrioritiesHeader);
        for (var index = 0; index < points.Count; index++)
        {
            builder.Append(index + 1)
                .Append(". ")
                .AppendLine(points[index]);
        }

        builder.AppendLine();
        builder.Append(SupportingEvidenceNote);
        return builder.ToString().TrimEnd();
    }
}
