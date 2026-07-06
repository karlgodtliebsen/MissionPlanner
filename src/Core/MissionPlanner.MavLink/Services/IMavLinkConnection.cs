using MissionPlanner.Transport;

namespace MissionPlanner.MavLink.Services;

//Frame parser
//Frame serializer
//Message definitions
//CRC handling
//Dialect generation

/// <summary>
/// Represents a connection to a MAVLink device.
/// </summary>
public interface IMavLinkConnection : IAsyncDisposable
{
    /// <summary>
    /// Starts the MAVLink connection, allowing it to receive and process incoming data.
    /// </summary>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A task that represents the asynchronous start operation.</returns>
    Task StartAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends raw MAVLink data to the connected device.
    /// </summary>
    /// <param name="data">The raw MAVLink data to send.</param>
    /// <param name="endpoint">The endpoint to send the data to.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A task that represents the asynchronous send operation.</returns>
    ValueTask SendRawAsync(ReadOnlyMemory<byte> data, TransportEndPoint endpoint, CancellationToken cancellationToken = default);
}
