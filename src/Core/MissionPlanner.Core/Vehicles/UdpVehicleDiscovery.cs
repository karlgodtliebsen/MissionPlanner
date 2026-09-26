using System.Net;
using System.Net.Sockets;
using MissionPlanner.Core.Vehicles.Abstractions;
using MissionPlanner.MavLink.Messages;
using MissionPlanner.MavLink.Services.Abstractions;
using MissionPlanner.Transport;

namespace MissionPlanner.Core.Vehicles;

/// <summary>Receives validated heartbeats without transmitting or registering a vehicle.</summary>
public sealed class UdpVehicleDiscovery(IMavLinkFrameParser parser, IMavLinkMessageDecodeHandler decoder) : IUdpVehicleDiscovery
{
    /// <inheritdoc />
    public async Task<IPEndPoint?> FindAsync(int localPort, CancellationToken cancellationToken = default)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(TimeSpan.FromSeconds(2));
        using var socket = new UdpClient(new IPEndPoint(IPAddress.Any, localPort));
        try
        {
            while (true)
            {
                var datagram = await socket.ReceiveAsync(deadline.Token).ConfigureAwait(false);
                // Never combine partial frames from different UDP senders.
                parser.Reset();
                foreach (var frame in parser.Parse(datagram.Buffer,
                    new TransportEndPoint("udp", datagram.RemoteEndPoint.Address.ToString(), datagram.RemoteEndPoint.Port), DateTimeOffset.UtcNow))
                {
                    if (decoder.TryDecode(frame, out var message) && message is HeartbeatMessage heartbeat
                        && heartbeat.ComponentId == 1 && heartbeat.Autopilot == 3)
                        return datagram.RemoteEndPoint;
                }
            }
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return null;
        }
    }
}
