using System.Text.Json;

namespace RaceEngineer.Core.Telemetry;

public sealed class RawPacketCapture
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly Queue<object> buffer = new();

    public RawPacketCapture(string filePath, int maxPackets = 250)
    {
        FilePath = filePath;
        MaxPackets = maxPackets;
    }

    public string FilePath { get; }
    public int MaxPackets { get; }
    public bool Enabled { get; private set; }

    public void SetEnabled(bool enabled)
    {
        Enabled = enabled;
        if (Enabled)
        {
            Flush();
        }
    }

    public void Record(TelemetryPacketResult packet)
    {
        if (!Enabled)
        {
            return;
        }

        buffer.Enqueue(new
        {
            timestamp = packet.Timestamp,
            valid = packet.IsValid,
            warning = packet.Warning,
            schema = packet.Schema,
            schema_version = packet.SchemaVersion,
            raw_json = packet.RawJson
        });

        while (buffer.Count > MaxPackets)
        {
            buffer.Dequeue();
        }

        Flush();
    }

    private void Flush()
    {
        var directory = Path.GetDirectoryName(FilePath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.WriteAllLines(FilePath, buffer.Select(item => JsonSerializer.Serialize(item, JsonOptions)));
    }
}

