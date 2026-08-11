using MissionPlanner.MavLink.Generated;

namespace MissionPlanner.Core.FlightData.Components;

/// <summary>Uniquely identifies a peripheral component on a vehicle system.</summary>
public readonly record struct VehicleComponentKey(byte SystemId, byte ComponentId);
