using MissionPlanner.Core.Vehicles.Models;
using MissionPlanner.MavLink.Parameters;

namespace MissionPlanner.Core.Simulation;

/// <summary>Identifies a simulation control category.</summary>
public enum SimulationControlCategory
{
    /// <summary>Weather or physical environment value.</summary>
    Environment,

    /// <summary>Simulated sensor state or reading.</summary>
    Sensor,

    /// <summary>Hazardous bounded failure injection.</summary>
    Fault
}

/// <summary>Identifies a simulation scenario event result.</summary>
public enum SimulationScenarioEventResult
{
    /// <summary>A requested value was confirmed.</summary>
    Applied,

    /// <summary>A control was explicitly reset.</summary>
    Reset,

    /// <summary>A hazardous control reached its duration and reset automatically.</summary>
    AutoReset,

    /// <summary>An operation failed or could not confirm readback.</summary>
    Failed
}

/// <summary>Maps a logical simulation control to one firmware parameter variant.</summary>
/// <param name="Name">Exact parameter name.</param>
/// <param name="ActiveValue">Fixed active value for a fault; <see langword="null"/> uses the requested value.</param>
/// <param name="ResetValue">Fixed safe reset value; <see langword="null"/> restores the captured original.</param>
public sealed record SimulationParameterBinding(
    string Name,
    double? ActiveValue = null,
    double? ResetValue = null);

/// <summary>Describes one documented simulation control and its safety bounds.</summary>
/// <param name="Key">Stable control key.</param>
/// <param name="DisplayName">User-facing name.</param>
/// <param name="Description">Behavior and safety detail.</param>
/// <param name="Category">Control category.</param>
/// <param name="Unit">Display unit.</param>
/// <param name="Minimum">Minimum requested value.</param>
/// <param name="Maximum">Maximum requested value.</param>
/// <param name="RequiresConfirmation">Whether explicit hazardous-action confirmation is required.</param>
/// <param name="MaximumDuration">Maximum active duration before automatic reset.</param>
/// <param name="ParameterBindings">Ordered firmware parameter variants selected by live presence.</param>
/// <param name="SupportedFamilies">Supported firmware families.</param>
/// <param name="DocumentationUri">Official source documenting the control.</param>
public sealed record SimulationControlDescriptor(
    string Key,
    string DisplayName,
    string Description,
    SimulationControlCategory Category,
    string Unit,
    double Minimum,
    double Maximum,
    bool RequiresConfirmation,
    TimeSpan? MaximumDuration,
    IReadOnlyList<SimulationParameterBinding> ParameterBindings,
    IReadOnlySet<FirmwareFamily> SupportedFamilies,
    Uri DocumentationUri);

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

/// <summary>Records one auditable control event against simulation time and vehicle identity.</summary>
/// <param name="Timestamp">Wall-clock event time.</param>
/// <param name="SimulationTime">Elapsed simulation session time.</param>
/// <param name="SessionId">Simulation session identity.</param>
/// <param name="VehicleId">Exact target vehicle.</param>
/// <param name="ControlKey">Logical control key.</param>
/// <param name="ParameterName">Resolved parameter name.</param>
/// <param name="RequestedValue">Requested or reset value.</param>
/// <param name="Result">Operation result.</param>
/// <param name="Message">Result detail.</param>
public sealed record SimulationScenarioEvent(
    DateTimeOffset Timestamp,
    TimeSpan SimulationTime,
    Guid SessionId,
    VehicleId VehicleId,
    string ControlKey,
    string ParameterName,
    double RequestedValue,
    SimulationScenarioEventResult Result,
    string Message);

/// <summary>Describes a named launch-location preset.</summary>
/// <param name="Key">Stable preset key.</param>
/// <param name="Name">User-facing name.</param>
/// <param name="Location">Typed start location.</param>
public sealed record SimulationLocationPreset(
    string Key,
    string Name,
    SimulationLocation Location);

/// <summary>Stores one requested control value in a reusable preset.</summary>
/// <param name="ControlKey">Logical control key.</param>
/// <param name="Value">Requested value.</param>
/// <param name="Duration">Bounded duration for hazardous controls.</param>
public sealed record SimulationPresetControlValue(
    string ControlKey,
    double Value,
    TimeSpan? Duration);

/// <summary>Defines a reusable environment/fault preset separate from a launch profile.</summary>
/// <param name="Id">Stable preset identity.</param>
/// <param name="Name">User-facing preset name.</param>
/// <param name="Location">Optional launch location.</param>
/// <param name="Controls">Requested environment and fault values.</param>
public sealed record SimulationScenarioPreset(
    Guid Id,
    string Name,
    SimulationLocation? Location,
    IReadOnlyList<SimulationPresetControlValue> Controls);
