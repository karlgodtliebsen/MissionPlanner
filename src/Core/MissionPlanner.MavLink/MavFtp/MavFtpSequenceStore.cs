using System.Collections.Concurrent;
using MissionPlanner.MavLink.MavFtp.Abstractions;

namespace MissionPlanner.MavLink.MavFtp;

/// <summary>
/// Keeps and serializes MAVFTP conversation state per remote endpoint for the application lifetime.
/// This allows newly-created clients to continue safely after navigation or a transport reconnect.
/// </summary>
public sealed class MavFtpSequenceStore : IMavFtpSequenceStore
{
    private readonly ConcurrentDictionary<MavFtpTarget, SequenceState> states = new();

    /// <summary>
    /// Enters the shared operation gate for the target.
    /// </summary>
    public async ValueTask<IDisposable> EnterOperationAsync(
        MavFtpTarget target,
        CancellationToken cancellationToken = default)
    {
        var state = states.GetOrAdd(target, static _ => new SequenceState());
        await state.Gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        return new OperationLease(state.Gate);
    }

    /// <summary>
    /// Reserves and returns the next request sequence for the target.
    /// </summary>
    public ushort GetNextRequest(MavFtpTarget target)
    {
        return states.GetOrAdd(target, static _ => new SequenceState()).Reserve();
    }

    /// <summary>
    /// Advances the target sequence after observing a response.
    /// </summary>
    public void ObserveResponse(MavFtpTarget target, ushort responseSequence)
    {
        states.GetOrAdd(target, static _ => new SequenceState()).Observe(responseSequence);
    }

    private sealed class SequenceState
    {
        private readonly Lock syncRoot = new();
        private ushort nextRequest;

        public SemaphoreSlim Gate { get; } = new(1, 1);

        public ushort Reserve()
        {
            lock (syncRoot)
            {
                var reserved = nextRequest;

                // A normal server reply consumes the following sequence value. Reserve
                // both slots immediately so cancellation after send cannot cause a new
                // client to reuse a request sequence cached by ArduPilot.
                nextRequest = MavFtpSequence.Next(MavFtpSequence.Next(reserved));
                return reserved;
            }
        }

        public void Observe(ushort responseSequence)
        {
            lock (syncRoot)
            {
                nextRequest = MavFtpSequence.Next(responseSequence);
            }
        }
    }

    private sealed class OperationLease(SemaphoreSlim gate) : IDisposable
    {
        private int disposed;

        public void Dispose()
        {
            if (Interlocked.Exchange(ref disposed, 1) == 0)
            {
                gate.Release();
            }
        }
    }
}
