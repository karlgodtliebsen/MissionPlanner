using MissionPlanner.Core.Commands;
using MissionPlanner.Core.Replay;
using MissionPlanner.Core.Vehicles.Models;
using MissionPlanner.MavLink.Generated;
using MissionPlanner.Shared.Models.Vehicles.Models;

namespace MissionPlanner.Core.FlightData.Auxiliary;

/// <summary>Classifies the operator risk of an auxiliary function.</summary>
public enum AuxiliaryFunctionHazard
{
    /// <summary>No additional confirmation is required.</summary>
    Safe,
    /// <summary>The operator must explicitly confirm the action.</summary>
    Warning,
    /// <summary>The action is safety-critical and may be unavailable generically.</summary>
    High
}

/// <summary>Describes how an auxiliary function consumes switch input.</summary>
public enum AuxiliarySwitchBehavior
{
    /// <summary>The function consumes low, middle, and high states.</summary>
    ThreePosition,
    /// <summary>The function represents a bounded press/release action.</summary>
    Momentary
}

/// <summary>Describes one reviewed ArduPilot auxiliary function.</summary>
public sealed record AuxiliaryFunctionDescriptor(int Id, string Name, string Description, string Category,
    AuxiliarySwitchBehavior SwitchBehavior, AuxiliaryFunctionHazard Hazard, string? PreferredWorkflow = null,
    bool IsSupported = true);

/// <summary>Requests execution of an auxiliary function at a generated switch level.</summary>
public sealed record AuxiliaryFunctionRequest(VehicleState Vehicle, AuxiliaryFunctionDescriptor Function,
    MavCmdDoAuxFunctionSwitchLevel Level, bool Confirmed);

/// <summary>Reports an auxiliary-function outcome without implying observed state.</summary>
public sealed record AuxiliaryFunctionResult(VehicleCommandResponse? Acknowledgement, bool IsAccepted, string Summary);

/// <summary>Provides firmware-aware auxiliary-function descriptors.</summary>
public interface IAuxiliaryFunctionCatalog
{
    /// <summary>Returns the reviewed catalog. Unknown IDs can be represented with <see cref="DescribeUnknown"/>.</summary>
    IReadOnlyList<AuxiliaryFunctionDescriptor> GetFunctions(VehicleState vehicle);
    /// <summary>Creates a non-executable descriptor for an ID absent from the reviewed catalog.</summary>
    AuxiliaryFunctionDescriptor DescribeUnknown(int id);
}

/// <summary>Evaluates whether an auxiliary function can use the generic workflow.</summary>
public interface IAuxiliaryFunctionPolicy
{
    /// <summary>Returns a denial reason, or <see langword="null"/> when execution is allowed.</summary>
    string? GetDenialReason(AuxiliaryFunctionRequest request);
}

/// <summary>Executes typed, acknowledged auxiliary-function commands.</summary>
public interface IAuxiliaryFunctionService
{
    /// <summary>Executes one request against its current vehicle.</summary>
    Task<AuxiliaryFunctionResult> ExecuteAsync(AuxiliaryFunctionRequest request, CancellationToken cancellationToken);
}

/// <summary>Reviewed baseline catalog; parameter-derived unknown IDs remain explicit and disabled.</summary>
public sealed class AuxiliaryFunctionCatalog : IAuxiliaryFunctionCatalog
{
    private static readonly AuxiliaryFunctionDescriptor[] functions =
    [
        new(28, "Relay 1", "Operate relay output 1.", "Actuator", AuxiliarySwitchBehavior.ThreePosition,
            AuxiliaryFunctionHazard.Warning, "Servo / Relay", false),
        new(9, "Camera trigger", "Trigger the selected camera.", "Payload", AuxiliarySwitchBehavior.Momentary,
            AuxiliaryFunctionHazard.Safe, "Payload Control", false),
        new(31, "Motor emergency stop", "Emergency motor stop control.", "Emergency", AuxiliarySwitchBehavior.ThreePosition,
            AuxiliaryFunctionHazard.High, null, false),
        new(27, "Landing gear", "Set landing gear switch state.", "Vehicle", AuxiliarySwitchBehavior.ThreePosition,
            AuxiliaryFunctionHazard.Warning),
        new(65, "GPS disable", "Temporarily disable GPS use.", "Sensors", AuxiliarySwitchBehavior.ThreePosition,
            AuxiliaryFunctionHazard.High, null, false),
        new(300, "Lost vehicle sound", "Activate the configured lost-vehicle sound.", "Notification",
            AuxiliarySwitchBehavior.ThreePosition, AuxiliaryFunctionHazard.Safe)
    ];

    /// <inheritdoc />
    public IReadOnlyList<AuxiliaryFunctionDescriptor> GetFunctions(VehicleState vehicle) => functions;

    /// <inheritdoc />
    public AuxiliaryFunctionDescriptor DescribeUnknown(int id) => new(id, $"Function {id}",
        "This function is not in the reviewed catalog for this MissionPlanner version.", "Unknown",
        AuxiliarySwitchBehavior.ThreePosition, AuxiliaryFunctionHazard.High, null, false);
}

/// <summary>Conservative policy for generic auxiliary commands.</summary>
public sealed class AuxiliaryFunctionPolicy(IReplaySessionManager? replay = null) : IAuxiliaryFunctionPolicy
{
    /// <inheritdoc />
    public string? GetDenialReason(AuxiliaryFunctionRequest request)
    {
        if (replay is not null && replay.Snapshot.State != ReplaySessionState.Unloaded)
            return "Auxiliary functions are blocked during replay.";
        if (request.Vehicle.ConnectionState != VehicleConnectionState.Online) return "The vehicle is offline.";
        if (!request.Function.IsSupported) return request.Function.PreferredWorkflow is { } workflow
            ? $"Use {workflow} for this function." : "This function is intentionally unavailable in the generic workflow.";
        if (request.Function.Hazard != AuxiliaryFunctionHazard.Safe && !request.Confirmed)
            return "Explicit confirmation is required for this function.";
        return null;
    }
}

/// <summary>Sends <see cref="MavCmd.DoAuxFunction"/> through the shared command workflow.</summary>
public sealed class AuxiliaryFunctionService(IVehicleCommandService commands, IAuxiliaryFunctionPolicy policy)
    : IAuxiliaryFunctionService
{
    /// <inheritdoc />
    public async Task<AuxiliaryFunctionResult> ExecuteAsync(AuxiliaryFunctionRequest request, CancellationToken cancellationToken)
    {
        var denied = policy.GetDenialReason(request);
        if (denied is not null) return new(null, false, denied);
        var response = await commands.ExecuteExpertAsync(new ExpertVehicleCommand(request.Vehicle.VehicleId,
            (ushort)MavCmd.DoAuxFunction, [request.Function.Id, (float)request.Level, 0, 0, 0, 0, 0]), true, cancellationToken);
        return new(response, response.Result == VehicleCommandResult.Accepted,
            $"Command {response.Result}; the ACK does not confirm the resulting switch state.");
    }
}
