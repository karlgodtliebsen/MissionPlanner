using MissionPlanner.MavLink.Parameters;

namespace MissionPlanner.Core.Setup.MandatoryHardware;

/// <summary>Describes one available frame-setting parameter and its current value.</summary>
/// <param name="Name">The parameter name.</param>
/// <param name="DisplayName">The user-facing parameter name.</param>
/// <param name="CurrentValue">The live value read from the vehicle.</param>
/// <param name="ParameterType">The MAVLink parameter type.</param>
/// <param name="RebootRequired">Whether firmware metadata requires a reboot.</param>
/// <param name="Options">The values explicitly advertised by firmware metadata.</param>
public sealed record FrameParameterSetting(
    string Name,
    string DisplayName,
    float CurrentValue,
    MavParamType ParameterType,
    bool RebootRequired,
    IReadOnlyList<FrameParameterOption> Options);
