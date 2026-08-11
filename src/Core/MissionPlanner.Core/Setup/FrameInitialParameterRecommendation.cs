using MissionPlanner.MavLink.Parameters;

namespace MissionPlanner.Core.Setup;

/// <summary>Describes an optional, user-reviewed initial parameter recommendation.</summary>
/// <param name="Name">The parameter name.</param>
/// <param name="DisplayName">The user-facing parameter name.</param>
/// <param name="CurrentValue">The live value read from the vehicle.</param>
/// <param name="RecommendedValue">The proposed value.</param>
/// <param name="ParameterType">The MAVLink parameter type.</param>
/// <param name="Reason">Why the value is recommended.</param>
/// <param name="RebootRequired">Whether firmware metadata requires a reboot.</param>
public sealed record FrameInitialParameterRecommendation(
    string Name,
    string DisplayName,
    float CurrentValue,
    float RecommendedValue,
    MavParamType ParameterType,
    string Reason,
    bool RebootRequired);
