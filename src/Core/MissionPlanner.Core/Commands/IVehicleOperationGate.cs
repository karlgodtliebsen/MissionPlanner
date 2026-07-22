using MissionPlanner.Core.Vehicles.Models;

namespace MissionPlanner.Core.Commands;

/// <summary>Prevents competing state-changing operations from targeting one vehicle.</summary>
public interface IVehicleOperationGate
{
    /// <summary>Attempts to reserve a vehicle for one named operation.</summary>
    /// <param name="vehicleId">The target vehicle.</param>
    /// <param name="operationName">The operation shown in conflict messages.</param>
    /// <param name="lease">A lease that releases the reservation when disposed.</param>
    /// <returns><see langword="true"/> when the reservation was acquired.</returns>
    bool TryAcquire(VehicleId vehicleId, string operationName, out IDisposable? lease);

    /// <summary>Gets the operation currently holding a vehicle reservation.</summary>
    /// <param name="vehicleId">The target vehicle.</param>
    /// <returns>The operation name, or <see langword="null"/> when available.</returns>
    string? GetCurrentOperation(VehicleId vehicleId);
}
