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
