namespace MissionPlanner.MavLink.MavFtp.Abstractions;

/// <summary>
/// Retains and coordinates MAVFTP conversation state independently of connection-scoped clients.
/// </summary>
public interface IMavFtpSequenceStore
{
    /// <summary>
    /// Enters the application-wide operation gate for one MAVFTP target.
    /// </summary>
    /// <param name="target">The remote MAVFTP target.</param>
    /// <param name="cancellationToken">A token that cancels waiting for the gate.</param>
    /// <returns>A lease that releases the target gate when disposed.</returns>
    ValueTask<IDisposable> EnterOperationAsync(
        MavFtpTarget target,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Reserves and returns the next request sequence.
    /// </summary>
    /// <param name="target">The remote MAVFTP target.</param>
    /// <returns>The sequence reserved for the request.</returns>
    ushort GetNextRequest(MavFtpTarget target);

    /// <summary>
    /// Advances the conversation after observing a response.
    /// </summary>
    /// <param name="target">The remote MAVFTP target.</param>
    /// <param name="responseSequence">The received response sequence.</param>
    void ObserveResponse(MavFtpTarget target, ushort responseSequence);
}
