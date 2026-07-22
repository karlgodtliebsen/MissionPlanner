using MissionPlanner.Core.Vehicles.Models;
using MissionPlanner.MavLink.Parameters;

namespace MissionPlanner.Core.Setup;

/// <summary>Describes one metadata-backed value offered for a frame parameter.</summary>
/// <param name="Value">The MAVLink parameter value.</param>
/// <param name="Label">The firmware metadata label.</param>
public sealed record FrameParameterOption(float Value, string Label);

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

/// <summary>Contains the frame configuration supported by the connected firmware.</summary>
/// <param name="VehicleId">The vehicle from which the values were read.</param>
/// <param name="Family">The reported firmware family.</param>
/// <param name="Settings">Metadata-backed frame settings.</param>
/// <param name="Recommendations">Optional user-reviewed initial changes.</param>
public sealed record FrameConfigurationSnapshot(
    VehicleId VehicleId,
    FirmwareFamily Family,
    IReadOnlyList<FrameParameterSetting> Settings,
    IReadOnlyList<FrameInitialParameterRecommendation> Recommendations);

/// <summary>Represents one explicitly approved parameter change.</summary>
/// <param name="Name">The parameter name.</param>
/// <param name="OriginalValue">The value read before applying the change.</param>
/// <param name="PendingValue">The reviewed value to write.</param>
/// <param name="ParameterType">The MAVLink parameter type.</param>
public sealed record FrameParameterChange(string Name, float OriginalValue, float PendingValue, MavParamType ParameterType);

/// <summary>Identifies the outcome of a frame-configuration write operation.</summary>
public enum FrameConfigurationApplyStatus
{
    /// <summary>Every requested value was confirmed by readback.</summary>
    Succeeded,
    /// <summary>No requested value was changed.</summary>
    NoChanges,
    /// <summary>A failure occurred and every confirmed write was rolled back.</summary>
    RolledBack,
    /// <summary>Some values may remain changed and require manual review.</summary>
    PartialFailure,
    /// <summary>The operation was cancelled before completion.</summary>
    Cancelled,
    /// <summary>The request was invalid or could not start.</summary>
    Failed
}

/// <summary>Reports confirmed writes and any recovery guidance after frame configuration.</summary>
/// <param name="Status">The terminal operation status.</param>
/// <param name="Message">The user-facing result.</param>
/// <param name="ConfirmedParameters">Parameters confirmed at their requested values.</param>
/// <param name="RollbackFailedParameters">Parameters that could not be confirmed at their original values.</param>
/// <param name="RequiresReboot">Whether any confirmed setting is metadata-marked as requiring reboot.</param>
public sealed record FrameConfigurationApplyResult(
    FrameConfigurationApplyStatus Status,
    string Message,
    IReadOnlyList<string> ConfirmedParameters,
    IReadOnlyList<string> RollbackFailedParameters,
    bool RequiresReboot)
{
    /// <summary>Gets whether all requested values were confirmed.</summary>
    public bool Succeeded => Status is FrameConfigurationApplyStatus.Succeeded or FrameConfigurationApplyStatus.NoChanges;
}
