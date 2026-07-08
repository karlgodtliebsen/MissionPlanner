using MissionPlanner.Core.Models;
using MissionPlanner.MavLink.Client;
using MissionPlanner.MavLink.Services;
using MissionPlanner.Transport;
using MissionPlanner.Transport.Abstractions;

namespace MissionPlanner.Core.Services.Abstractions;

/// <summary>
/// Represents a session for a vehicle connection, managing its state and handling updates.
/// </summary>
public interface IVehicleConnectionSession
{
    /// <summary>
    /// Gets the established MAVLink connection. Throws an exception if no connection is established.
    /// </summary>
    IMavLinkConnection Connection { get; }

    /// <summary>
    /// Gets the established message pump. Throws an exception if no message pump is established.
    /// </summary>
    IVehicleMessagePump MessagePump { get; }

    /// <summary>
    /// Gets the established parameter service. Throws an exception if no parameter service is established.
    /// </summary>
    IVehicleParameterService ParameterService { get; }

    /// <summary>
    /// Gets the established parameter registry. Throws an exception if no parameter registry is established.
    /// </summary>
    IVehicleParameterRegistry ParameterRegistry { get; }

    /// <summary>
    /// Gets the established MAVLink transport. Throws an exception if no transport is established.
    /// </summary>
    IMavLinkTransport Transport { get; }

    /// <summary>
    /// Gets the established MAVLink client. Throws an exception if no client is established.
    /// </summary>
    IMavLinkClient Client { get; }

    /// <summary>
    /// Creates a serial connection to a vehicle using the specified port name and baud rate. Optionally, a configuration action can be provided to customize the transport endpoint settings. The connection process is cancellable via the provided cancellation token.
    /// </summary>
    /// <param name="portName"></param>
    /// <param name="baudRate"></param>
    /// <param name="configure"></param>
    /// <param name="cancellationToken"></param>
    CancellationTokenSource CreateSerialConnection(string portName, int baudRate = 57600, Action<TransportEndpoint>? configure = null, CancellationToken cancellationToken = default);

    CancellationTokenSource CreateTcpConnection(int port, string host, Action<TransportEndpoint>? configure = null, CancellationToken cancellationToken = default);
    CancellationTokenSource CreateUdpConnection(int localPort, string? remoteHost = null, int? remotePort = null, Action<TransportEndpoint>? configure = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Internal disconnect method - must be called with connectionLock held or from single-threaded context
    /// </summary>
    Task DisconnectAsync(VehicleId? vehicleId = null, CancellationToken cancellationToken = default);
}
