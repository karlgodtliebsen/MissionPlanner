using MissionPlanner.MavLink.Parameters;

namespace MissionPlanner.Core.Setup.MandatoryHardware;

/// <summary>Projects one editable peripheral parameter.</summary>
/// <param name="Name">The parameter name.</param>
/// <param name="DisplayName">The user-facing name.</param>
/// <param name="CurrentValue">The current value.</param>
/// <param name="ParameterType">The parameter wire type.</param>
/// <param name="RebootRequired">Whether changing this parameter requires a reboot.</param>
/// <param name="Options">The metadata-supported values, empty for free numeric entry.</param>
/// <param name="IsSecret">Whether the value is sensitive and must not be logged.</param>
public sealed record PeripheralSetting(
    string Name,
    string DisplayName,
    double CurrentValue,
    MavParamType ParameterType,
    bool RebootRequired,
    IReadOnlyList<PeripheralSettingOption> Options,
    bool IsSecret = false)
{
    /// <summary>Gets explanatory firmware metadata.</summary>
    public string Description { get; init; } = string.Empty;
    /// <summary>Gets the measurement units.</summary>
    public string Units { get; init; } = string.Empty;
    /// <summary>Gets the recommended lower bound.</summary>
    public double? Minimum { get; init; }
    /// <summary>Gets the recommended upper bound.</summary>
    public double? Maximum { get; init; }
    /// <summary>Gets the recommended numeric increment.</summary>
    public double Increment { get; init; } = 1;
    /// <summary>Gets named bit positions.</summary>
    public IReadOnlyDictionary<int, string> Bits { get; init; } = new Dictionary<int, string>();
}
