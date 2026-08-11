using MissionPlanner.Core.Replay;
using MissionPlanner.Shared.Models.Vehicles.Models;

namespace MissionPlanner.Core.FlightData.Auxiliary;

/// <summary>Conservative policy for generic auxiliary commands.</summary>
public sealed class AuxiliaryFunctionPolicy(IReplaySessionManager? replay = null) : IAuxiliaryFunctionPolicy
{
    /// <inheritdoc />
    public string? GetDenialReason(AuxiliaryFunctionRequest request)
    {
        if (replay is not null && replay.Snapshot.State != ReplaySessionState.Unloaded)
        {
            return "Auxiliary functions are blocked during replay.";
        }

        if (request.Vehicle.ConnectionState != VehicleConnectionState.Online)
        {
            return "The vehicle is offline.";
        }

        if (!request.Function.IsSupported)
        {
            return request.Function.PreferredWorkflow is { } workflow
                ? $"Use {workflow} for this function."
                : "This function is intentionally unavailable in the generic workflow.";
        }

        if (request.Function.Hazard != AuxiliaryFunctionHazard.Safe && !request.Confirmed)
        {
            return "Explicit confirmation is required for this function.";
        }

        return null;
    }
}
