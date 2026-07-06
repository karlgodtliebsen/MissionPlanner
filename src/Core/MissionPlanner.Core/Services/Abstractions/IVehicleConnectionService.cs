using MissionPlanner.Core.Models;

namespace MissionPlanner.Core.Services.Abstractions;

/// <summary>
/// Service for managing vehicle connections via MAVLink transport.
/// </summary>
public interface IVehicleConnectionService
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
    Task<VehicleConnectionResult> ConnectUdpAsync(int localPort, string? remoteHost = null, int? remotePort = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Disconnects a specific vehicle.
    /// </summary>
    /// <param name="vehicleId">The vehicle to disconnect</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task DisconnectAsync(VehicleId vehicleId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Disconnects all connected vehicles.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    Task DisconnectAllAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Result of a vehicle connection attempt.
/// </summary>
/// <param name="Success">Indicates if the connection was successful</param>
/// <param name="VehicleId">The connected vehicle's ID (null if failed)</param>
/// <param name="ErrorMessage">Error message if connection failed</param>
public record VehicleConnectionResult(bool Success, VehicleId? VehicleId, string? ErrorMessage = null);
