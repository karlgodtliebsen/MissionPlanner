using System.Threading.Channels;
using MissionPlanner.Transport;

namespace MissionPlanner.MavLink.Client;

/// <summary>
/// Interface for a MAVLink client.
/// </summary>
public interface IMavLinkClient : IAsyncDisposable
{
    /// <summary>
    /// Gets a value indicating whether the MAVLink client is running.
    /// </summary>
    bool IsRunning { get; }

    /// <summary>
    /// Gets a value indicating whether the MAVLink client is connected.
    /// </summary>  
    bool IsConnected { get; }

    /// <summary>
    /// Stream of received byte blocks. Each item is owned by the consumer and must be disposed.
    /// </summary>
    ChannelReader<PooledMavLinkDataReceived> ReceivedBytes { get; }

    /// <summary>
    /// Starts the MAVLink client, initiating the reception loop and enabling data transmission.
    /// </summary>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    Task StartAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Stops the MAVLink client, terminating the reception loop and disabling data transmission. 
    /// </summary>
    /// <returns></returns>
    Task StopAsync();

    /// <summary>
    /// Sends data asynchronously to the specified transport endpoint.
    /// </summary>
    /// <param name="data"></param>
    /// <param name="endPoint"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    ValueTask SendAsync(ReadOnlyMemory<byte> data, TransportEndPoint endPoint, CancellationToken cancellationToken = default);
}
