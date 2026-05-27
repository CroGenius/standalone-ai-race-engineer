using System.Globalization;
using RaceEngineer.Core.Events;

namespace RaceEngineer.Desktop.Wpf;

public sealed class EventLogItem
{
    public EventLogItem(
        string type,
        string severity,
        string lap,
        string lapProgress,
        string suggestedAction,
        string confidence,
        string evidenceSummary)
    {
        Type = type;
        Severity = severity;
        Lap = lap;
        LapProgress = lapProgress;
        SuggestedAction = suggestedAction;
        Confidence = confidence;
        EvidenceSummary = evidenceSummary;
    }

    public string Type { get; }
    public string Severity { get; }
    public string Lap { get; }
    public string LapProgress { get; }
    public string SuggestedAction { get; }
    public string Confidence { get; }
    public string EvidenceSummary { get; }

    public static EventLogItem FromEvent(TelemetryEvent item)
    {
        return new EventLogItem(
            item.Type.ToString(),
            item.Severity.ToString(),
            item.LapNumber?.ToString(CultureInfo.InvariantCulture) ?? "-",
            item.LapProgress?.ToString("0.000", CultureInfo.InvariantCulture) ?? "-",
            item.SuggestedAction,
            item.Confidence.ToString("0.00", CultureInfo.InvariantCulture),
            string.Join(Environment.NewLine, item.Evidence.Select(pair => $"{pair.Key}: {pair.Value ?? "null"}")));
    }

    public static EventLogItem ParserWarning(string warning)
    {
        return new EventLogItem("ParserWarning", "Warning", "-", "-", warning, "-", warning);
    }
}

