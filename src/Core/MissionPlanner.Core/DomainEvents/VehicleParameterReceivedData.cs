using MissionPlanner.Core.Models;
using MissionPlanner.MavLink.Parameters;

namespace MissionPlanner.Core.DomainEvents;

/// <summary>
/// Data for the VehicleParameterReceived event.
/// </summary>
/// <param name="VehicleId">The ID of the vehicle.</param>
/// <param name="Parameter">The parameter that was received.</param>
public sealed record VehicleParameterReceivedData(VehicleId VehicleId, VehicleParameter Parameter);
