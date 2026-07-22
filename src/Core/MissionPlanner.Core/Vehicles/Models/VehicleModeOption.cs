namespace MissionPlanner.Core.Vehicles.Models;

/// <summary>
/// Describes a flight mode supported by one ArduPilot firmware family.
/// </summary>
/// <param name="Name">The user-facing ArduPilot mode name.</param>
/// <param name="CustomMode">The firmware-specific custom-mode value.</param>
/// <param name="SemanticMode">The optional common semantic mode.</param>
public sealed record VehicleModeOption(string Name, uint CustomMode, VehicleMode SemanticMode = VehicleMode.Unknown);
