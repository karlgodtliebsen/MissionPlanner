namespace MissionPlanner.Core.ConfigTuning.Tuning;

/// <summary>Identifies a supported coupled validation relationship.</summary>
public enum BasicTuningRuleKind
{
    /// <summary>The first value must be less than or equal to the second value.</summary>
    LessThanOrEqual,

    /// <summary>A positive first value requires a positive companion value.</summary>
    PositiveCompanion
}
