using MissionPlanner.Core.ConfigTuning;
using MissionPlanner.Shared.Models.Vehicles.Models;

namespace MissionPlanner.Core.Setup.Arming;

/// <summary>Semantic pre-arm check selection; unknown is never disabled.</summary>
public enum PreArmCheckMode
{
    /// <summary>No confirmed configuration.</summary>
    Unknown,
    /// <summary>No configured checks.</summary>
    Disabled,
    /// <summary>Firmware's special All flag.</summary>
    All,
    /// <summary>A metadata-backed subset, not a safety guarantee.</summary>
    Custom
}

/// <summary>Firmware stick arming modes.</summary>
public enum StickArmingMode
{
    /// <summary>No stick arming.</summary>
    Disabled,
    /// <summary>Stick can arm only.</summary>
    ArmOnly,
    /// <summary>Stick can arm and disarm.</summary>
    ArmAndDisarm
}

/// <summary>Feature-specific settings shown by this page.</summary>
public enum ArmingSetting
{
    /// <summary>Pre-arm check selection.</summary>
    Checks,
    /// <summary>Stick arming method.</summary>
    Stick,
    /// <summary>Location requirement.</summary>
    Location,
    /// <summary>Firmware-specific arming requirement.</summary>
    Requirement
}

/// <summary>Confirmed or desired configuration; null values remain unknown. Switch zero means explicitly none.</summary>
public sealed record ArmingConfiguration(PreArmCheckMode Checks = PreArmCheckMode.Unknown, int CustomChecks = 0,
    StickArmingMode? Stick = null, bool? RequireLocation = null, int? Requirement = null, int? ArmSwitch = null)
{
    /// <summary>Gets an editor value without exposing mapping rules to the view.</summary>
    public double? Get(ArmingSetting setting) => setting switch
    {
        ArmingSetting.Checks => Checks switch { PreArmCheckMode.All => 1, PreArmCheckMode.Disabled => 0, PreArmCheckMode.Custom => CustomChecks, _ => null },
        ArmingSetting.Stick => Stick is { } value ? (int)value : null,
        ArmingSetting.Location => RequireLocation is { } enabled ? enabled ? 1 : 0 : null,
        ArmingSetting.Requirement => Requirement,
        _ => null
    };

    /// <summary>Gets the choice-editor value; Custom is a UI-only choice, never written as its sentinel.</summary>
    public double? EditorValue(ArmingSetting setting) => setting == ArmingSetting.Checks && Checks == PreArmCheckMode.Custom ? -1 : Get(setting);

    /// <summary>Stages a semantic editor choice while retaining custom bits.</summary>
    public ArmingConfiguration WithEditorValue(ArmingSetting setting, double value) => setting == ArmingSetting.Checks && value == -1
        ? this with { Checks = PreArmCheckMode.Custom } : With(setting, value);

    /// <summary>Stages a metadata-derived value; no transport operation is performed.</summary>
    public ArmingConfiguration With(ArmingSetting setting, double value) => setting switch
    {
        ArmingSetting.Checks => this with { Checks = value == 0 ? PreArmCheckMode.Disabled : ((int)value & 1) != 0 ? PreArmCheckMode.All : PreArmCheckMode.Custom, CustomChecks = value == 0 || ((int)value & 1) != 0 ? 0 : (int)value },
        ArmingSetting.Stick => this with { Stick = (StickArmingMode)(int)value },
        ArmingSetting.Location => this with { RequireLocation = value == 1 },
        ArmingSetting.Requirement => this with { Requirement = (int)value },
        _ => this
    };
}

/// <summary>One metadata-derived enum or bit choice.</summary>
public sealed record ArmingChoice(double Value, string Label);
/// <summary>Setting capability, current/default evidence and firmware choices.</summary>
public sealed record ArmingSettingDefinition(ArmingSetting Setting, string Label, string ParameterName,
    double? Current, double? Default, IReadOnlyList<ArmingChoice> Choices, IReadOnlyList<ArmingChoice> Bits,
    bool CanEdit, bool RequiresReboot, string? UnavailableReason);
/// <summary>Immutable configuration projection; live armed/readiness truth belongs to live diagnostics.</summary>
public sealed record ArmingSetupState(VehicleId VehicleId, ArmingConfiguration Current,
    IReadOnlyList<ArmingSettingDefinition> Settings, IReadOnlyList<int> ArmSwitches, bool AssignmentsKnown,
    bool IsSupported, bool ParametersReady, IReadOnlyDictionary<string, double> Evidence, string Status);
/// <summary>One explicitly reviewed write.</summary>
public sealed record ArmingParameterChange(string Name, double OldValue, double NewValue, bool RequiresReboot);
/// <summary>A review tied to one connection and firmware identity.</summary>
public sealed record ArmingChangeSet(ParameterEditScope Scope, CancellationToken Connection, ArmingConfiguration Original,
    ArmingConfiguration Desired, IReadOnlyList<ArmingParameterChange> Changes, IReadOnlyList<string> Errors,
    IReadOnlyList<string> Warnings)
{
    /// <summary>Whether all proposed changes are valid and nonempty.</summary>
    public bool CanApply => Changes.Count > 0 && Errors.Count == 0;
    /// <summary>Whether any reviewed change requires a reboot.</summary>
    public bool RequiresReboot => Changes.Any(c => c.RequiresReboot);
}
/// <summary>Actual readback and partial-write outcome.</summary>
public sealed record ArmingApplyResult(bool Success, ArmingSetupState? Actual, IReadOnlyList<string> Confirmed,
    bool RequiresReboot, string Message);
