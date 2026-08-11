using MissionPlanner.Core.Commands;
using MissionPlanner.Core.Replay;
using MissionPlanner.Core.Vehicles.Models;
using MissionPlanner.MavLink.Generated;
using MissionPlanner.Shared.Models.Vehicles.Models;

namespace MissionPlanner.Core.FlightData.Actuators;

/// <summary>Safety-gated actuator command implementation.</summary>
public sealed class VehicleActuatorService(IVehicleCommandService commands, IReplaySessionManager? replay = null) : IVehicleActuatorService
{
    /// <inheritdoc />
    public Task<ActuatorCommandResult> SetServoAsync(VehicleState state, int channel, double pwm, bool confirmed, CancellationToken cancellationToken)
    {
        return Execute(state, (ushort)MavCmd.DoSetServo, channel is >= 1 and <= 32 && double.IsFinite(pwm) && pwm is >= 800 and <= 2200,
            [channel, (float)pwm, 0, 0, 0, 0, 0], confirmed, "Servo", cancellationToken);
    }

    /// <inheritdoc />
    public Task<ActuatorCommandResult> SetRelayAsync(VehicleState state, int relay, bool enabled, bool confirmed, CancellationToken cancellationToken)
    {
        return Execute(state, (ushort)MavCmd.DoSetRelay, relay is >= 0 and <= 15,
            [relay, enabled ? 1 : 0, 0, 0, 0, 0, 0], confirmed, "Relay", cancellationToken);
    }

    private async Task<ActuatorCommandResult> Execute(VehicleState state, ushort id, bool valid, IReadOnlyList<float> parameters, bool confirmed, string label, CancellationToken token)
    {
        if (!valid)
        {
            return new ActuatorCommandResult(null, false, $"{label} request is outside supported bounds.");
        }

        if (replay is not null && replay.Snapshot.State != ReplaySessionState.Unloaded)
        {
            return new ActuatorCommandResult(null, false, "Actuator writes are blocked during replay.");
        }

        if (state.IsArmed || !confirmed)
        {
            return new ActuatorCommandResult(null, false, "Disarm and explicitly confirm the actuator test.");
        }

        var ack = await commands.ExecuteExpertAsync(new ExpertVehicleCommand(state.VehicleId, id, parameters), true, token);
        return new ActuatorCommandResult(ack, false, $"Command {ack.Result}; observed state is not confirmed by the ACK.");
    }
}
