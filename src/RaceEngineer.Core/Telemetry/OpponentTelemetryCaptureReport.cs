using System.Text;
using System.Text.Json;

namespace RaceEngineer.Core.Telemetry;

public static class OpponentTelemetryCaptureReport
{
    public static string BuildReport(string jsonlPath)
    {
        if (!File.Exists(jsonlPath))
        {
            return $"Capture file not found: {jsonlPath}";
        }

        var builder = new StringBuilder();
        builder.AppendLine($"Opponent/gap telemetry report: {jsonlPath}");
        builder.AppendLine();

        var lineNumber = 0;
        var validPackets = 0;
        var aggregateCandidates = new Dictionary<string, OpponentFieldCandidate>(StringComparer.OrdinalIgnoreCase);
        var aggregateResolved = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var aggregatePresent = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var aggregateMissing = new HashSet<string>(OpponentTelemetryFieldDiscovery.TrackedOpponentFields, StringComparer.OrdinalIgnoreCase);
        OpponentTelemetrySourceReport? latestReport = null;
        string? latestRawJson = null;

        foreach (var line in File.ReadLines(jsonlPath))
        {
            lineNumber++;
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            string? rawJson;
            try
            {
                using var document = JsonDocument.Parse(line);
                var root = document.RootElement;
                rawJson = root.TryGetProperty("raw_json", out var rawElement) && rawElement.ValueKind == JsonValueKind.String
                    ? rawElement.GetString()
                    : line;
            }
            catch (JsonException)
            {
                rawJson = line;
            }

            if (string.IsNullOrWhiteSpace(rawJson))
            {
                continue;
            }

            TelemetrySnapshot? snapshot = null;
            if (SimHubPacketParser.TryParse(rawJson, out var parsed, out _))
            {
                snapshot = parsed;
                validPackets++;
            }

            var report = OpponentTelemetryFieldDiscovery.AnalyzeSnapshot(snapshot, rawJson);
            latestReport = report;
            latestRawJson = rawJson;

            foreach (var field in report.PresentFields)
            {
                aggregatePresent.Add(field);
                aggregateMissing.Remove(field);
            }

            foreach (var pair in report.ResolvedPropertyNames)
            {
                aggregateResolved[pair.Key] = pair.Value;
            }

            foreach (var candidate in report.CandidateRawFields)
            {
                aggregateCandidates[candidate.JsonPath] = candidate;
            }
        }

        builder.AppendLine($"Lines scanned: {lineNumber}");
        builder.AppendLine($"Valid SimHub packets: {validPackets}");
        builder.AppendLine();

        builder.AppendLine("Aggregate opponent fields:");
        builder.AppendLine($"  Present: {(aggregatePresent.Count == 0 ? "(none)" : string.Join(", ", aggregatePresent.OrderBy(field => field)))}");
        builder.AppendLine($"  Missing: {(aggregateMissing.Count == 0 ? "(none)" : string.Join(", ", aggregateMissing.OrderBy(field => field)))}");
        builder.AppendLine();

        builder.AppendLine("Resolved property mappings:");
        if (aggregateResolved.Count == 0)
        {
            builder.AppendLine("  (none)");
        }
        else
        {
            foreach (var pair in aggregateResolved.OrderBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase))
            {
                builder.AppendLine($"  {pair.Key} <- {pair.Value}");
            }
        }

        builder.AppendLine();
        builder.AppendLine("Candidate raw fields:");
        if (aggregateCandidates.Count == 0)
        {
            builder.AppendLine("  (none)");
        }
        else
        {
            foreach (var candidate in aggregateCandidates.Values.OrderBy(item => item.JsonPath, StringComparer.OrdinalIgnoreCase))
            {
                builder.AppendLine($"  {candidate.JsonPath} = {candidate.SampleValue} [{candidate.MatchedKeyword}]");
            }
        }

        if (latestReport is not null)
        {
            builder.AppendLine();
            builder.AppendLine("Latest packet summary:");
            builder.AppendLine($"  {latestReport.Summary}");
            if (!string.IsNullOrWhiteSpace(latestRawJson))
            {
                builder.AppendLine($"  Raw sample: {Truncate(latestRawJson, 240)}");
            }
        }

        return builder.ToString().TrimEnd();
    }

    public static void PrintReport(string jsonlPath, TextWriter? output = null)
    {
        output ??= Console.Out;
        output.WriteLine(BuildReport(jsonlPath));
    }

    private static string Truncate(string value, int maxLength)
    {
        if (value.Length <= maxLength)
        {
            return value;
        }

        return value[..maxLength] + "...";
    }
}
