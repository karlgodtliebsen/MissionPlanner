using MissionPlanner.Core.Commands;
using MissionPlanner.Core.Vehicles;

namespace MissionPlanner.Core.FlightData.Preflight;

/// <summary>Describes an acknowledged pre-arm request and captured diagnostics.</summary>
public sealed record PreflightCommandResult(VehicleCommandResponse? Response, IReadOnlyList<VehicleStatusText> Diagnostics, string Summary);
