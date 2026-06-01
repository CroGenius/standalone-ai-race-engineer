using System.Globalization;
using System.Text;
using System.Text.Json;
using RaceEngineer.Core.RaceAwareness;

namespace RaceEngineer.Core.Telemetry;

public static class OpponentTelemetryFieldDiscovery
{
    public static readonly string[] TrackedOpponentFields =
    [
        "gap_ahead_s",
        "gap_behind_s",
        "car_ahead",
        "car_behind",
        "total_cars",
        "position"
    ];

    private static readonly string[] SearchKeywords =
    [
        "gap",
        "opponent",
        "carahead",
        "carbehind",
        "driverahead",
        "driverbehind",
        "leaderboard",
        "standings",
        "totalcars",
        "opponents"
    ];

    private static readonly string[] GapAheadAliases =
    [
        "gap_ahead_s",
        "gap_ahead",
        "gap_ahead_sec",
        "gap_ahead_seconds",
        "gapAhead",
        "gapAheadS",
        "gapAheadSec",
        "gapAheadSeconds"
    ];

    private static readonly string[] GapBehindAliases =
    [
        "gap_behind_s",
        "gap_behind",
        "gap_behind_sec",
        "gap_behind_seconds",
        "gapBehind",
        "gapBehindS",
        "gapBehindSec",
        "gapBehindSeconds"
    ];

    private static readonly string[] CarAheadAliases =
    [
        "car_ahead",
        "carAhead",
        "driver_ahead",
        "driverAhead",
        "opponent_ahead",
        "opponentAhead"
    ];

    private static readonly string[] CarBehindAliases =
    [
        "car_behind",
        "carBehind",
        "driver_behind",
        "driverBehind",
        "opponent_behind",
        "opponentBehind"
    ];

    private static readonly string[] TotalCarsAliases =
    [
        "total_cars",
        "totalCars",
        "totalcars",
        "field_size",
        "fieldSize",
        "grid_size",
        "gridSize",
        "opponent_count",
        "opponentCount",
        "opponents"
    ];

    private static readonly string[] PositionAliases =
    [
        "position",
        "race_position",
        "racePosition"
    ];

    public sealed record ResolvedOpponentFields(
        int? Position,
        int? TotalCars,
        double? GapAheadSeconds,
        double? GapBehindSeconds,
        string? CarAhead,
        string? CarBehind,
        IReadOnlyDictionary<string, string> ResolvedPropertyNames);

    public static ResolvedOpponentFields Resolve(JsonElement root, JsonElement raceElement)
    {
        var resolved = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        var position = ResolveInt(root, raceElement, PositionAliases, "position", resolved);
        var totalCars = ResolveInt(root, raceElement, TotalCarsAliases, "total_cars", resolved);
        var gapAhead = ResolveDouble(root, raceElement, GapAheadAliases, "gap_ahead_s", resolved);
        var gapBehind = ResolveDouble(root, raceElement, GapBehindAliases, "gap_behind_s", resolved);
        var carAhead = ResolveString(root, raceElement, CarAheadAliases, "car_ahead", resolved);
        var carBehind = ResolveString(root, raceElement, CarBehindAliases, "car_behind", resolved);

        return new ResolvedOpponentFields(
            position,
            totalCars,
            gapAhead,
            gapBehind,
            carAhead,
            carBehind,
            resolved);
    }

    public static OpponentTelemetrySourceReport Analyze(string? rawJson, RaceAwarenessState? awareness)
    {
        IReadOnlyList<OpponentFieldCandidate> candidates = [];
        var resolvedNames = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (!string.IsNullOrWhiteSpace(rawJson))
        {
            try
            {
                using var document = JsonDocument.Parse(rawJson);
                var root = document.RootElement;
                if (root.ValueKind == JsonValueKind.Object)
                {
                    var raceElement = default(JsonElement);
                    if (root.TryGetProperty("race", out var race) && race.ValueKind == JsonValueKind.Object)
                    {
                        raceElement = race;
                    }

                    candidates = ScanCandidates(root);
                    resolvedNames = new Dictionary<string, string>(
                        Resolve(root, raceElement).ResolvedPropertyNames,
                        StringComparer.OrdinalIgnoreCase);
                }
            }
            catch (JsonException)
            {
                return new OpponentTelemetrySourceReport(
                    [],
                    TrackedOpponentFields,
                    new Dictionary<string, string>(),
                    [],
                    "Latest packet JSON could not be scanned for opponent/gap fields.");
            }
        }

        var present = new List<string>();
        var missing = new List<string>();
        TrackMappedField("gap_ahead_s", awareness?.GapAheadSeconds?.ToString("0.000", CultureInfo.InvariantCulture), resolvedNames, present, missing);
        TrackMappedField("gap_behind_s", awareness?.GapBehindSeconds?.ToString("0.000", CultureInfo.InvariantCulture), resolvedNames, present, missing);
        TrackMappedField("car_ahead", awareness?.CarAhead, resolvedNames, present, missing);
        TrackMappedField("car_behind", awareness?.CarBehind, resolvedNames, present, missing);
        TrackMappedField("total_cars", awareness?.TotalCars?.ToString(CultureInfo.InvariantCulture), resolvedNames, present, missing);
        TrackMappedField("position", awareness?.Position?.ToString(CultureInfo.InvariantCulture), resolvedNames, present, missing);

        var unmappedCandidates = candidates
            .Where(candidate => !IsResolvedCandidate(candidate, resolvedNames))
            .ToArray();

        var summary = BuildSummary(present, missing, resolvedNames, unmappedCandidates);
        return new OpponentTelemetrySourceReport(present, missing, resolvedNames, unmappedCandidates, summary);
    }

    public static OpponentTelemetrySourceReport AnalyzeSnapshot(TelemetrySnapshot? snapshot, string? rawJson = null)
    {
        return Analyze(rawJson, snapshot?.RaceAwareness);
    }

    private static void TrackMappedField(
        string fieldName,
        string? mappedValue,
        IReadOnlyDictionary<string, string> resolvedNames,
        ICollection<string> present,
        ICollection<string> missing)
    {
        if (!string.IsNullOrWhiteSpace(mappedValue))
        {
            present.Add(fieldName);
            return;
        }

        missing.Add(fieldName);
    }

    private static string BuildSummary(
        IReadOnlyList<string> present,
        IReadOnlyList<string> missing,
        IReadOnlyDictionary<string, string> resolvedNames,
        IReadOnlyList<OpponentFieldCandidate> candidates)
    {
        var builder = new StringBuilder();
        builder.Append(present.Count == 0
            ? "Opponent/gap fields: none mapped from telemetry."
            : $"Opponent/gap present: {string.Join(", ", present)}.");

        if (missing.Count > 0)
        {
            builder.Append($" Missing: {string.Join(", ", missing)}.");
        }

        if (resolvedNames.Count > 0)
        {
            builder.Append(" Resolved:");
            builder.Append(string.Join("; ", resolvedNames.Select(pair => $"{pair.Key}<-{pair.Value}")));
            builder.Append('.');
        }

        if (candidates.Count > 0)
        {
            builder.Append(" Candidates:");
            builder.Append(string.Join("; ", candidates.Take(6).Select(candidate => $"{candidate.JsonPath}={candidate.SampleValue}")));
            if (candidates.Count > 6)
            {
                builder.Append($"; +{candidates.Count - 6} more");
            }

            builder.Append('.');
        }

        return builder.ToString().Trim();
    }

    private static bool IsResolvedCandidate(OpponentFieldCandidate candidate, IReadOnlyDictionary<string, string> resolvedNames)
    {
        foreach (var sourcePath in resolvedNames.Values)
        {
            if (string.Equals(candidate.JsonPath, sourcePath, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static int? ResolveInt(
        JsonElement root,
        JsonElement raceElement,
        IReadOnlyList<string> aliases,
        string mappedFieldName,
        IDictionary<string, string> resolved)
    {
        if (TryResolveInt(root, "", aliases, out var value, out var path)
            || (raceElement.ValueKind == JsonValueKind.Object && TryResolveInt(raceElement, "race", aliases, out value, out path)))
        {
            resolved[mappedFieldName] = path;
            return value;
        }

        return null;
    }

    private static double? ResolveDouble(
        JsonElement root,
        JsonElement raceElement,
        IReadOnlyList<string> aliases,
        string mappedFieldName,
        IDictionary<string, string> resolved)
    {
        if (TryResolveDouble(root, "", aliases, out var value, out var path)
            || (raceElement.ValueKind == JsonValueKind.Object && TryResolveDouble(raceElement, "race", aliases, out value, out path)))
        {
            resolved[mappedFieldName] = path;
            return value;
        }

        return null;
    }

    private static string? ResolveString(
        JsonElement root,
        JsonElement raceElement,
        IReadOnlyList<string> aliases,
        string mappedFieldName,
        IDictionary<string, string> resolved)
    {
        if (TryResolveString(root, "", aliases, out var value, out var path)
            || (raceElement.ValueKind == JsonValueKind.Object && TryResolveString(raceElement, "race", aliases, out value, out path)))
        {
            resolved[mappedFieldName] = path;
            return value;
        }

        return null;
    }

    private static bool TryResolveInt(
        JsonElement element,
        string prefix,
        IReadOnlyList<string> aliases,
        out int? value,
        out string path)
    {
        value = null;
        path = "";

        foreach (var alias in aliases)
        {
            if (alias.Equals("opponents", StringComparison.OrdinalIgnoreCase)
                && element.TryGetProperty(alias, out var opponents)
                && opponents.ValueKind == JsonValueKind.Array)
            {
                continue;
            }

            if (!element.TryGetProperty(alias, out var property))
            {
                continue;
            }

            var parsed = ReadInt(property);
            if (parsed is null)
            {
                continue;
            }

            value = parsed;
            path = string.IsNullOrEmpty(prefix) ? alias : $"{prefix}.{alias}";
            return true;
        }

        foreach (var property in element.EnumerateObject())
        {
            if (!aliases.Any(alias => PropertyMatchesAlias(property.Name, alias)))
            {
                continue;
            }

            if (property.Name.Equals("opponents", StringComparison.OrdinalIgnoreCase)
                && property.Value.ValueKind == JsonValueKind.Array)
            {
                continue;
            }

            var parsed = ReadInt(property.Value);
            if (parsed is null)
            {
                continue;
            }

            value = parsed;
            path = string.IsNullOrEmpty(prefix) ? property.Name : $"{prefix}.{property.Name}";
            return true;
        }

        return false;
    }

    private static bool TryResolveDouble(
        JsonElement element,
        string prefix,
        IReadOnlyList<string> aliases,
        out double? value,
        out string path)
    {
        value = null;
        path = "";

        foreach (var alias in aliases)
        {
            if (!element.TryGetProperty(alias, out var property))
            {
                continue;
            }

            var parsed = ReadDouble(property);
            if (parsed is null)
            {
                continue;
            }

            value = parsed;
            path = string.IsNullOrEmpty(prefix) ? alias : $"{prefix}.{alias}";
            return true;
        }

        foreach (var property in element.EnumerateObject())
        {
            if (!aliases.Any(alias => PropertyMatchesAlias(property.Name, alias)))
            {
                continue;
            }

            var parsed = ReadDouble(property.Value);
            if (parsed is null)
            {
                continue;
            }

            value = parsed;
            path = string.IsNullOrEmpty(prefix) ? property.Name : $"{prefix}.{property.Name}";
            return true;
        }

        return false;
    }

    private static bool TryResolveString(
        JsonElement element,
        string prefix,
        IReadOnlyList<string> aliases,
        out string? value,
        out string path)
    {
        value = null;
        path = "";

        foreach (var alias in aliases)
        {
            if (!element.TryGetProperty(alias, out var property))
            {
                continue;
            }

            var parsed = ReadString(property);
            if (parsed is null)
            {
                continue;
            }

            value = parsed;
            path = string.IsNullOrEmpty(prefix) ? alias : $"{prefix}.{alias}";
            return true;
        }

        foreach (var property in element.EnumerateObject())
        {
            if (!aliases.Any(alias => PropertyMatchesAlias(property.Name, alias)))
            {
                continue;
            }

            var parsed = ReadString(property.Value);
            if (parsed is null)
            {
                continue;
            }

            value = parsed;
            path = string.IsNullOrEmpty(prefix) ? property.Name : $"{prefix}.{property.Name}";
            return true;
        }

        return false;
    }

    private static IReadOnlyList<OpponentFieldCandidate> ScanCandidates(JsonElement root)
    {
        var candidates = new List<OpponentFieldCandidate>();
        ScanElement(root, "", candidates);
        return candidates
            .GroupBy(candidate => candidate.JsonPath, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .OrderBy(candidate => candidate.JsonPath, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static void ScanElement(JsonElement element, string path, ICollection<OpponentFieldCandidate> candidates)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            return;
        }

        foreach (var property in element.EnumerateObject())
        {
            var propertyPath = string.IsNullOrEmpty(path) ? property.Name : $"{path}.{property.Name}";
            var keyword = MatchKeyword(property.Name);
            if (keyword is not null && !ShouldSkipCandidate(property.Name, property.Value))
            {
                candidates.Add(new OpponentFieldCandidate(propertyPath, FormatSampleValue(property.Value), keyword));
            }

            if (property.Value.ValueKind == JsonValueKind.Object)
            {
                ScanElement(property.Value, propertyPath, candidates);
            }
            else if (property.Value.ValueKind == JsonValueKind.Array && property.Value.GetArrayLength() > 0)
            {
                var first = property.Value[0];
                if (first.ValueKind == JsonValueKind.Object)
                {
                    ScanElement(first, $"{propertyPath}[0]", candidates);
                }
                else if (keyword is not null)
                {
                    candidates.Add(new OpponentFieldCandidate($"{propertyPath}[0]", FormatSampleValue(first), keyword));
                }
            }
        }
    }

    private static bool ShouldSkipCandidate(string propertyName, JsonElement value)
    {
        if (propertyName.Equals("provider_diag", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (propertyName.Equals("opponents", StringComparison.OrdinalIgnoreCase)
            && value.ValueKind == JsonValueKind.Array)
        {
            return false;
        }

        return value.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined;
    }

    private static string? MatchKeyword(string propertyName)
    {
        var normalized = NormalizePropertyName(propertyName);
        return SearchKeywords.FirstOrDefault(keyword => normalized.Contains(keyword, StringComparison.Ordinal));
    }

    private static bool PropertyMatchesAlias(string propertyName, string alias)
    {
        return string.Equals(NormalizePropertyName(propertyName), NormalizePropertyName(alias), StringComparison.Ordinal);
    }

    private static string NormalizePropertyName(string propertyName)
    {
        return propertyName
            .Replace("_", "", StringComparison.Ordinal)
            .Replace("-", "", StringComparison.Ordinal)
            .Replace(" ", "", StringComparison.Ordinal)
            .ToLowerInvariant();
    }

    private static int? ReadInt(JsonElement value)
    {
        if (value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var number))
        {
            return number;
        }

        return value.ValueKind == JsonValueKind.String && int.TryParse(value.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out number)
            ? number
            : null;
    }

    private static double? ReadDouble(JsonElement value)
    {
        if (value.ValueKind == JsonValueKind.Number && value.TryGetDouble(out var number))
        {
            return number;
        }

        return value.ValueKind == JsonValueKind.String && double.TryParse(value.GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out number)
            ? number
            : null;
    }

    private static string? ReadString(JsonElement value)
    {
        return value.ValueKind switch
        {
            JsonValueKind.String => NullIfEmpty(value.GetString()),
            JsonValueKind.Number when value.TryGetDouble(out var number) => number.ToString("0.###", CultureInfo.InvariantCulture),
            _ => null
        };
    }

    private static string? NullIfEmpty(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static string FormatSampleValue(JsonElement value)
    {
        return value.ValueKind switch
        {
            JsonValueKind.String => value.GetString() ?? "null",
            JsonValueKind.Number when value.TryGetDouble(out var number) => number.ToString("0.###", CultureInfo.InvariantCulture),
            JsonValueKind.True => "true",
            JsonValueKind.False => "false",
            JsonValueKind.Array => $"array[{value.GetArrayLength()}]",
            JsonValueKind.Object => "{object}",
            _ => value.GetRawText()
        };
    }
}
