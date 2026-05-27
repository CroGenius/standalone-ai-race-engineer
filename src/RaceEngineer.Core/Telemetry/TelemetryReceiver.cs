using System.Net;
using System.Net.Sockets;
using System.Text;

namespace RaceEngineer.Core.Telemetry;

public sealed class TelemetryReceiver : IAsyncDisposable
{
    private readonly IPEndPoint endpoint;
    private UdpClient? udpClient;
    private CancellationTokenSource? cancellation;
    public string BindAddress => endpoint.Address.ToString();
    public int Port => endpoint.Port;
    public bool IsRunning => udpClient is not null;

    public TelemetryReceiver(string host = "127.0.0.1", int port = 20999)
    {
        endpoint = new IPEndPoint(IPAddress.Parse(host), port);
    }

    public event EventHandler<TelemetrySnapshot>? SnapshotReceived;
    public event EventHandler<TelemetryPacketResult>? PacketProcessed;

    public Task StartAsync(CancellationToken cancellationToken = default)
    {
        if (udpClient is not null)
        {
            return Task.CompletedTask;
        }

        try
        {
            udpClient = new UdpClient(endpoint);
        }
        catch (SocketException exception)
        {
            throw new InvalidOperationException($"Could not bind UDP receiver to {BindAddress}:{Port}. The port may already be in use.", exception);
        }

        cancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _ = Task.Run(() => ReceiveLoopAsync(cancellation.Token), CancellationToken.None);
        return Task.CompletedTask;
    }

    public void Stop()
    {
        cancellation?.Cancel();
        udpClient?.Dispose();
        udpClient = null;
    }

    private async Task ReceiveLoopAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested && udpClient is not null)
        {
            try
            {
                var result = await udpClient.ReceiveAsync(cancellationToken);
                var rawJson = Encoding.UTF8.GetString(result.Buffer);
                var envelope = SimHubPacketParser.ReadEnvelope(rawJson);
                if (SimHubPacketParser.TryParse(rawJson, out var snapshot, out var warning))
                {
                    var packet = new TelemetryPacketResult(
                        DateTimeOffset.UtcNow,
                        true,
                        snapshot,
                        null,
                        rawJson,
                        snapshot!.Source.Schema,
                        snapshot.Source.SchemaVersion);
                    PacketProcessed?.Invoke(this, packet);
                    SnapshotReceived?.Invoke(this, snapshot!);
                }
                else
                {
                    PacketProcessed?.Invoke(
                        this,
                        new TelemetryPacketResult(
                            DateTimeOffset.UtcNow,
                            false,
                            null,
                            warning ?? "Packet rejected.",
                            rawJson,
                            envelope.Schema,
                            envelope.SchemaVersion));
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (ObjectDisposedException) when (cancellationToken.IsCancellationRequested)
            {
                break;
            }
            catch
            {
                PacketProcessed?.Invoke(
                    this,
                    new TelemetryPacketResult(DateTimeOffset.UtcNow, false, null, "Receiver error while processing UDP packet."));
            }
        }
    }

    public ValueTask DisposeAsync()
    {
        Stop();
        cancellation?.Dispose();
        return ValueTask.CompletedTask;
    }
}
