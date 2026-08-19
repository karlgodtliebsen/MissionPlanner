using Microsoft.Extensions.Options;
using MissionPlanner.Transport;

namespace MissionPlanner.MavLink.Services;

/// <summary>
/// Represents a session for a vehicle connection, managing its state and handling updates.
/// </summary>
public interface IMavLinkConnectionSessionFactory
{
    /// <summary>
    /// Creates a serial connection to a vehicle using the specified port name and baud rate. Optionally, a configuration action can be provided to customize the transport endpoint settings. The connection process is cancellable via the provided cancellation token.
    /// </summary>
    /// <param name="transportOptions"></param>
    /// <param name="cancellationToken"></param>
    /// <returns>The created MAVLink connection session.</returns>
    Task<IMavLinkConnectionSession> CreateSerialConnection(IOptions<TransportEndpoint> transportOptions, CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a TCP connection to a vehicle using the specified port and host. Optionally, a configuration action can be provided to customize the transport endpoint settings. The connection process is cancellable via the provided cancellation token.
    /// </summary>
    /// <param name="transportOptions"></param>
    /// <param name="cancellationToken">A cancellation token to cancel the connection process.</param>
    /// <returns>The created MAVLink connection session.</returns>
    Task<IMavLinkConnectionSession> CreateTcpConnection(IOptions<TransportEndpoint> transportOptions, CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a UDP connection to a vehicle using the specified local port and optional remote host and port. Optionally, a configuration action can be provided to customize the transport endpoint settings. The connection process is cancellable via the provided cancellation token.
    /// </summary>
    /// <param name="transportOptions"></param>
    /// <param name="cancellationToken"></param>
    /// <returns>The created MAVLink connection session.</returns>
    Task<IMavLinkConnectionSession> CreateUdpConnection(IOptions<TransportEndpoint> transportOptions, CancellationToken cancellationToken = default);
}
