using MissionPlanner.Core.Vehicles.Models;

namespace MissionPlanner.Core.Setup.MandatoryHardware;

/// <summary>Represents one selectable servo output function.</summary>
/// <param name="Value">The stored function value.</param>
/// <param name="Name">The human-readable function name.</param>
public sealed record ServoFunctionOption(int Value, string Name);
