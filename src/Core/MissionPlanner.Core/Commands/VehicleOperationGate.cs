using System.Collections.Concurrent;
using MissionPlanner.Core.Vehicles.Models;

namespace MissionPlanner.Core.Commands;

/// <summary>Provides process-local, per-vehicle operation reservations.</summary>
public sealed class VehicleOperationGate : IVehicleOperationGate
{
    private readonly ConcurrentDictionary<VehicleId, Reservation> reservations = [];

    /// <inheritdoc />
    public bool TryAcquire(VehicleId vehicleId, string operationName, out IDisposable? lease)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(operationName);
        var reservation = new Reservation(this, vehicleId, operationName);
        if (!reservations.TryAdd(vehicleId, reservation))
        {
            lease = null;
            return false;
        }

        lease = reservation;
        return true;
    }

    /// <inheritdoc />
    public string? GetCurrentOperation(VehicleId vehicleId) =>
        reservations.TryGetValue(vehicleId, out var reservation) ? reservation.OperationName : null;

    private void Release(Reservation reservation) =>
        reservations.TryRemove(new KeyValuePair<VehicleId, Reservation>(reservation.VehicleId, reservation));

    private sealed class Reservation(VehicleOperationGate owner, VehicleId vehicleId, string operationName) : IDisposable
    {
        private int disposed;

        public VehicleId VehicleId { get; } = vehicleId;

        public string OperationName { get; } = operationName;

        public void Dispose()
        {
            if (Interlocked.Exchange(ref disposed, 1) == 0)
            {
                owner.Release(this);
            }
        }
    }
}
