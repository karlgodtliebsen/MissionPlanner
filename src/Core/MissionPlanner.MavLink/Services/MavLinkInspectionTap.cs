using System.Threading.Channels;
using MissionPlanner.MavLink.Messages;

namespace MissionPlanner.MavLink.Services;

/// <summary>Traffic direction as observed by the existing connection pipeline.</summary>
public enum MavLinkTrafficDirection
{
    /// <summary>A received frame or unsupported-dialect candidate.</summary>
    Inbound,
    /// <summary>A frame successfully handed to the transport.</summary>
    Outbound
}

/// <summary>Immutable inspection observation retaining exact bytes and an already-decoded message when available.</summary>
public sealed record MavLinkInspectionObservation(MavLinkTrafficDirection Direction, MavLinkFrame Frame,
    MavLinkMessage? Message, bool CrcVerified)
{
    /// <summary>Gets authentication evidence independently of CRC validation.</summary>
    public MissionPlanner.MavLink.Signing.MavLinkSignatureStatus Signature { get; init; } = MissionPlanner.MavLink.Signing.MavLinkSignatureStatus.Unverified;
}

/// <summary>A bounded observer lease. Disposing it never disposes the vehicle connection.</summary>
public sealed class MavLinkInspectionLease : IDisposable
{
    private readonly Action<MavLinkInspectionLease> release;
    private readonly Channel<MavLinkInspectionObservation> channel;
    private long dropped;
    private long droppedBytes;
    private int disposed;

    internal MavLinkInspectionLease(Action<MavLinkInspectionLease> release, int capacity)
    {
        this.release = release;
        channel = Channel.CreateBounded<MavLinkInspectionObservation>(new BoundedChannelOptions(capacity)
        {
            SingleReader = false,
            SingleWriter = false,
            FullMode = BoundedChannelFullMode.Wait,
            AllowSynchronousContinuations = false
        });
    }

    /// <summary>Gets this observer's independent read stream.</summary>
    public ChannelReader<MavLinkInspectionObservation> Reader => channel.Reader;
    /// <summary>Gets observations dropped when this observer's queue was full.</summary>
    public long Dropped => Interlocked.Read(ref dropped);
    /// <summary>Gets bytes omitted when this observer's queue was full.</summary>
    public long DroppedBytes => Interlocked.Read(ref droppedBytes);

    internal void Offer(MavLinkInspectionObservation observation)
    {
        if (Volatile.Read(ref disposed) == 0 && !channel.Writer.TryWrite(observation))
        {
            Interlocked.Increment(ref dropped);
            Interlocked.Add(ref droppedBytes, observation.Frame.RawBytes.Length);
        }
    }

    /// <summary>Detaches the observer and completes its stream while preserving queued frames for draining.</summary>
    public void Complete()
    {
        if (Interlocked.Exchange(ref disposed, 1) == 0)
        {
            release(this);
            channel.Writer.TryComplete();
        }
    }

    /// <summary>Releases the observer and its pending observations immediately.</summary>
    public void Dispose()
    {
        if (Interlocked.Exchange(ref disposed, 1) == 0)
        {
            release(this);
            channel.Writer.TryComplete();
            while (channel.Reader.TryRead(out _))
            {
            }
        }
    }
}

/// <summary>Bounded read-only fan-out owned by one connection, with no work when no observer exists.</summary>
public sealed class MavLinkInspectionTap
{
    private readonly object sync = new();
    private MavLinkInspectionLease[] observers = [];

    /// <summary>Gets whether traffic needs to be retained for inspection.</summary>
    public bool HasObservers => Volatile.Read(ref observers).Length != 0;

    /// <summary>Acquires one of at most eight bounded observer queues.</summary>
    public MavLinkInspectionLease Subscribe(int capacity = 512)
    {
        if (capacity is < 1 or > 4096)
        {
            throw new ArgumentOutOfRangeException(nameof(capacity));
        }
        lock (sync)
        {
            if (observers.Length >= 8)
            {
                throw new InvalidOperationException("The connection already has eight inspection observers.");
            }
            var lease = new MavLinkInspectionLease(Remove, capacity);
            Volatile.Write(ref observers, [.. observers, lease]);
            return lease;
        }
    }

    /// <summary>Offers an observation without awaiting or blocking on any consumer.</summary>
    public void Publish(MavLinkInspectionObservation observation)
    {
        if (observation.Frame.MessageId == 256)
        {
            return; // SETUP_SIGNING contains secret material and is never diagnostic traffic.
        }
        foreach (var observer in Volatile.Read(ref observers))
        {
            observer.Offer(observation);
        }
    }

    /// <summary>Closes all leases at a connection boundary.</summary>
    public void CloseObservers()
    {
        foreach (var observer in Volatile.Read(ref observers))
        {
            observer.Dispose();
        }
    }

    private void Remove(MavLinkInspectionLease lease)
    {
        lock (sync)
        {
            Volatile.Write(ref observers, observers.Where(item => !ReferenceEquals(item, lease)).ToArray());
        }
    }
}
