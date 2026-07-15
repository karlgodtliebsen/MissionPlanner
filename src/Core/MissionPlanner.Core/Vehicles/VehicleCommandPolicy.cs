using MissionPlanner.Core.Commands;
using MissionPlanner.Core.Vehicles.Abstractions;
using MissionPlanner.Core.Vehicles.Models;

namespace MissionPlanner.Core.Vehicles;

/// <inheritdoc />
public sealed class VehicleCommandPolicy : IVehicleCommandPolicy
{
    /// <inheritdoc />
    public VehicleCommandResponse? ValidateArm(VehicleState state)
    {
        return ValidateOnline(state);
    }

    /// <inheritdoc />      
    public VehicleCommandResponse? ValidateDisarm(VehicleState state)
    {
        return ValidateOnline(state);
    }

    /// <inheritdoc />
    public VehicleCommandResponse? ValidateSetMode(VehicleState state, VehicleMode mode)
    {
        var onlineValidation = ValidateOnline(state);

        return onlineValidation ?? (mode == VehicleMode.Guided && !state.IsArmed
            ? Denied(state.VehicleId)
            : null);
    }

    private static VehicleCommandResponse? ValidateOnline(VehicleState state)
    {
        return state.ConnectionState == VehicleConnectionState.Online ? null : Denied(state.VehicleId);
    }

    private static VehicleCommandResponse Denied(VehicleId vehicleId)
    {
        return new VehicleCommandResponse(vehicleId, VehicleCommandResult.Denied, DateTimeOffset.UtcNow);
    }
}
