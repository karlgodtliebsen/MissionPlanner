using MissionPlanner.MavLink.Parameters;

namespace MissionPlanner.Core.Setup;

/// <summary>Represents the severity of an optional-hardware configuration issue.</summary>
public enum PeripheralIssueSeverity
{
    /// <summary>Informational guidance.</summary>
    Info,
    /// <summary>A configuration that should be reviewed before flight.</summary>
    Warning,
    /// <summary>A configuration that must not be saved.</summary>
    Blocking
}

/// <summary>Describes an optional-hardware configuration issue.</summary>
/// <param name="Severity">The relative severity of the issue.</param>
/// <param name="Message">The user-facing explanation.</param>
public sealed record PeripheralValidationIssue(PeripheralIssueSeverity Severity, string Message);

/// <summary>Represents one selectable enumerated value for a peripheral setting.</summary>
/// <param name="Value">The stored numeric value.</param>
/// <param name="Name">The human-readable label.</param>
public sealed record PeripheralSettingOption(double Value, string Name);

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
    bool IsSecret = false);

/// <summary>Represents one available optional-hardware module projection.</summary>
/// <param name="Key">The stable module key.</param>
/// <param name="Title">The module title.</param>
/// <param name="Description">The module description.</param>
/// <param name="Settings">The editable settings.</param>
/// <param name="Issues">The detected configuration issues.</param>
/// <param name="LiveStatus">An optional live status line.</param>
public sealed record OptionalHardwareModuleView(
    string Key,
    string Title,
    string Description,
    IReadOnlyList<PeripheralSetting> Settings,
    IReadOnlyList<PeripheralValidationIssue> Issues,
    string? LiveStatus);

/// <summary>Represents the outcome of a confirmed peripheral setting write.</summary>
/// <param name="Success">Whether the vehicle confirmed the new value by readback.</param>
/// <param name="Message">A user-facing explanation of the outcome.</param>
/// <param name="RequiresReboot">Whether the confirmed change requires a reboot.</param>
public sealed record OptionalHardwareApplyResult(bool Success, string Message, bool RequiresReboot = false);

/// <summary>
/// Defines one optional-hardware setup module. Modules are discovered from parameter presence so
/// peripherals can be added without modifying a central switch.
/// </summary>
public interface IOptionalHardwareModule
{
    /// <summary>Gets the stable module key.</summary>
    string Key { get; }

    /// <summary>Gets the module title.</summary>
    string Title { get; }

    /// <summary>Determines whether the module applies to the connected vehicle's parameters.</summary>
    /// <param name="parameters">The live parameter set.</param>
    /// <returns><see langword="true"/> when the peripheral parameters are present.</returns>
    bool IsAvailable(IReadOnlyDictionary<string, VehicleParameter> parameters);

    /// <summary>Builds the module projection from live parameters and metadata.</summary>
    /// <param name="parameters">The live parameter set.</param>
    /// <param name="metadata">The firmware parameter metadata.</param>
    /// <returns>The module projection.</returns>
    OptionalHardwareModuleView Build(
        IReadOnlyDictionary<string, VehicleParameter> parameters,
        IReadOnlyDictionary<string, ParameterMetadata> metadata);
}

/// <summary>Provides shared helpers for building optional-hardware settings from metadata.</summary>
public static class PeripheralSettingFactory
{
    /// <summary>Builds a setting when the parameter is present, applying metadata options and reboot flags.</summary>
    /// <param name="name">The parameter name.</param>
    /// <param name="parameters">The live parameter set.</param>
    /// <param name="metadata">The firmware parameter metadata.</param>
    /// <param name="isSecret">Whether the value is sensitive.</param>
    /// <returns>The setting, or null when the parameter is absent.</returns>
    public static PeripheralSetting? TryBuild(
        string name,
        IReadOnlyDictionary<string, VehicleParameter> parameters,
        IReadOnlyDictionary<string, ParameterMetadata> metadata,
        bool isSecret = false)
    {
        if (!parameters.TryGetValue(name, out var parameter))
        {
            return null;
        }

        metadata.TryGetValue(name, out var definition);
        var options = definition?.GetValueOptions()
            .OrderBy(option => option.Key)
            .Select(option => new PeripheralSettingOption(option.Key, option.Value))
            .ToArray() ?? [];
        return new PeripheralSetting(
            name,
            definition?.DisplayName ?? name,
            parameter.Value,
            parameter.Type,
            definition?.RebootRequired ?? false,
            options,
            isSecret);
    }
}
