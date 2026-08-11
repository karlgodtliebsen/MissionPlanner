using MissionPlanner.Core.Vehicles.Models;

namespace MissionPlanner.Core.FlightData.Actuators;

/// <summary>Executes bounded typed servo and relay commands.</summary>
public interface IVehicleActuatorService
{
    /// <summary>Sets one non-motor servo output in microseconds.</summary>
    Task<ActuatorCommandResult> SetServoAsync(VehicleState state, int channel, double pwm, bool confirmed, CancellationToken cancellationToken);

    /// <summary>Sets one relay output.</summary>
    Task<ActuatorCommandResult> SetRelayAsync(VehicleState state, int relay, bool enabled, bool confirmed, CancellationToken cancellationToken);
}
