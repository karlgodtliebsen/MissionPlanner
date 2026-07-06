using MissionPlanner.Library;

namespace MissionPlanner.Transport;

/// <summary>
/// Represents a MAVLink transport that can be used by a <see cref="MavLinkClient"/> to send and receive data.
/// </summary>
public interface IMavLinkTransport : IAsyncDisposable
{
    /// <summary>
    /// Gets a value indicating whether the transport is currently connected.
    /// </summary>
    bool IsConnected { get; }

    /// <summary>
    /// Connects the transport.
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    Task ConnectAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Disconnects the transport.
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    Task DisconnectAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Reads data from the transport.
    /// </summary>
    /// <param name="buffer">The buffer to read data into.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A <see cref="TransportReceiveResult"/> containing the result of the read operation.</returns>  
    ValueTask<TransportReceiveResult> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken);

    /// <summary>
    /// Writes data to the transport.
    /// </summary>
    /// <param name="data">The data to write.</param>
    /// <param name="endPoint">The endpoint to write to.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A <see cref="ValueTask"/> representing the asynchronous operation.</returns>
    ValueTask WriteAsync(ReadOnlyMemory<byte> data, TransportEndPoint endPoint, CancellationToken cancellationToken);
}
