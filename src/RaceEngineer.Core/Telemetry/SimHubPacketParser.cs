using System.Text.Json;
using RaceEngineer.Core.RaceAwareness;

namespace RaceEngineer.Core.Telemetry;

public static class SimHubPacketParser
{
    public const string ExpectedSchema = "acevo_engineer.simhub_datacore";
    public const int ExpectedSchemaVersion = 1;

    public static bool TryParse(ReadOnlyMemory<byte> utf8Json, out TelemetrySnapshot? snapshot, out string? warning)
    {
        try
        {
            using var document = JsonDocument.Parse(utf8Json);
            return TryParse(document.RootElement, out snapshot, out warning);
        }
        catch (JsonException exception)
        {
            snapshot = null;
            warning = $"Invalid JSON: {exception.Message}";
            return false;
        }
    }

    public static TelemetryPacketEnvelope ReadEnvelope(string json)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            return root.ValueKind == JsonValueKind.Object
                ? new TelemetryPacketEnvelope(StringOrNull(root, "schema"), IntOrNull(root, "schema_version"))
                : new TelemetryPacketEnvelope(null, null);
        }
        catch (JsonException)
        {
            return new TelemetryPacketEnvelope(null, null);
        }
    }

    public static bool TryParse(string json, out TelemetrySnapshot? snapshot, out string? warning)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            return TryParse(document.RootElement, out snapshot, out warning);
        }
        catch (JsonException exception)
        {
            snapshot = null;
            warning = $"Invalid JSON: {exception.Message}";
            return false;
        }
    }

    public static TelemetrySnapshot Parse(ReadOnlyMemory<byte> utf8Json)
    {
        return TryParse(utf8Json, out var snapshot, out var warning)
            ? snapshot!
            : throw new JsonException(warning);
    }

    public static TelemetrySnapshot Parse(string json)
    {
        return TryParse(json, out var snapshot, out var warning)
            ? snapshot!
            : throw new JsonException(warning);
    }

    private static bool TryParse(JsonElement root, out TelemetrySnapshot? snapshot, out string? warning)
    {
        snapshot = null;
        if (root.ValueKind != JsonValueKind.Object)
        {
            warning = "Packet root must be a JSON object.";
            return false;
        }

        var schema = StringOrNull(root, "schema");
        if (!string.Equals(schema, ExpectedSchema, StringComparison.Ordinal))
        {
            warning = $"Unsupported schema '{schema ?? "<missing>"}'. Expected '{ExpectedSchema}'.";
            return false;
        }

        var schemaVersion = IntOrNull(root, "schema_version");
        if (schemaVersion != ExpectedSchemaVersion)
        {
            warning = $"Unsupported schema_version '{schemaVersion?.ToString() ?? "<missing>"}'. Expected '{ExpectedSchemaVersion}'.";
            return false;
        }

        snapshot = ParseValidated(root, ExpectedSchema, schemaVersion);
        warning = null;
        return true;
    }

    private static TelemetrySnapshot ParseValidated(JsonElement root, string schema, int? schemaVersion)
    {
        var raceElement = root.TryGetProperty("race", out var race) && race.ValueKind == JsonValueKind.Object
            ? race
            : default;

        return new TelemetrySnapshot(
            Guid.NewGuid(),
            DateTimeOffset.UtcNow,
            new SourceInfo("simhub_datacore", schema, schemaVersion),
            new CarState(
                DoubleOrNull(root, "speed_kmh"),
                DoubleOrNull(root, "rpm"),
                IntOrNull(root, "gear"),
                RawOrNull(root, "damage")),
            new DriverInputs(
                DoubleOrNull(root, "throttle"),
                DoubleOrNull(root, "brake"),
                DoubleOrNull(root, "steering")),
            new LapState(
                IntOrNull(root, "lap_time_ms"),
                DoubleOrNull(root, "lap_time_s"),
                DoubleOrNull(root, "lap_progress"),
                IntOrNull(root, "lap_number"),
                BoolOrNull(root, "valid_lap")),
            new RaceState(
                IntOrNull(root, "position") ?? (raceElement.ValueKind == JsonValueKind.Object ? IntOrNull(raceElement, "position") : null),
                RawOrNull(root, "flags") ?? (raceElement.ValueKind == JsonValueKind.Object ? RawOrNull(raceElement, "flags") : null)),
            new TyreBrakeFuelState(
                DoubleOrNull(root, "fuel"),
                DoubleListOrNull(root, "tyre_temp_c"),
                DoubleListOrNull(root, "tyre_pressure"),
                DoubleListOrNull(root, "brake_temp_c"),
                DoubleListOrNull(root, "tyre_wear")),
            ParseRaceAwareness(root, raceElement));
    }

    private static RaceAwarenessState ParseRaceAwareness(JsonElement root, JsonElement raceElement)
    {
        var (providerTrackId, providerCarId, _) = ParseProviderDiagnostics(root);
        var trackName = StringOrNull(root, "track_name")
            ?? StringOrNull(root, "track")
            ?? providerTrackId;
        var circuitId = StringOrNull(root, "circuit_id") ?? providerTrackId;
        var carName = StringOrNull(root, "car_name")
            ?? StringOrNull(root, "car")
            ?? providerCarId;

        return new RaceAwarenessState(
            trackName,
            circuitId,
            carName,
            StringOrNull(root, "car_class"),
            StringOrNull(root, "session_type"),
            IntOrNull(root, "lap_number") ?? IntOrNull(root, "current_lap"),
            IntOrNull(root, "total_laps"),
            IntOrNull(root, "position") ?? (raceElement.ValueKind == JsonValueKind.Object ? IntOrNull(raceElement, "position") : null),
            IntOrNull(root, "total_cars") ?? IntOrNull(root, "opponents"),
            DoubleOrNull(root, "gap_ahead_s") ?? DoubleOrNull(root, "gap_ahead"),
            DoubleOrNull(root, "gap_behind_s") ?? DoubleOrNull(root, "gap_behind"),
            StringOrNull(root, "car_ahead"),
            StringOrNull(root, "car_behind"),
            StringOrNull(root, "pit_state"),
            StringOrNull(root, "flags") ?? (raceElement.ValueKind == JsonValueKind.Object ? StringOrNull(raceElement, "flags") : null),
            IntOrNull(root, "sector"),
            DoubleOrNull(root, "session_time_remaining_s") ?? DoubleOrNull(root, "session_remaining_s"),
            IntOrNull(root, "laps_remaining"));
    }

    private static (string? TrackId, string? CarId, string? GameName) ParseProviderDiagnostics(JsonElement root)
    {
        if (!root.TryGetProperty("provider_diag", out var providerDiag)
            || providerDiag.ValueKind != JsonValueKind.Object)
        {
            return (null, null, null);
        }

        return (
            StringOrNull(providerDiag, "pm_last_track_id"),
            StringOrNull(providerDiag, "pm_last_car_id"),
            StringOrNull(providerDiag, "pm_game_name"));
    }

    private static double? DoubleOrNull(JsonElement root, string name)
    {
        if (!root.TryGetProperty(name, out var value) || value.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
        {
            return null;
        }

        if (value.ValueKind == JsonValueKind.Number && value.TryGetDouble(out var number))
        {
            return number;
        }

        return value.ValueKind == JsonValueKind.String && double.TryParse(value.GetString(), out number) ? number : null;
    }

    private static int? IntOrNull(JsonElement root, string name)
    {
        if (!root.TryGetProperty(name, out var value) || value.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
        {
            return null;
        }

        if (value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var number))
        {
            return number;
        }

        return value.ValueKind == JsonValueKind.String && int.TryParse(value.GetString(), out number) ? number : null;
    }

    private static bool? BoolOrNull(JsonElement root, string name)
    {
        if (!root.TryGetProperty(name, out var value) || value.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
        {
            return null;
        }

        return value.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.String when bool.TryParse(value.GetString(), out var parsed) => parsed,
            _ => null
        };
    }

    private static string? StringOrNull(JsonElement root, string name)
    {
        return root.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
    }

    private static IReadOnlyList<double?>? DoubleListOrNull(JsonElement root, string name)
    {
        if (!root.TryGetProperty(name, out var value) || value.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        var results = new List<double?>();
        foreach (var item in value.EnumerateArray())
        {
            results.Add(item.ValueKind switch
            {
                JsonValueKind.Number when item.TryGetDouble(out var number) => number,
                JsonValueKind.String when double.TryParse(item.GetString(), out var number) => number,
                _ => null
            });
        }

        return results;
    }

    private static object? RawOrNull(JsonElement root, string name)
    {
        if (!root.TryGetProperty(name, out var value) || value.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
        {
            return null;
        }

        return value.GetRawText();
    }
}
