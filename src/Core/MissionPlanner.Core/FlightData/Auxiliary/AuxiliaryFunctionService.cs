using MissionPlanner.Core.Commands;
using MissionPlanner.MavLink.Generated;

namespace MissionPlanner.Core.FlightData.Auxiliary;

/// <summary>Sends <see cref="MavCmd.DoAuxFunction"/> through the shared command workflow.</summary>
public sealed class AuxiliaryFunctionService(IVehicleCommandService commands, IAuxiliaryFunctionPolicy policy)
    : IAuxiliaryFunctionService
{
    /// <inheritdoc />
    public async Task<AuxiliaryFunctionResult> ExecuteAsync(AuxiliaryFunctionRequest request, CancellationToken cancellationToken)
    {
        var denied = policy.GetDenialReason(request);
        if (denied is not null)
        {
            return new AuxiliaryFunctionResult(null, false, denied);
        }

        var response = await commands.ExecuteExpertAsync(new ExpertVehicleCommand(request.Vehicle.VehicleId,
            (ushort)MavCmd.DoAuxFunction, [request.Function.Id, (float)request.Level, 0, 0, 0, 0, 0]), true, cancellationToken);
        return new AuxiliaryFunctionResult(response, response.Result == VehicleCommandResult.Accepted,
            $"Command {response.Result}; the ACK does not confirm the resulting switch state.");
    }
}
