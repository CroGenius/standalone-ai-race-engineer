using System.Globalization;
using System.Text;
using System.Text.Json;
using RaceEngineer.Core.Telemetry;

namespace RaceEngineer.SmokeTests;

internal static class SampleThreeLapFixture
{
    public const string FileName = "sample-three-lap-run.jsonl";

    public static string ResolvePath()
    {
        return Path.Combine(AppContext.BaseDirectory, "fixtures", FileName);
    }

    public static string WriteDefault(string? directory = null)
    {
        var targetDirectory = directory ?? Path.Combine(AppContext.BaseDirectory, "fixtures");
        Directory.CreateDirectory(targetDirectory);
        var path = Path.Combine(targetDirectory, FileName);
        File.WriteAllLines(path, BuildJsonlLines(), Encoding.UTF8);
        return path;
    }

    public static IReadOnlyList<string> BuildJsonlLines()
    {
        var baseTime = new DateTimeOffset(2026, 1, 15, 12, 0, 0, TimeSpan.Zero);
        var index = 0;
        var lines = new List<string>();

        void Add(string rawBody)
        {
            var raw = ValidRaw(rawBody);
            SimHubPacketParser.TryParse(raw, out var snapshot, out _);
            var line = JsonSerializer.Serialize(new
            {
                timestamp = baseTime.AddMilliseconds(index * 100).ToString("O", CultureInfo.InvariantCulture),
                valid = true,
                warning = (string?)null,
                schema = snapshot?.Source.Schema ?? SimHubPacketParser.ExpectedSchema,
                schema_version = snapshot?.Source.SchemaVersion ?? SimHubPacketParser.ExpectedSchemaVersion,
                raw_json = raw
            });
            lines.Add(line);
            index++;
        }

        // Lap 1
        Add("""{"lap_progress":0.05,"speed_kmh":95,"throttle":0.35,"brake":0.00,"steering":0.05,"fuel":30.0,"gear":3}""");
        Add("""{"lap_progress":0.20,"speed_kmh":145,"throttle":0.85,"brake":0.00,"steering":0.10,"fuel":29.8,"gear":4}""");
        Add("""{"lap_progress":0.40,"speed_kmh":165,"throttle":0.90,"brake":0.00,"steering":0.08,"fuel":29.4,"gear":5}""");
        Add("""{"lap_progress":0.55,"speed_kmh":150,"throttle":0.35,"brake":0.55,"steering":0.25,"fuel":29.0,"gear":4}""");
        Add("""{"lap_progress":0.70,"speed_kmh":130,"throttle":0.60,"brake":0.10,"steering":0.12,"fuel":28.6,"gear":4}""");
        Add("""{"lap_progress":0.90,"speed_kmh":110,"throttle":0.50,"brake":0.05,"steering":0.06,"fuel":28.2,"gear":3}""");
        Add("""{"lap_progress":0.99,"speed_kmh":100,"throttle":0.40,"brake":0.90,"steering":0.04,"fuel":28.0,"gear":3}""");
        Add("""{"lap_progress":0.05,"lap_time_s":90.0,"speed_kmh":105,"throttle":0.45,"brake":0.00,"steering":0.03,"fuel":27.0,"gear":3}""");

        // Lap 2 with race-control flags / invalid lap marker
        Add("""{"lap_progress":0.15,"speed_kmh":140,"throttle":0.80,"brake":0.00,"steering":0.08,"fuel":26.8,"gear":4}""");
        Add("""{"lap_progress":0.35,"speed_kmh":160,"throttle":0.88,"brake":0.00,"steering":0.10,"fuel":26.4,"gear":5}""");
        Add("""{"lap_progress":0.50,"speed_kmh":120,"throttle":0.30,"brake":0.60,"steering":0.30,"fuel":26.0,"gear":4,"flags":"invalid"}""");
        Add("""{"lap_progress":0.75,"speed_kmh":125,"throttle":0.55,"brake":0.15,"steering":0.10,"fuel":25.5,"gear":4}""");
        Add("""{"lap_progress":0.99,"speed_kmh":98,"throttle":0.35,"brake":0.88,"steering":0.05,"fuel":25.0,"gear":3}""");
        Add("""{"lap_progress":0.05,"lap_time_s":92.0,"speed_kmh":102,"throttle":0.40,"brake":0.00,"steering":0.04,"fuel":24.0,"gear":3}""");

        // Lap 3 - best lap
        Add("""{"lap_progress":0.10,"speed_kmh":150,"throttle":0.82,"brake":0.00,"steering":0.07,"fuel":23.8,"gear":4}""");
        Add("""{"lap_progress":0.30,"speed_kmh":170,"throttle":0.92,"brake":0.00,"steering":0.06,"fuel":23.4,"gear":5}""");
        Add("""{"lap_progress":0.50,"speed_kmh":155,"throttle":0.40,"brake":0.50,"steering":0.22,"fuel":23.0,"gear":4}""");
        Add("""{"lap_progress":0.72,"speed_kmh":135,"throttle":0.65,"brake":0.08,"steering":0.11,"fuel":22.5,"gear":4}""");
        Add("""{"lap_progress":0.92,"speed_kmh":115,"throttle":0.55,"brake":0.04,"steering":0.05,"fuel":22.1,"gear":3}""");
        Add("""{"lap_progress":0.99,"speed_kmh":101,"throttle":0.38,"brake":0.91,"steering":0.03,"fuel":22.0,"gear":3}""");
        Add("""{"lap_progress":0.05,"lap_time_s":88.0,"speed_kmh":108,"throttle":0.48,"brake":0.00,"steering":0.02,"fuel":21.0,"gear":3}""");

        return lines;
    }

    private static string ValidRaw(string jsonBody)
    {
        var body = jsonBody.Trim();
        if (body.StartsWith('{') && body.EndsWith('}'))
        {
            body = body[1..^1];
        }

        return $$"""{"schema":"acevo_engineer.simhub_datacore","schema_version":1,{{body}}}""";
    }
}
