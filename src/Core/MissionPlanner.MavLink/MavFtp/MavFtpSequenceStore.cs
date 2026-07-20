using System.Collections.Concurrent;
using MissionPlanner.MavLink.MavFtp.Abstractions;

namespace MissionPlanner.MavLink.MavFtp;

/// <summary>
/// Keeps the next request sequence per remote MAVFTP endpoint for the application lifetime.
/// This allows a newly-created client to continue after a transport reconnect.
/// </summary>
public sealed class MavFtpSequenceStore : IMavFtpSequenceStore
{
    private readonly ConcurrentDictionary<MavFtpTarget, SequenceState> states = new();

    /// <summary>
    /// Provides the public API for GetNextRequest.
    /// </summary>
    public ushort GetNextRequest(MavFtpTarget target)
    {
        return states.GetOrAdd(target, static _ => new SequenceState()).Read();
    }

    /// <summary>
    /// Provides the public API for ObserveResponse.
    /// </summary>
    public void ObserveResponse(MavFtpTarget target, ushort responseSequence)
    {
        states.GetOrAdd(target, static _ => new SequenceState()).Write(MavFtpSequence.Next(responseSequence));
    }

    private sealed class SequenceState
    {
        private int nextRequest;
        public ushort Read() => unchecked((ushort)Volatile.Read(ref nextRequest));
        public void Write(ushort value) => Volatile.Write(ref nextRequest, value);
    }
}
