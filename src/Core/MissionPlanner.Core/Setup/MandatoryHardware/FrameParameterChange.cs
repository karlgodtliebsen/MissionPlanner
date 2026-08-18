using MissionPlanner.MavLink.Parameters;

namespace MissionPlanner.Core.Setup.MandatoryHardware;

/// <summary>Represents one explicitly approved parameter change.</summary>
/// <param name="Name">The parameter name.</param>
/// <param name="OriginalValue">The value read before applying the change.</param>
/// <param name="PendingValue">The reviewed value to write.</param>
/// <param name="ParameterType">The MAVLink parameter type.</param>
public sealed record FrameParameterChange(string Name, float OriginalValue, float PendingValue, MavParamType ParameterType);
