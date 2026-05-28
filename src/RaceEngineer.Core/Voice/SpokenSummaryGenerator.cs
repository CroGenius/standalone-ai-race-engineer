using System.Globalization;
using System.Text.RegularExpressions;
using RaceEngineer.Core.Coaching;
using RaceEngineer.Core.Profile;
using RaceEngineer.Core.SessionContext;

namespace RaceEngineer.Core.Voice;

public sealed record SpokenSummaryResult(
    string Summary,
    string OriginalAnswer,
    string GeneratedSummary,
    string Diagnostic);

public static class SpokenSummaryGenerator
{
    public static int ResolveMaxWords(CoachPreferencesRecord? preferences)
    {
        return preferences?.ResponseLength switch
        {
            "short" => 12,
            "detailed" => 18,
            _ => 14
        };
    }

    public static SpokenSummaryResult GenerateSpokenSummary(
        CoachMessage message,
        CoachPreferencesRecord? preferences = null,
        string? query = null)
    {
        var topic = query is null ? CoachQueryTopic.Unknown : CoachQueryTopicClassifier.ClassifyPrimary(query);
        var maxWords = ResolveMaxWords(preferences);
        var original = ExtractActionText(message.Content);
        var useEnglish = CoachSpokenLanguageResolver.UseEnglishSpokenOutput(preferences);

        var topicSummary = BuildTopicSpokenSummary(message, topic, maxWords, useEnglish);
        var fromContent = ExtractFirstActionableSentence(original);
        var candidate = SelectBestCandidate(topic, topicSummary, fromContent, original, message);

        if (string.IsNullOrWhiteSpace(candidate))
        {
            return new SpokenSummaryResult(
                "",
                original,
                "",
                $"no actionable sentence for topic {topic}");
        }

        var summary = VoiceText.LimitWords(CleanForSpeech(candidate), maxWords);
        summary = CoachSpokenLanguageResolver.ToSpokenLanguage(summary, message, topic, preferences);

        if (string.IsNullOrWhiteSpace(summary))
        {
            return new SpokenSummaryResult(
                "",
                original,
                "",
                "summary empty after word limit or language normalization");
        }

        return new SpokenSummaryResult(summary, original, summary, $"ok topic={topic}");
    }

    internal static string? BuildEnglishTopicSummary(CoachMessage message, CoachQueryTopic topic)
    {
        return BuildTopicSpokenSummary(message, topic, ResolveMaxWords(null), english: true);
    }

    public static string ComposeSpokenPayload(string summary, bool confirmQuery, int maxWords)
    {
        if (string.IsNullOrWhiteSpace(summary))
        {
            return "";
        }

        var answerWords = confirmQuery
            ? Math.Max(8, maxWords - 1)
            : maxWords;
        var limited = VoiceText.LimitWords(summary.Trim(), answerWords);
        if (string.IsNullOrWhiteSpace(limited))
        {
            return "";
        }

        return confirmQuery ? $"Copy. {limited}" : limited;
    }

    public static string BuildDiagnostic(SpokenSummaryResult summaryResult, string finalPayload)
    {
        return
            $"original='{TruncateForLog(summaryResult.OriginalAnswer)}' " +
            $"summary='{TruncateForLog(summaryResult.GeneratedSummary)}' " +
            $"tts='{TruncateForLog(finalPayload)}' " +
            $"reason={summaryResult.Diagnostic}";
    }

    private static string? BuildTopicSpokenSummary(
        CoachMessage message,
        CoachQueryTopic topic,
        int maxWords,
        bool english)
    {
        var action = ExtractActionText(message.Content);
        var packets = FilterPacketsByTopic(message.EvidencePackets, topic);

        return topic switch
        {
            CoachQueryTopic.Position => BuildPositionSummary(action, english),
            CoachQueryTopic.Tyre => BuildTyreSummary(action, packets, english),
            CoachQueryTopic.PushConfidence => BuildPushConfidenceSummary(action, english),
            CoachQueryTopic.LapTime => BuildLapTimeSummary(action, packets, english),
            CoachQueryTopic.FuelAmount => BuildFuelAmountSummary(action, packets, english),
            CoachQueryTopic.FuelConsumption => BuildFuelConsumptionSummary(action, packets, english),
            CoachQueryTopic.FuelStrategy => BuildFuelStrategySummary(action, packets, english),
            CoachQueryTopic.Strategy or CoachQueryTopic.Pit => BuildStrategySummary(action, packets, english),
            CoachQueryTopic.Braking => BuildBrakingSummary(action, packets, english),
            CoachQueryTopic.Throttle => BuildThrottleSummary(action, packets, english),
            CoachQueryTopic.RacePace => BuildGenericSummary(action, packets, english, "Pace"),
            CoachQueryTopic.LosingTime => BuildGenericSummary(action, packets, english, "Sector"),
            CoachQueryTopic.Improvement => BuildGenericSummary(action, packets, english, "Improvement"),
            CoachQueryTopic.LapComparison => BuildGenericSummary(action, packets, english, "Lap"),
            CoachQueryTopic.Incidents => BuildGenericSummary(action, packets, english, "Incident"),
            _ => BuildGenericSummary(action, packets, english, null)
        };
    }

    private static string? BuildPositionSummary(string action, bool english)
    {
        if (action.Contains("Position data is unavailable", StringComparison.OrdinalIgnoreCase))
        {
            return "Position data is unavailable.";
        }

        var match = Regex.Match(action, @"\bP(\d+)\b", RegexOptions.IgnoreCase);
        if (match.Success)
        {
            return $"You are P{match.Groups[1].Value}.";
        }

        match = Regex.Match(action, @"position:\s*(\d+)", RegexOptions.IgnoreCase);
        if (match.Success)
        {
            return $"You are P{match.Groups[1].Value}.";
        }

        return english && action.Contains("unavailable", StringComparison.OrdinalIgnoreCase)
            ? "Position data is unavailable."
            : ExtractFirstActionableSentence(action);
    }

    private static string? BuildTyreSummary(string action, IReadOnlyList<CoachEvidencePacket> packets, bool english)
    {
        if (action.Contains("Tyre data is not reliable yet", StringComparison.OrdinalIgnoreCase)
            || action.Contains("Tyre status is unavailable", StringComparison.OrdinalIgnoreCase))
        {
            return "Tyre data is not reliable yet.";
        }

        var spokenPacket = packets.FirstOrDefault(packet => packet.Summary == "Spoken tyre summary");
        if (spokenPacket is not null && !string.IsNullOrWhiteSpace(spokenPacket.Explanation))
        {
            return spokenPacket.Explanation;
        }

        if (action.Contains("Front-left", StringComparison.OrdinalIgnoreCase)
            || action.Contains("Front-right", StringComparison.OrdinalIgnoreCase)
            || action.Contains("Rear-left", StringComparison.OrdinalIgnoreCase)
            || action.Contains("Rear-right", StringComparison.OrdinalIgnoreCase))
        {
            return BuildTyreSpokenFromAction(action);
        }

        var packet = packets.FirstOrDefault(item =>
            item.Summary is "Tyre readiness" or "Warmup state")
            ?? packets.FirstOrDefault();
        return packet is null
            ? "Tyre data is not reliable yet."
            : ExtractFirstActionableSentence(packet.Explanation);
    }

    private static string? BuildPushConfidenceSummary(string action, bool english)
    {
        if (string.IsNullOrWhiteSpace(action))
        {
            return null;
        }

        return ExtractFirstActionableSentence(action);
    }

    private static string? BuildThrottleSummary(string action, IReadOnlyList<CoachEvidencePacket> packets, bool english)
    {
        if (action.Contains("No throttle data yet", StringComparison.OrdinalIgnoreCase))
        {
            return "No throttle data yet. Drive a clean lap first.";
        }

        if (!string.IsNullOrWhiteSpace(action))
        {
            return ExtractFirstActionableSentence(action);
        }

        var packet = packets.FirstOrDefault(packet => packet.Category == "Throttle");
        return packet is null ? null : ExtractFirstActionableSentence(packet.Explanation);
    }

    private static string? BuildTyreSpokenFromAction(string action)
    {
        var sentences = Regex.Split(CleanForSpeech(action), @"(?<=[.!?;])\s+")
            .Select(CleanForSpeech)
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .Where(s => !s.Contains("push", StringComparison.OrdinalIgnoreCase)
                && !s.Contains("Safe to push", StringComparison.OrdinalIgnoreCase))
            .ToArray();
        if (sentences.Length == 0)
        {
            return ExtractFirstActionableSentence(action);
        }

        return string.Join(" ", sentences.Take(2));
    }

    private static string? BuildFuelAmountSummary(string action, IReadOnlyList<CoachEvidencePacket> packets, bool english)
    {
        var fuelMatch = Regex.Match(action, @"You have\s+([0-9.]+)\s+liters", RegexOptions.IgnoreCase);
        if (!fuelMatch.Success)
        {
            fuelMatch = Regex.Match(action, @"You have\s+([0-9.]+)\s+L fuel", RegexOptions.IgnoreCase);
        }

        if (fuelMatch.Success)
        {
            var consumptionMatch = Regex.Match(action, @"Fuel use is about\s+([0-9.]+)\s+L/lap", RegexOptions.IgnoreCase);
            return consumptionMatch.Success
                ? $"You have {fuelMatch.Groups[1].Value} liters. Fuel use is about {consumptionMatch.Groups[1].Value} L/lap."
                : $"You have {fuelMatch.Groups[1].Value} liters.";
        }

        var packet = packets.FirstOrDefault(packet => packet.Summary.Contains("fuel", StringComparison.OrdinalIgnoreCase) && !packet.Summary.Contains("per lap", StringComparison.OrdinalIgnoreCase))
            ?? packets.FirstOrDefault(IsFuelLevelPacket);
        if (packet is not null)
        {
            var value = ExtractMetricValue(packet.Explanation) ?? ExtractMetricValue(packet.Summary);
            return value is null
                ? ExtractFirstActionableSentence(packet.Explanation)
                : $"You have {value} liters.";
        }

        return ExtractFirstActionableSentence(action);
    }

    private static string? BuildFuelConsumptionSummary(string action, IReadOnlyList<CoachEvidencePacket> packets, bool english)
    {
        var consumptionMatch = Regex.Match(action, @"Fuel use is about\s+([0-9.]+)\s+L/lap", RegexOptions.IgnoreCase);
        if (consumptionMatch.Success)
        {
            return $"Fuel use is about {consumptionMatch.Groups[1].Value} L/lap.";
        }

        var packet = packets.FirstOrDefault(packet => packet.Summary.Contains("per lap", StringComparison.OrdinalIgnoreCase))
            ?? packets.FirstOrDefault(IsFuelConsumptionPacket);
        if (packet is not null)
        {
            if (packet.MetricValue is { } metricValue)
            {
                return $"Fuel use is about {metricValue.ToString("0.0", CultureInfo.InvariantCulture)} L/lap.";
            }

            var parsed = ExtractMetricValue(packet.Explanation);
            return parsed is null
                ? ExtractFirstActionableSentence(packet.Explanation)
                : $"Fuel use is about {parsed} L/lap.";
        }

        return ExtractFirstActionableSentence(action);
    }

    private static string? BuildFuelStrategySummary(string action, IReadOnlyList<CoachEvidencePacket> packets, bool english)
    {
        var lapsMatch = Regex.Match(action, @"About\s+([0-9.]+)\s+laps remaining", RegexOptions.IgnoreCase);
        var riskMatch = Regex.Match(action, @"Fuel risk level is\s+(\w+)", RegexOptions.IgnoreCase);
        if (lapsMatch.Success && riskMatch.Success)
        {
            return $"About {lapsMatch.Groups[1].Value} laps remaining. Fuel risk is {riskMatch.Groups[1].Value}.";
        }

        if (lapsMatch.Success)
        {
            return $"About {lapsMatch.Groups[1].Value} laps remaining on current fuel.";
        }

        var riskPacket = packets.FirstOrDefault(packet => packet.Summary == "Fuel risk")
            ?? packets.FirstOrDefault(packet => packet.Summary == "Laps remaining");
        if (riskPacket is not null)
        {
            return ExtractFirstActionableSentence(riskPacket.Explanation);
        }

        return ExtractFirstActionableSentence(action);
    }

    private static string? BuildLapTimeSummary(string action, IReadOnlyList<CoachEvidencePacket> packets, bool english)
    {
        if (action.Contains("No valid lap time yet", StringComparison.OrdinalIgnoreCase))
        {
            return "No valid lap time yet.";
        }

        var timeMatch = Regex.Match(action, @"(Last lap was|Best lap is)\s+([0-9:.]+)", RegexOptions.IgnoreCase);
        if (timeMatch.Success)
        {
            return $"{timeMatch.Groups[1].Value} {timeMatch.Groups[2].Value}.";
        }

        var packet = packets.FirstOrDefault(packet =>
            packet.Summary.Contains("lap", StringComparison.OrdinalIgnoreCase)
                || packet.Explanation.Contains("time:", StringComparison.OrdinalIgnoreCase));
        if (packet is not null)
        {
            return ExtractFirstActionableSentence($"{packet.Summary}. {packet.Explanation}");
        }

        return ExtractFirstActionableSentence(action);
    }

    private static string? BuildStrategySummary(string action, IReadOnlyList<CoachEvidencePacket> packets, bool english)
    {
        var recommendation = packets.FirstOrDefault(packet => packet.Summary == "Pit recommendation")
            ?? packets.FirstOrDefault(packet => packet.Summary == "Strategy summary")
            ?? packets.FirstOrDefault();
        if (recommendation is not null)
        {
            return ExtractFirstActionableSentence(recommendation.Explanation);
        }

        return ExtractFirstActionableSentence(action);
    }

    private static string? BuildBrakingSummary(string action, IReadOnlyList<CoachEvidencePacket> packets, bool english)
    {
        if (action.Contains("No braking data yet", StringComparison.OrdinalIgnoreCase)
            || action.Contains("Not enough braking data yet", StringComparison.OrdinalIgnoreCase))
        {
            return "No braking data yet. Drive a clean lap first.";
        }

        if (DrivingTechniqueGate.ContainsBlockedTechniqueLeak(action))
        {
            return "No braking data yet. Drive a clean lap first.";
        }

        if (action.Contains("brake", StringComparison.OrdinalIgnoreCase))
        {
            return ExtractFirstActionableSentence(action);
        }

        var packet = packets.FirstOrDefault(packet => packet.Summary != "InvalidLapOrFlags");
        return packet is null ? ExtractFirstActionableSentence(action) : ExtractFirstActionableSentence(packet.Explanation);
    }

    private static string? BuildGenericSummary(
        string action,
        IReadOnlyList<CoachEvidencePacket> packets,
        bool english,
        string? categoryHint)
    {
        if (categoryHint == "Throttle"
            && (action.Contains("No throttle data yet", StringComparison.OrdinalIgnoreCase)
                || action.Contains("Not enough throttle data yet", StringComparison.OrdinalIgnoreCase)))
        {
            return "No throttle data yet. Drive a clean lap first.";
        }

        if (categoryHint == "Pace"
            && (action.Contains("No pace data yet", StringComparison.OrdinalIgnoreCase)
                || action.Contains("No valid pace data yet", StringComparison.OrdinalIgnoreCase)))
        {
            return "No pace data yet. Drive a clean lap first.";
        }

        if (categoryHint == "Sector"
            && (action.Contains("No sector data yet", StringComparison.OrdinalIgnoreCase)
                || action.Contains("No valid sector data yet", StringComparison.OrdinalIgnoreCase)))
        {
            return "No sector data yet. Drive a clean lap first.";
        }

        if (categoryHint == "Improvement"
            && (action.Contains("No improvement data yet", StringComparison.OrdinalIgnoreCase)
                || action.Contains("No valid improvement data yet", StringComparison.OrdinalIgnoreCase)))
        {
            return "No improvement data yet. Drive a clean lap first.";
        }

        if (categoryHint == "Lap"
            && (action.Contains("No lap comparison data yet", StringComparison.OrdinalIgnoreCase)
                || action.Contains("No valid lap comparison data yet", StringComparison.OrdinalIgnoreCase)))
        {
            return "No lap comparison data yet. Drive a clean lap first.";
        }

        if (!string.IsNullOrWhiteSpace(action))
        {
            var sentence = ExtractFirstActionableSentence(action);
            if (!string.IsNullOrWhiteSpace(sentence))
            {
                return sentence;
            }
        }

        var packet = categoryHint is null
            ? packets.FirstOrDefault()
            : packets.FirstOrDefault(item =>
                item.Category.Contains(categoryHint, StringComparison.OrdinalIgnoreCase)
                    || item.Summary.Contains(categoryHint, StringComparison.OrdinalIgnoreCase));
        return packet is null ? null : ExtractFirstActionableSentence($"{packet.Summary}. {packet.Explanation}");
    }

    private static string SelectBestCandidate(
        CoachQueryTopic topic,
        string? topicSummary,
        string? fromContent,
        string original,
        CoachMessage message)
    {
        if (CoachQueryTopicClassifier.AllowsFuelEvidence(topic))
        {
            foreach (var candidate in new[] { topicSummary, fromContent, original })
            {
                if (IsUsableCandidate(candidate, topic))
                {
                    return candidate!;
                }
            }
        }
        else
        {
            if (IsUsableCandidate(topicSummary, topic))
            {
                return topicSummary!;
            }

            if (IsUsableCandidate(fromContent, topic) && !LooksLikeFuelAnswer(fromContent!))
            {
                return fromContent!;
            }

            if (IsUsableCandidate(original, topic) && !LooksLikeFuelAnswer(original))
            {
                return original;
            }

            return topicSummary ?? fromContent ?? original;
        }

        return original;
    }

    private static bool IsUsableCandidate(string? candidate, CoachQueryTopic topic)
    {
        if (string.IsNullOrWhiteSpace(candidate))
        {
            return false;
        }

        if (!CoachQueryTopicClassifier.AllowsFuelEvidence(topic) && LooksLikeFuelAnswer(candidate))
        {
            return false;
        }

        return WordCount(candidate) >= 2;
    }

    private static bool LooksLikeFuelAnswer(string text)
    {
        return text.Contains("fuel", StringComparison.OrdinalIgnoreCase)
            || text.Contains("goriv", StringComparison.OrdinalIgnoreCase)
            || text.Contains("liters fuel", StringComparison.OrdinalIgnoreCase)
            || text.Contains(" L fuel", StringComparison.Ordinal)
            || text.Contains("laps remaining", StringComparison.OrdinalIgnoreCase);
    }

    private static IReadOnlyList<CoachEvidencePacket> FilterPacketsByTopic(
        IReadOnlyList<CoachEvidencePacket> packets,
        CoachQueryTopic topic)
    {
        if (packets.Count == 0)
        {
            return packets;
        }

        return topic switch
        {
            CoachQueryTopic.FuelAmount => packets.Where(IsFuelLevelPacket).Concat(packets.Where(IsFuelPacket)).DistinctBy(p => p.Summary).ToArray(),
            CoachQueryTopic.FuelConsumption => packets.Where(IsFuelConsumptionPacket).ToArray(),
            CoachQueryTopic.FuelStrategy => packets.Where(packet => IsFuelStrategyPacket(packet) || IsFuelConsumptionPacket(packet)).DistinctBy(p => p.Summary).ToArray(),
            CoachQueryTopic.Strategy or CoachQueryTopic.Pit => packets.Where(IsStrategyPacket).Concat(packets.Where(IsFuelPacket)).DistinctBy(p => p.Summary).ToArray(),
            CoachQueryTopic.Braking => packets.Where(IsBrakingPacket).ToArray(),
            CoachQueryTopic.Throttle => packets.Where(IsThrottlePacket).ToArray(),
            CoachQueryTopic.RacePace => packets.Where(IsPacePacket).ToArray(),
            CoachQueryTopic.LosingTime or CoachQueryTopic.LapComparison => packets.Where(IsLapComparisonPacket).ToArray(),
            CoachQueryTopic.Improvement => packets.Where(IsImprovementPacket).ToArray(),
            CoachQueryTopic.Incidents => packets.Where(IsIncidentPacket).ToArray(),
            CoachQueryTopic.Tyre => packets.Where(packet =>
                IsTyrePacket(packet) || packet.Category.Contains("TyreIntelligence", StringComparison.Ordinal)).ToArray(),
            CoachQueryTopic.LapTime => packets.Where(IsLapTimePacket).ToArray(),
            CoachQueryTopic.Position => [],
            _ => packets.Where(packet => !IsFuelPacket(packet)).ToArray()
        };
    }

    private static bool IsFuelPacket(CoachEvidencePacket packet)
    {
        var key = $"{packet.Category} {packet.Summary} {packet.Explanation}".ToLowerInvariant();
        return key.Contains("fuel") || key.Contains("goriv");
    }

    private static bool IsFuelLevelPacket(CoachEvidencePacket packet)
    {
        var key = $"{packet.Category} {packet.Summary} {packet.Explanation}".ToLowerInvariant();
        return key.Contains("latest fuel") || packet.Summary.Contains("fuel level", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsFuelConsumptionPacket(CoachEvidencePacket packet)
    {
        var key = $"{packet.Category} {packet.Summary} {packet.Explanation}".ToLowerInvariant();
        return key.Contains("per lap") || key.Contains("consumption") || key.Contains("potro");
    }

    private static bool IsFuelStrategyPacket(CoachEvidencePacket packet)
    {
        var key = $"{packet.Category} {packet.Summary} {packet.Explanation}".ToLowerInvariant();
        return key.Contains("laps remaining") || key.Contains("fuel risk") || key.Contains("finish");
    }

    private static bool IsStrategyPacket(CoachEvidencePacket packet)
    {
        var key = $"{packet.Category} {packet.Summary}".ToLowerInvariant();
        return key.Contains("strategy") || key.Contains("pit");
    }

    private static bool IsBrakingPacket(CoachEvidencePacket packet)
    {
        var key = $"{packet.Category} {packet.Summary} {packet.Explanation}".ToLowerInvariant();
        return key.Contains("brak");
    }

    private static bool IsThrottlePacket(CoachEvidencePacket packet)
    {
        var key = $"{packet.Category} {packet.Summary} {packet.Explanation}".ToLowerInvariant();
        return key.Contains("throttle") || key.Contains("gas");
    }

    private static bool IsPacePacket(CoachEvidencePacket packet)
    {
        var key = $"{packet.Category} {packet.Summary}".ToLowerInvariant();
        return key.Contains("pace") || key.Contains("tempo");
    }

    private static bool IsLapComparisonPacket(CoachEvidencePacket packet)
    {
        var key = $"{packet.Category} {packet.Summary}".ToLowerInvariant();
        return key.Contains("lap") || key.Contains("sector") || key.Contains("delta");
    }

    private static bool IsImprovementPacket(CoachEvidencePacket packet)
    {
        var key = $"{packet.Category} {packet.Summary}".ToLowerInvariant();
        return key.Contains("improvement") || key.Contains("weakness");
    }

    private static bool IsIncidentPacket(CoachEvidencePacket packet)
    {
        var key = $"{packet.Category} {packet.Summary}".ToLowerInvariant();
        return key.Contains("incident") || key.Contains("event");
    }

    private static bool IsTyrePacket(CoachEvidencePacket packet)
    {
        var key = $"{packet.Category} {packet.Summary} {packet.Explanation}".ToLowerInvariant();
        return key.Contains("tyre") || key.Contains("tire") || key.Contains("gum");
    }

    private static bool IsLapTimePacket(CoachEvidencePacket packet)
    {
        var key = $"{packet.Category} {packet.Summary} {packet.Explanation}".ToLowerInvariant();
        return key.Contains("lap") || key.Contains("time:");
    }

    private static string? ExtractFirstActionableSentence(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        var segments = Regex.Split(CleanForSpeech(text), @"(?<=[.!?;])\s+");
        foreach (var segment in segments)
        {
            var trimmed = CleanForSpeech(segment);
            if (WordCount(trimmed) >= 3)
            {
                return trimmed;
            }
        }

        return WordCount(text) >= 2 ? CleanForSpeech(text) : null;
    }

    private static string ExtractActionText(string content)
    {
        var parts = content.Split("Evidence:", StringSplitOptions.None);
        return parts[0].Trim();
    }

    private static string? ExtractMetricValue(string explanation)
    {
        var match = Regex.Match(explanation, @"(?<value>\d+(?:\.\d+)?)");
        return match.Success ? match.Groups["value"].Value : null;
    }

    private static string CleanForSpeech(string text)
    {
        return text
            .ReplaceLineEndings(" ")
            .Replace("Evidence:", "", StringComparison.Ordinal)
            .Trim();
    }

    private static int WordCount(string text) =>
        text.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length;

    private static string TruncateForLog(string text, int maxLength = 120)
    {
        var compact = text.ReplaceLineEndings(" ").Trim();
        return compact.Length <= maxLength ? compact : compact[..maxLength] + "...";
    }
}

public static class VoiceText
{
    public static string LimitWords(string text, int maxWords)
    {
        var compact = text.ReplaceLineEndings(" ").Trim();
        if (compact.Length == 0 || maxWords <= 0)
        {
            return "";
        }

        var words = compact.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (words.Length <= maxWords)
        {
            return compact;
        }

        return string.Join(' ', words.Take(maxWords)).TrimEnd('.', ';') + ".";
    }

    public static string RaceSafeCallout(string text, int maxWords = 10)
    {
        var compact = text.ReplaceLineEndings(" ").Trim();
        if (compact.Length == 0)
        {
            return "";
        }

        var firstSentenceEnd = compact.IndexOfAny(['.', '!', '?']);
        var firstSentence = firstSentenceEnd >= 0
            ? compact[..(firstSentenceEnd + 1)].Trim()
            : compact;

        return LimitWords(firstSentence, maxWords);
    }
}
