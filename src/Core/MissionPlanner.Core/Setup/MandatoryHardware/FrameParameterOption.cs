using MissionPlanner.Core.Vehicles.Models;

namespace MissionPlanner.Core.Setup.MandatoryHardware;

/// <summary>Describes one metadata-backed value offered for a frame parameter.</summary>
/// <param name="Value">The MAVLink parameter value.</param>
/// <param name="Label">The firmware metadata label.</param>
public sealed record FrameParameterOption(float Value, string Label);
