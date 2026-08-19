using MissionPlanner.MavLink.Client;
using MissionPlanner.MavLink.MavFtp.Abstractions;
using MissionPlanner.MavLink.Services.Abstractions;
using MissionPlanner.Transport.Abstractions;

namespace MissionPlanner.MavLink.Services;

/// <summary>
/// Represents a session for a vehicle connection, managing its state and handling updates.
/// </summary>
public interface IMavLinkConnectionSession : IAsyncDisposable
{
    /// <summary>
    /// Creates a MAVLink FTP client. Returns null if the client cannot be created.
    /// </summary>
    /// <returns>The created MAVLink FTP client or null.</returns>
    IMavFtpClient? CreateMavFtpClient();

    /// <summary>
    /// Gets the established MAVLink connection. Throws an exception if no connection is established.
    /// </summary>
    IMavLinkConnection Connection { get; }

    /// <summary>
    /// Gets the cancellation token source for the connection session.
    /// </summary>
    CancellationTokenSource CancellationTokenSource { get; }

    /// <summary>
    /// Gets the established MAVLink transport. Throws an exception if no transport is established.
    /// </summary>
    IMavLinkTransport Transport { get; }

    /// <summary>
    /// Gets the established MAVLink client. Throws an exception if no client is established.
    /// </summary>
    IMavLinkClient Client { get; }

    /// <summary>
    /// Internal disconnect method - must be called with connectionLock held or from single-threaded context
    /// </summary>
    Task DisconnectAsync(CancellationToken cancellationToken = default);
}
