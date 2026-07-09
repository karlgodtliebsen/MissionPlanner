using System.Buffers;
using MissionPlanner.Transport;

namespace MissionPlanner.MavLink.Client;

/// <summary>
/// Represents one received transport byte block backed by a rented memory owner.
/// The consumer owns the instance and must dispose it after the bytes have been parsed/copied.
/// </summary>
public sealed class PooledMavLinkDataReceived : IDisposable
{
    private IMemoryOwner<byte>? owner;

    /// <summary>
    /// Initializes a new instance of the <see cref="PooledMavLinkDataReceived"/> class.
    /// </summary>
    /// <param name="owner">The memory owner containing the received data.</param>
    /// <param name="length">The length of the valid data in the memory owner.</param>
    /// <param name="remoteEndpoint">The remote endpoint from which the data was received.</param>
    /// <param name="receivedAt">The timestamp when the data was received.</param>
    /// <exception cref="ArgumentOutOfRangeException"></exception>
    /// <exception cref="ArgumentNullException"></exception>
    public PooledMavLinkDataReceived(
        IMemoryOwner<byte> owner,
        int length,
        TransportEndPoint? remoteEndpoint,
        DateTimeOffset receivedAt)
    {
        if (length < 0 || length > owner.Memory.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(length));
        }

        this.owner = owner ?? throw new ArgumentNullException(nameof(owner));
        Length = length;
        RemoteEndpoint = remoteEndpoint;
        ReceivedAt = receivedAt;
    }

    /// <summary>
    /// Gets the length of the valid data in the memory owner.
    /// </summary>
    public int Length { get; }

    /// <summary>
    /// Gets the remote endpoint from which the data was received.
    /// </summary>
    public TransportEndPoint? RemoteEndpoint { get; }

    /// <summary>
    /// Gets the timestamp when the data was received.
    /// </summary>
    public DateTimeOffset ReceivedAt { get; }

    /// <summary>
    /// Gets the data as a read-only memory block.
    /// </summary>
    public ReadOnlyMemory<byte> Data
    {
        get
        {
            ObjectDisposedException.ThrowIf(owner is null, this);
            return owner!.Memory[..Length];
        }
    }

    /// <summary>
    /// Gets the data as a read-only span.
    /// </summary>
    public ReadOnlySpan<byte> Span
    {
        get
        {
            ObjectDisposedException.ThrowIf(owner is null, this);
            return owner!.Memory.Span[..Length];
        }
    }

    /// <summary>
    /// Disposes the memory owner, releasing the rented memory back to the pool.
    /// </summary>
    public void Dispose()
    {
        owner?.Dispose();
        owner = null;
    }
}
