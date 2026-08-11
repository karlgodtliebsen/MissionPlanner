using MissionPlanner.Core.FlightData.Components;
using MissionPlanner.Core.Simulation.Abstractions;

namespace MissionPlanner.Core.FlightData.Payload;

/// <summary>Identifies one selected payload component.</summary>
public sealed record PayloadComponentSelection(VehicleComponentKey Key, string Kind, DateTimeOffset LastSeen);
