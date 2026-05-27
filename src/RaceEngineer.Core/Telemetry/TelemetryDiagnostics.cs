namespace RaceEngineer.Core.Telemetry;

public sealed class TelemetryDiagnostics
{
    private readonly Queue<DateTimeOffset> packetTimes = new();
    private readonly Queue<DateTimeOffset> validTimes = new();
    private readonly Queue<DateTimeOffset> invalidTimes = new();

    public TelemetryDiagnostics(string bindAddress = "127.0.0.1", int port = 20999)
    {
        BindAddress = bindAddress;
        Port = port;
    }

    public string BindAddress { get; }
    public int Port { get; }
    public bool ReceiverRunning { get; private set; }
    public int PacketsReceived { get; private set; }
    public int ValidPackets { get; private set; }
    public int InvalidPackets { get; private set; }
    public DateTimeOffset? LastValidPacketAt { get; private set; }
    public string? LastInvalidReason { get; private set; }
    public string? CurrentSchema { get; private set; }
    public int? CurrentSchemaVersion { get; private set; }
    public double PacketsPerSecond => Rate(packetTimes);
    public double ValidPacketsPerSecond => Rate(validTimes);
    public double InvalidPacketsPerSecond => Rate(invalidTimes);
    public TimeSpan? LastValidPacketAge => LastValidPacketAt.HasValue ? DateTimeOffset.UtcNow - LastValidPacketAt.Value : null;

    public void SetReceiverRunning(bool running)
    {
        ReceiverRunning = running;
    }

    public void Record(TelemetryPacketResult packet)
    {
        PacketsReceived++;
        packetTimes.Enqueue(packet.Timestamp);
        CurrentSchema = packet.Schema ?? CurrentSchema;
        CurrentSchemaVersion = packet.SchemaVersion ?? CurrentSchemaVersion;

        if (packet.IsValid)
        {
            ValidPackets++;
            LastValidPacketAt = packet.Timestamp;
            validTimes.Enqueue(packet.Timestamp);
        }
        else
        {
            InvalidPackets++;
            LastInvalidReason = packet.Warning;
            invalidTimes.Enqueue(packet.Timestamp);
        }

        Trim(packetTimes, packet.Timestamp);
        Trim(validTimes, packet.Timestamp);
        Trim(invalidTimes, packet.Timestamp);
    }

    private static void Trim(Queue<DateTimeOffset> queue, DateTimeOffset now)
    {
        while (queue.Count > 0 && now - queue.Peek() > TimeSpan.FromSeconds(1))
        {
            queue.Dequeue();
        }
    }

    private static double Rate(Queue<DateTimeOffset> queue)
    {
        return queue.Count;
    }
}

