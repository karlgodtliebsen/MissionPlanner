using MissionPlanner.Core.Models;

namespace MissionPlanner.Core.Services.Abstractions;

/// <summary>
/// Service for managing vehicle connections via MAVLink transport.
/// </summary>
public interface IVehicleConnectionService : IAsyncDisposable
{
    /// <summary>
    /// Gets a value indicating whether any vehicle is currently connected.
    /// </summary>
    bool IsConnected { get; }

    /// <summary>
    /// Gets the list of currently connected vehicle IDs.
    /// </summary>
    IReadOnlyCollection<VehicleId> ConnectedVehicles { get; }

    /// <summary>
    /// Connects to a vehicle via serial port.
    /// </summary>
    /// <param name="portName">Serial port name (e.g., "COM3", "/dev/ttyUSB0")</param>
    /// <param name="baudRate">Baud rate for the connection (default: 57600)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Connection result with vehicle ID if successful</returns>
    Task<VehicleConnectionResult> ConnectSerialAsync(string portName, int baudRate = 57600, CancellationToken cancellationToken = default);

    /// <summary>
    /// Connects to a vehicle via TCP.
    /// </summary>
    /// <param name="host">TCP host address</param>
    /// <param name="port">TCP port number</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Connection result with vehicle ID if successful</returns>
    Task<VehicleConnectionResult> ConnectTcpAsync(string host, int port, CancellationToken cancellationToken = default);

    /// <summary>
    /// Connects to a vehicle via UDP.
    /// </summary>
    /// <param name="localPort">Local UDP port to listen on</param>
    /// <param name="remoteHost">Optional remote host for outbound messages</param>
    /// <param name="remotePort">Optional remote port for outbound messages</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Connection result with vehicle ID if successful</returns>
    Task<VehicleConnectionResult> ConnectUdpAsync(int localPort, string? remoteHost = null, int? remotePort = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Disconnects the transport.
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    Task DisconnectAsync(CancellationToken cancellationToken = default);
}
