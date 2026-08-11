using MissionPlanner.Core.Commands;

namespace MissionPlanner.Core.FlightData.Actuators;

/// <summary>Separates requested actuator commands from observed telemetry.</summary>
public sealed record ActuatorCommandResult(VehicleCommandResponse? Acknowledgement, bool ObservedConfirmed, string Summary);
