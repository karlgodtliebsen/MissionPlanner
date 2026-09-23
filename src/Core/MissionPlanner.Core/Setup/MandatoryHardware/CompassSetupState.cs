using MissionPlanner.Core.ConfigTuning;
using MissionPlanner.Shared.Models.Vehicles.Models;

namespace MissionPlanner.Core.Setup.MandatoryHardware;

/// <summary>Semantic compass controls, independent of firmware parameter names.</summary>
public enum CompassSetting
{
    /// <summary>Subsystem enable switch.</summary>
    Enabled,
    /// <summary>Primary navigation use.</summary>
    PrimaryUse,
    /// <summary>Secondary navigation use.</summary>
    SecondaryUse,
    /// <summary>Tertiary navigation use.</summary>
    TertiaryUse,
    /// <summary>First EKF yaw source set.</summary>
    YawSource,
    /// <summary>Primary mounting rotation.</summary>
    PrimaryOrientation,
    /// <summary>Secondary mounting rotation.</summary>
    SecondaryOrientation,
    /// <summary>Tertiary mounting rotation.</summary>
    TertiaryOrientation,
    /// <summary>Primary external sensor mode.</summary>
    PrimaryExternal,
    /// <summary>Secondary external sensor mode.</summary>
    SecondaryExternal,
    /// <summary>Tertiary external sensor mode.</summary>
    TertiaryExternal
}

/// <summary>ArduPilot EKF yaw choices; unrecognized values remain unknown capabilities.</summary>
public enum CompassYawSource
{
    /// <summary>No configured yaw observation.</summary>
    None = 0,
    /// <summary>Magnetic heading.</summary>
    Compass = 1,
    /// <summary>GPS yaw.</summary>
    Gps = 2,
    /// <summary>GPS yaw with compass fallback.</summary>
    GpsWithCompassFallback = 3,
    /// <summary>External navigation yaw.</summary>
    ExternalNavigation = 6,
    /// <summary>Gaussian sum filter yaw.</summary>
    GaussianSumFilter = 8
}

/// <summary>Semantic desired/current values. Null means absent or unknown, never a guessed default.</summary>
public sealed record CompassConfiguration
{
    /// <summary>Subsystem enabled state.</summary>
    public bool? Enabled { get; init; }
    /// <summary>Primary compass navigation use.</summary>
    public bool? UsePrimary { get; init; }
    /// <summary>Secondary compass navigation use.</summary>
    public bool? UseSecondary { get; init; }
    /// <summary>Tertiary compass navigation use.</summary>
    public bool? UseTertiary { get; init; }
    /// <summary>Selected first EKF yaw source.</summary>
    public CompassYawSource? YawSource { get; init; }
    /// <summary>Primary metadata rotation code.</summary>
    public int? PrimaryOrientation { get; init; }
    /// <summary>Secondary metadata rotation code.</summary>
    public int? SecondaryOrientation { get; init; }
    /// <summary>Tertiary metadata rotation code.</summary>
    public int? TertiaryOrientation { get; init; }
    /// <summary>Primary external sensor mode.</summary>
    public int? PrimaryExternal { get; init; }
    /// <summary>Secondary external sensor mode.</summary>
    public int? SecondaryExternal { get; init; }
    /// <summary>Tertiary external sensor mode.</summary>
    public int? TertiaryExternal { get; init; }

    /// <summary>Gets a semantic control value for a choice editor.</summary>
    public double? Get(CompassSetting setting) => setting switch
    {
        CompassSetting.Enabled => Boolean(Enabled),
        CompassSetting.PrimaryUse => Boolean(UsePrimary),
        CompassSetting.SecondaryUse => Boolean(UseSecondary),
        CompassSetting.TertiaryUse => Boolean(UseTertiary),
        CompassSetting.YawSource => YawSource is { } yaw ? (int)yaw : null,
        CompassSetting.PrimaryOrientation => PrimaryOrientation,
        CompassSetting.SecondaryOrientation => SecondaryOrientation,
        CompassSetting.TertiaryOrientation => TertiaryOrientation,
        CompassSetting.PrimaryExternal => PrimaryExternal,
        CompassSetting.SecondaryExternal => SecondaryExternal,
        CompassSetting.TertiaryExternal => TertiaryExternal,
        _ => null
    };

    /// <summary>Returns a new desired configuration after editing one semantic setting.</summary>
    public CompassConfiguration With(CompassSetting setting, double? value) => setting switch
    {
        CompassSetting.Enabled => this with { Enabled = Flag(value) },
        CompassSetting.PrimaryUse => this with { UsePrimary = Flag(value) },
        CompassSetting.SecondaryUse => this with { UseSecondary = Flag(value) },
        CompassSetting.TertiaryUse => this with { UseTertiary = Flag(value) },
        CompassSetting.YawSource => this with { YawSource = value is null ? null : (CompassYawSource)(int)value },
        CompassSetting.PrimaryOrientation => this with { PrimaryOrientation = (int?)value },
        CompassSetting.SecondaryOrientation => this with { SecondaryOrientation = (int?)value },
        CompassSetting.TertiaryOrientation => this with { TertiaryOrientation = (int?)value },
        CompassSetting.PrimaryExternal => this with { PrimaryExternal = (int?)value },
        CompassSetting.SecondaryExternal => this with { SecondaryExternal = (int?)value },
        CompassSetting.TertiaryExternal => this with { TertiaryExternal = (int?)value },
        _ => this
    };

    private static double? Boolean(bool? value) => value is null ? null : value.Value ? 1 : 0;
    private static bool? Flag(double? value) => value == 0 ? false : value == 1 ? true : null;
}

/// <summary>A supported semantic choice and its friendly label.</summary>
public sealed record CompassChoice(double Value, string Label);

/// <summary>A control's capability, default and secondary parameter diagnostics.</summary>
public sealed record CompassSettingDefinition(CompassSetting Setting, string Label, string Description,
    string ParameterName, double? Current, double? Default, IReadOnlyList<CompassChoice> Choices,
    bool CanEdit, bool RequiresReboot, string? UnavailableReason);

/// <summary>Read-only subsystem state projected from the authoritative registry and fresh telemetry.</summary>
public sealed record CompassSetupState(VehicleId VehicleId, CompassConfiguration Current,
    IReadOnlyList<CompassSettingDefinition> Settings, IReadOnlyList<string> DetectedDevices,
    string Health, string Validation, string ArmingImpact, bool IsSupported, string? UnsupportedReason,
    DateTimeOffset ObservedAt, IReadOnlyList<string> Diagnostics);

/// <summary>One explicit parameter mutation shown during review, including dependency reasoning.</summary>
public sealed record CompassParameterChange(string Name, double OldValue, double NewValue, bool RequiresReboot, string Reason);

/// <summary>Immutable vehicle/connection-scoped review, validated again immediately before writing.</summary>
public sealed record CompassChangeSet(ParameterEditScope Scope, CancellationToken Connection,
    CompassConfiguration Original, CompassConfiguration Desired, IReadOnlyList<CompassParameterChange> Changes,
    IReadOnlyList<string> Errors, IReadOnlyList<string> Warnings)
{
    /// <summary>Whether the reviewed changes may be applied.</summary>
    public bool CanApply => Errors.Count == 0 && Changes.Count > 0;
    /// <summary>Whether any proposed mutation requires reboot.</summary>
    public bool RequiresReboot => Changes.Any(change => change.RequiresReboot);
}

/// <summary>Actual post-write state and partial-failure evidence; success requires matching readback.</summary>
public sealed record CompassApplyResult(bool Success, CompassSetupState? Actual, bool RequiresReboot,
    IReadOnlyList<string> Confirmed, string Message);
