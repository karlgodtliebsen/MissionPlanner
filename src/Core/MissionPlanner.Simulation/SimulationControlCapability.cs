using MissionPlanner.Firmware;
using MissionPlanner.MavLink.Parameters;

namespace MissionPlanner.Simulation;

/// <summary>Describes runtime availability and current state of one logical control.</summary>
/// <param name="Descriptor">Control definition.</param>
/// <param name="IsAvailable">Whether a supported parameter exists on the exact simulator vehicle.</param>
/// <param name="ParameterName">Resolved firmware parameter name.</param>
/// <param name="ParameterType">Resolved MAVLink parameter type.</param>
/// <param name="CurrentValue">Last registry value.</param>
/// <param name="Reason">Availability explanation.</param>
/// <param name="FirmwareVersion">Observed vehicle firmware version.</param>
public sealed record SimulationControlCapability(
    SimulationControlDescriptor Descriptor,
    bool IsAvailable,
    string? ParameterName,
    MavParamType? ParameterType,
    double? CurrentValue,
    string Reason,
    FirmwareSemanticVersion? FirmwareVersion)
{
    /// <summary>Gets a concise selection label.</summary>
    public string DisplayName => IsAvailable
        ? $"{Descriptor.DisplayName} — {CurrentValue:0.###} {Descriptor.Unit}".TrimEnd()
        : $"{Descriptor.DisplayName} — unavailable";
}
