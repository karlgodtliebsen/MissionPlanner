using Microsoft.Extensions.Options;
using MissionPlanner.MavLink.Client;
using MissionPlanner.MavLink.Services.Abstractions;
using MissionPlanner.Transport;
using MissionPlanner.Transport.Abstractions;

namespace MissionPlanner.MavLink.Services;

public sealed partial class MavLinkConnectionSessionFactory
{
    /// <inheritdoc />
    public async Task<IMavLinkConnectionSession> CreateNetworkConnection(IOptions<TransportEndpoint> options,
        byte gcsSystemId = 255, CancellationToken cancellationToken = default)
    {
        IMavLinkTransport transport = options.Value.Protocol switch
        {
            "udp" or "udpcl" => domainFactory.Create<IUdpMavLinkTransport, IOptions<TransportEndpoint>>(options),
            "ws" or "wss" => domainFactory.Create<IWebSocketMavLinkTransport, IOptions<TransportEndpoint>>(options),
            _ => throw new ArgumentException("Unsupported network transport.")
        };
        var client = domainFactory.Create<IMavLinkClient, IMavLinkTransport>(transport);
        var connection = domainFactory.Create<IMavLinkConnection, IMavLinkClient>(client);
        var lifetime = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        lifetime.CancelAfter(TimeSpan.FromSeconds(10));
        try
        {
            await connection.StartAsync(lifetime.Token).ConfigureAwait(false);
            lifetime.CancelAfter(Timeout.InfiniteTimeSpan);
            TransportEndPoint? peer = transport switch
            {
                UdpMavLinkTransport udp => udp.RemotePeer,
                WebSocketMavLinkTransport ws => ws.RemotePeer,
                _ => null
            };
            var heartbeat = peer is null ? Task.CompletedTask : RunHeartbeatAsync(connection, peer, gcsSystemId, lifetime.Token);
            return domainFactory.Create<IMavLinkConnectionSession, IMavLinkTransport, IMavLinkClient, IMavLinkConnection, CancellationTokenSource, Task>(
                transport, client, connection, lifetime, heartbeat);
        }
        catch
        {
            await lifetime.CancelAsync().ConfigureAwait(false);
            await connection.DisposeAsync().ConfigureAwait(false);
            await client.StopAsync().ConfigureAwait(false);
            await transport.DisposeAsync().ConfigureAwait(false);
            lifetime.Dispose();
            throw;
        }
    }

    private async Task RunHeartbeatAsync(IMavLinkConnection connection, TransportEndPoint peer, byte systemId, CancellationToken token)
    {
        using var lifetime = CancellationTokenSource.CreateLinkedTokenSource(token,
            connection.Activity?.LifetimeToken ?? CancellationToken.None);
        byte sequence = 0;
        try
        {
            while (!lifetime.IsCancellationRequested)
            {
                var frame = MavLinkKnownFrames.CreateHeartbeatV2(crcProvider, sequence++, systemId, 190,
                    vehicleType: 6, autopilot: 8);
                await connection.SendRawAsync(frame, peer, lifetime.Token).ConfigureAwait(false);
                await Task.Delay(TimeSpan.FromSeconds(1), lifetime.Token).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (lifetime.IsCancellationRequested)
        {
        }
        catch
        {
            // A failed startup/write must end this session rather than leave a silent socket.
            await connection.DisposeAsync().ConfigureAwait(false);
        }
    }
}
