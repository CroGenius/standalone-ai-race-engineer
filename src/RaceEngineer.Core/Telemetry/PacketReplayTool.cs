using System.Text.Json;
using RaceEngineer.Core.Events;
using RaceEngineer.Core.Session;

namespace RaceEngineer.Core.Telemetry;

public sealed record PacketReplayResult(
    int PacketsRead,
    int ValidPackets,
    int InvalidPackets,
    int SnapshotsProduced,
    int EventsProduced,
    SessionState Session);

public sealed class PacketReplayTool
{
    public PacketReplayResult Replay(string jsonlPath, EventEngine? eventEngine = null, SessionState? session = null)
    {
        eventEngine ??= new EventEngine();
        session ??= new SessionState();
        var packetsRead = 0;
        var validPackets = 0;
        var invalidPackets = 0;
        var eventsProduced = 0;

        foreach (var line in File.ReadLines(jsonlPath))
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            packetsRead++;
            var rawJson = ExtractRawJson(line);
            if (rawJson is null)
            {
                invalidPackets++;
                continue;
            }

            if (!SimHubPacketParser.TryParse(rawJson, out var snapshot, out _))
            {
                invalidPackets++;
                continue;
            }

            validPackets++;
            var events = eventEngine.Process(snapshot!);
            eventsProduced += events.Count;
            session.ApplySnapshot(snapshot!, events);
        }

        return new PacketReplayResult(
            packetsRead,
            validPackets,
            invalidPackets,
            validPackets,
            eventsProduced,
            session);
    }

    private static string? ExtractRawJson(string line)
    {
        try
        {
            using var document = JsonDocument.Parse(line);
            var root = document.RootElement;
            if (root.ValueKind == JsonValueKind.Object && root.TryGetProperty("raw_json", out var rawJson) && rawJson.ValueKind == JsonValueKind.String)
            {
                return rawJson.GetString();
            }
        }
        catch (JsonException)
        {
            // Fall through and treat the line itself as a raw packet.
        }

        return line.Trim();
    }
}

