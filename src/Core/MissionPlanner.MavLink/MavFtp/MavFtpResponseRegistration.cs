using System.Threading.Channels;

namespace MissionPlanner.MavLink.MavFtp;

/// <summary>
/// Represents a registration for receiving MAVFTP responses.
/// </summary>
public sealed class MavFtpResponseRegistration : IDisposable
{
    private readonly Channel<MavFtpPacket> responses;
    private readonly Action dispose;
    private int disposed;

    internal MavFtpResponseRegistration(MavFtpTarget target, ushort requestSequence, MavFtpOpcode requestedOpcode, byte? session, bool multipleResponses, int capacity, Action dispose)
    {
        Target = target;
        RequestSequence = requestSequence;
        RequestedOpcode = requestedOpcode;
        Session = session;
        MultipleResponses = multipleResponses;
        this.dispose = dispose;
        responses = Channel.CreateBounded<MavFtpPacket>(new BoundedChannelOptions(capacity) { SingleReader = true, SingleWriter = false, FullMode = BoundedChannelFullMode.Wait });
    }

    internal MavFtpTarget Target { get; }
    internal ushort RequestSequence { get; }
    internal MavFtpOpcode RequestedOpcode { get; }
    internal byte? Session { get; }
    internal bool MultipleResponses { get; }

    internal bool TryWrite(MavFtpPacket packet)
    {
        return responses.Writer.TryWrite(packet);
    }

    internal void Fail(Exception exception)
    {
        responses.Writer.TryComplete(exception);
    }

    /// <summary>
    /// Reads a MAVFTP response packet asynchronously.
    /// </summary>
    /// <param name="timeout">The timeout for the read operation.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The MAVFTP response packet.</returns>
    public ValueTask<MavFtpPacket> ReadAsync(TimeSpan timeout, CancellationToken cancellationToken)
    {
        return new ValueTask<MavFtpPacket>(responses.Reader.ReadAsync(cancellationToken).AsTask().WaitAsync(timeout, cancellationToken));
    }

    /// <summary>
    /// Disposes the registration and releases associated resources.
    /// </summary>
    public void Dispose()
    {
        if (Interlocked.Exchange(ref disposed, 1) != 0)
        {
            return;
        }

        responses.Writer.TryComplete();
        dispose();
    }
}
