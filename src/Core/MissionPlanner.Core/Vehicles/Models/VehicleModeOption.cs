using MissionPlanner.Shared.Models.Vehicles.Models;

namespace MissionPlanner.Core.Vehicles.Models;

/// <summary>
/// Describes a known mode in one ArduPilot firmware family; membership does not establish support in a particular firmware build.
/// </summary>
/// <param name="Name">The user-facing ArduPilot mode name.</param>
/// <param name="CustomMode">The firmware-specific custom-mode value.</param>
/// <param name="SemanticMode">The optional common semantic mode.</param>
public sealed record VehicleModeOption(string Name, uint CustomMode, VehicleMode SemanticMode = VehicleMode.Unknown);
