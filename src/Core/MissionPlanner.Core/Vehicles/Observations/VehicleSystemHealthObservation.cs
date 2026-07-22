using MissionPlanner.Core.Vehicles.Abstractions;

namespace MissionPlanner.Core.Vehicles.Observations;

/// <summary>Represents normalized controller and onboard-sensor health.</summary>
/// <param name="SensorsPresent">The available onboard sensor bitmap.</param>
/// <param name="SensorsEnabled">The enabled onboard sensor bitmap.</param>
/// <param name="SensorsHealthy">The healthy onboard sensor bitmap.</param>
/// <param name="ControllerLoadPercent">The controller load percentage.</param>
/// <param name="CommunicationDropRatePercent">The communication drop rate percentage.</param>
/// <param name="CommunicationErrors">The communication error count.</param>
/// <param name="ObservedAt">The reception timestamp.</param>
public sealed record VehicleSystemHealthObservation(uint SensorsPresent, uint SensorsEnabled, uint SensorsHealthy, double ControllerLoadPercent, double CommunicationDropRatePercent, ushort CommunicationErrors, DateTimeOffset ObservedAt) : IVehicleObservation;
