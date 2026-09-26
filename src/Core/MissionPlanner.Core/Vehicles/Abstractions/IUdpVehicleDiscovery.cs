using System.Net;

namespace MissionPlanner.Core.Vehicles.Abstractions;

/// <summary>Passively detects an ArduPilot UDP sender without creating a vehicle connection.</summary>
public interface IUdpVehicleDiscovery
{
    /// <summary>Listens briefly on the selected local port; returns null if no ArduPilot heartbeat arrives.</summary>
    Task<IPEndPoint?> FindAsync(int localPort, CancellationToken cancellationToken = default);
}
