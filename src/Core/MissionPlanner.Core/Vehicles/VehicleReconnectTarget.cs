namespace MissionPlanner.Core.Vehicles;

/// <summary>Connection settings captured before an operation changes a vehicle identifier.</summary>
/// <param name="ConnectionId">Original connection generation, used for owned cleanup.</param>
/// <param name="ConnectionType">Serial, TCP or UDP.</param>
/// <param name="Address">Serial device name, TCP host, or optional UDP remote host.</param>
/// <param name="Port">TCP port or UDP local port; zero for serial.</param>
/// <param name="BaudRate">Original serial baud rate; zero for network transports.</param>
public sealed record VehicleReconnectTarget(Guid ConnectionId, string ConnectionType, string? Address, int Port, int BaudRate)
{
    /// <summary>Gets the optional UDP remote port.</summary>
    public int? RemotePort { get; init; }

    /// <summary>Gets the endpoint description shown during recovery.</summary>
    public string Description => ConnectionType == "Serial" ? Address ?? "serial device" : $"{ConnectionType} {Address}:{Port}";
}
