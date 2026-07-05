using System.Net;
using MissionPlanner.Transport;

namespace MissionPlanner.MavLink.Client;

/// <summary>
/// Represents a MAVLink client that can send and receive data from a MAVLink transport.
/// </summary>
public interface IMavLinkClient : IAsyncDisposable
{
    /// <summary>
    /// Occurs when data is received from the MAVLink transport.
    /// </summary>
    event Func<MavLinkDataReceived, CancellationToken, Task>? DataReceived;

    /// <summary>
    /// Gets a value indicating whether the MAVLink client is currently running and receiving data.
    /// </summary>
    bool IsRunning { get; }

    /// <summary>
    /// Gets a value indicating whether the MAVLink client is connected to the transport.
    /// </summary>
    bool IsConnected { get; }

    /// <summary>
    /// Starts the MAVLink client and begins receiving data.
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    Task StartAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends data to the MAVLink transport.
    /// </summary>
    /// <param name="data">The data to send.</param>
    /// <param name="ipEndpoint">The IP endpoint to send the data to.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <exception cref="InvalidOperationException">Thrown if the transport is not connected.</exception>
    ValueTask SendAsync(ReadOnlyMemory<byte> data, IPEndPoint ipEndpoint, CancellationToken cancellationToken = default);

    /// <summary>
    /// Stops the MAVLink client and cancels any ongoing operations.
    /// </summary>
    Task StopAsync();
}