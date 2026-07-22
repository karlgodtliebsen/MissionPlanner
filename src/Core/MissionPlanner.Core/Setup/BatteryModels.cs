using MissionPlanner.Core.Vehicles.Models;

namespace MissionPlanner.Core.Setup;

/// <summary>Identifies an editable battery-monitor setting independent of its parameter name.</summary>
public enum BatterySetting
{
    /// <summary>The battery monitor backend selector (BATT_MONITOR).</summary>
    Monitor,
    /// <summary>The pack capacity in milliampere-hours (BATT_CAPACITY).</summary>
    Capacity,
    /// <summary>The low-voltage failsafe threshold (BATT_LOW_VOLT).</summary>
    LowVoltage,
    /// <summary>The critical-voltage failsafe threshold (BATT_CRT_VOLT).</summary>
    CriticalVoltage,
    /// <summary>The low-capacity failsafe threshold (BATT_LOW_MAH).</summary>
    LowCapacity,
    /// <summary>The critical-capacity failsafe threshold (BATT_CRT_MAH).</summary>
    CriticalCapacity,
    /// <summary>The voltage multiplier (BATT_VOLT_MULT).</summary>
    VoltageMultiplier,
    /// <summary>The amperes-per-volt current scale (BATT_AMP_PERVLT).</summary>
    CurrentPerVolt,
    /// <summary>The current sensor offset (BATT_AMP_OFFSET).</summary>
    CurrentOffset,
    /// <summary>The low-battery failsafe action (BATT_FS_LOW_ACT).</summary>
    LowAction,
    /// <summary>The critical-battery failsafe action (BATT_FS_CRT_ACT).</summary>
    CriticalAction
}

/// <summary>Represents the severity of a battery configuration issue.</summary>
public enum BatteryIssueSeverity
{
    /// <summary>Informational guidance.</summary>
    Info,
    /// <summary>A configuration that should be reviewed before flight.</summary>
    Warning,
    /// <summary>A configuration that must not be saved.</summary>
    Blocking
}

/// <summary>Describes a discovered battery configuration issue.</summary>
/// <param name="Severity">The relative severity of the issue.</param>
/// <param name="Message">The user-facing explanation.</param>
public sealed record BatteryValidationIssue(BatteryIssueSeverity Severity, string Message);

/// <summary>Represents one selectable enumerated value for a battery setting.</summary>
/// <param name="Value">The stored numeric value.</param>
/// <param name="Name">The human-readable label.</param>
public sealed record BatterySettingOption(double Value, string Name);

/// <summary>Projects the live readings of one battery instance.</summary>
/// <param name="VoltageVolts">The live voltage, when reported.</param>
/// <param name="CurrentAmps">The live current, when reported.</param>
/// <param name="ConsumedMah">The consumed capacity, when reported.</param>
/// <param name="RemainingPercent">The estimated remaining percentage, when reported.</param>
/// <param name="IsStale">Whether the telemetry is older than the freshness window.</param>
/// <param name="HasTelemetry">Whether any live telemetry is available for this instance.</param>
public sealed record BatteryLiveReading(
    double? VoltageVolts,
    double? CurrentAmps,
    double? ConsumedMah,
    int? RemainingPercent,
    bool IsStale,
    bool HasTelemetry);

/// <summary>Describes one discovered battery monitor instance built from parameters and telemetry.</summary>
/// <param name="Index">The one-based battery instance index.</param>
/// <param name="MonitorType">The configured monitor backend value.</param>
/// <param name="MonitorName">The human-readable monitor backend name.</param>
/// <param name="Values">The available numeric settings keyed by kind.</param>
/// <param name="Live">The live readings for this instance.</param>
public sealed record BatteryMonitorInstance(
    int Index,
    int MonitorType,
    string MonitorName,
    IReadOnlyDictionary<BatterySetting, double> Values,
    BatteryLiveReading Live)
{
    /// <summary>Gets the stored value for a setting, when present.</summary>
    /// <param name="setting">The setting kind.</param>
    /// <returns>The stored value, or null when the parameter is absent.</returns>
    public double? Get(BatterySetting setting) => Values.TryGetValue(setting, out var value) ? value : null;
}

/// <summary>Represents the immutable battery configuration projected by the Setup UI.</summary>
/// <param name="VehicleId">The vehicle the configuration belongs to.</param>
/// <param name="Instances">The discovered battery instances in ascending order.</param>
/// <param name="MonitorOptions">The available monitor backends from metadata.</param>
/// <param name="LowActionOptions">The available low-failsafe actions from metadata.</param>
/// <param name="CriticalActionOptions">The available critical-failsafe actions from metadata.</param>
/// <param name="Issues">The detected configuration issues.</param>
public sealed record BatteryConfiguration(
    VehicleId VehicleId,
    IReadOnlyList<BatteryMonitorInstance> Instances,
    IReadOnlyList<BatterySettingOption> MonitorOptions,
    IReadOnlyList<BatterySettingOption> LowActionOptions,
    IReadOnlyList<BatterySettingOption> CriticalActionOptions,
    IReadOnlyList<BatteryValidationIssue> Issues)
{
    /// <summary>Creates an empty configuration for the specified vehicle.</summary>
    /// <param name="vehicleId">The vehicle identifier.</param>
    /// <returns>An empty configuration.</returns>
    public static BatteryConfiguration Empty(VehicleId vehicleId) => new(vehicleId, [], [], [], [], []);
}

/// <summary>Represents the outcome of a confirmed battery setting write.</summary>
/// <param name="Success">Whether the vehicle confirmed the new value by readback.</param>
/// <param name="Message">A user-facing explanation of the outcome.</param>
/// <param name="RequiresReboot">Whether the confirmed change requires a reboot.</param>
public sealed record BatteryApplyResult(bool Success, string Message, bool RequiresReboot = false);
