using RaceEngineer.Core.Coaching;

namespace RaceEngineer.Core.Profile;

public static class CoachResponseFormatter
{
    public static CoachMessage ApplyPreferences(CoachMessage message, CoachPreferencesRecord? preferences)
    {
        if (preferences is null)
        {
            return message;
        }

        var content = FormatContent(message.Content, preferences);
        var packets = preferences.EvidenceBulletsEnabled
            ? FilterEvidencePackets(message.EvidencePackets, preferences)
            : [];
        return message with { Content = content, EvidencePackets = packets };
    }

    public static string FormatContent(string content, CoachPreferencesRecord preferences)
    {
        var action = ExtractAction(content);
        var formattedAction = preferences.ResponseLength switch
        {
            "short" => TrimToLength(TakeFirstSentence(action), 120),
            "detailed" => action,
            _ => TrimToLength(action, 220)
        };

        if (!preferences.EvidenceBulletsEnabled)
        {
            return formattedAction;
        }

        var evidenceSection = ExtractEvidenceSection(content);
        if (string.IsNullOrWhiteSpace(evidenceSection))
        {
            return formattedAction;
        }

        var lines = evidenceSection
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(line => line.StartsWith("- ", StringComparison.Ordinal))
            .ToArray();
        var maxLines = preferences.ResponseLength switch
        {
            "short" => 2,
            "detailed" => 8,
            _ => 4
        };
        if (lines.Length == 0)
        {
            return formattedAction;
        }

        return $"{formattedAction}{Environment.NewLine}{Environment.NewLine}Evidence:{Environment.NewLine}{string.Join(Environment.NewLine, lines.Take(maxLines))}";
    }

    public static IReadOnlyList<CoachEvidencePacket> FilterEvidencePackets(
        IReadOnlyList<CoachEvidencePacket> packets,
        CoachPreferencesRecord preferences)
    {
        var max = preferences.ResponseLength switch
        {
            "short" => 2,
            "detailed" => 8,
            _ => 6
        };
        return packets.Take(max).ToArray();
    }

    public static int MaxSpeechWords(CoachPreferencesRecord? preferences)
    {
        return preferences?.ResponseLength switch
        {
            "short" => 8,
            "detailed" => 18,
            _ => 10
        };
    }

    private static string ExtractAction(string content)
    {
        var parts = content.Split("Evidence:", StringSplitOptions.None);
        return parts[0].Trim();
    }

    private static string ExtractEvidenceSection(string content)
    {
        var index = content.IndexOf("Evidence:", StringComparison.Ordinal);
        return index < 0 ? "" : content[index..];
    }

    private static string TakeFirstSentence(string text)
    {
        var end = text.IndexOfAny(['.', '!', '?']);
        return end >= 0 ? text[..(end + 1)].Trim() : text.Trim();
    }

    private static string TrimToLength(string text, int maxLength)
    {
        if (text.Length <= maxLength)
        {
            return text;
        }

        return text[..maxLength].TrimEnd() + "...";
    }
}
