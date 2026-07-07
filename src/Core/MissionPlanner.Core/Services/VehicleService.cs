using System.Collections.ObjectModel;
using MissionPlanner.Core.Commands;
using MissionPlanner.Core.Models;
using MissionPlanner.Core.Services.Abstractions;

namespace MissionPlanner.Core.Services;

/// <summary>
/// Represents a service for managing vehicles and their states.
/// </summary>
/// <param name="registry">The vehicle registry to be used by the service.</param>
/// <param name="commandService">The vehicle command service to be used by the service.</param>
public sealed class VehicleService(IVehicleRegistry registry, IVehicleCommandService commandService) : IVehicleService
{
    /// <inheritdoc />
    public IReadOnlyCollection<VehicleState> GetVehicles()
    {
        return registry.Vehicles
            .Select(vehicle => vehicle.State)
            .ToArray();
    }

    /// <inheritdoc />
    public VehicleState? GetVehicleState(VehicleId vehicleId)
    {
        return registry.GetRequired(vehicleId)?.State;
    }

    /// <inheritdoc />
    public VehicleSession? GetVehicle(VehicleId vehicleId)
    {
        return registry.Vehicles.FirstOrDefault(v => v.State.VehicleId == vehicleId);
    }

    /// <inheritdoc />
    public IReadOnlyCollection<VehicleStatusText> GetVehicleNotifications(VehicleId vehicleId)
    {
        return new ReadOnlyCollection<VehicleStatusText>(registry.GetRequired(vehicleId)!.Notifications);
    }


    /// <inheritdoc />
    public async Task<VehicleCommandResponse> ArmAsync(VehicleId vehicleId, CancellationToken cancellationToken)
    {
        EnsureVehicleExists(vehicleId);
        var result = await commandService.ArmAsync(vehicleId, cancellationToken);

        if (result.Result == VehicleCommandResult.Accepted)
        {
            var vehicle = registry.GetRequired(vehicleId);
            vehicle!.ApplyArm(true);
        }

        return result;
    }

    /// <inheritdoc />
    public async Task<VehicleCommandResponse> DisarmAsync(VehicleId vehicleId, CancellationToken cancellationToken)
    {
        EnsureVehicleExists(vehicleId);
        var result = await commandService.DisarmAsync(vehicleId, cancellationToken);
        if (result.Result == VehicleCommandResult.Accepted)
        {
            var vehicle = registry.GetRequired(vehicleId);
            vehicle!.ApplyArm(false);
        }

        return result;
    }

    /// <inheritdoc />  
    public async Task<VehicleCommandResponse> SetModeAsync(VehicleId vehicleId, VehicleMode mode, CancellationToken cancellationToken)
    {
        EnsureVehicleExists(vehicleId);
        var result = await commandService.SetModeAsync(vehicleId, mode, cancellationToken);
        if (result.Result == VehicleCommandResult.Accepted)
        {
            var vehicle = registry.GetRequired(vehicleId);
            vehicle!.ApplyMode(mode);
        }

        return result;
    }

    private void EnsureVehicleExists(VehicleId vehicleId)
    {
        registry.GetRequired(vehicleId);
    }
}
