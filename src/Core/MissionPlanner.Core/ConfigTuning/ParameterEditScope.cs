using MissionPlanner.Core.Vehicles.Models;

namespace MissionPlanner.Core.ConfigTuning;

/// <summary>Identifies the vehicle and firmware identity owned by an editing session.</summary>
/// <param name="VehicleId">The target vehicle.</param>
/// <param name="FirmwareIdentity">The firmware identity captured when the session was created.</param>
public sealed record ParameterEditScope(VehicleId VehicleId, VehicleFirmwareIdentity FirmwareIdentity);
