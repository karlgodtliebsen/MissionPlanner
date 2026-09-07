namespace MissionPlanner.Core.Setup.Advanced.Warnings;

/// <summary>Numeric comparisons supported by warning rules.</summary>
public enum WarningComparison
{
    /// <summary>Strictly below the threshold.</summary>
    Less,
    /// <summary>At or below the threshold.</summary>
    LessOrEqual,
    /// <summary>Exactly equal to the threshold.</summary>
    Equal,
    /// <summary>Different from the threshold.</summary>
    NotEqual,
    /// <summary>At or above the threshold.</summary>
    GreaterOrEqual,
    /// <summary>Strictly above the threshold.</summary>
    Greater,
    /// <summary>Within inclusive lower and upper bounds.</summary>
    InsideRange,
    /// <summary>Outside inclusive lower and upper bounds.</summary>
    OutsideRange
}

/// <summary>Visual urgency of an operator-defined warning.</summary>
public enum WarningSeverity
{
    /// <summary>Advisory information.</summary>
    Information,
    /// <summary>An operator should investigate.</summary>
    Warning,
    /// <summary>Immediate attention is requested.</summary>
    Critical
}

/// <summary>Serializable user rule. Values are expressed in the source catalogue's native units.</summary>
public sealed record WarningRule(
    Guid Id,
    string Name,
    bool Enabled,
    string Source,
    WarningComparison Comparison,
    double Threshold,
    double UpperThreshold,
    WarningSeverity Severity,
    string Message,
    double DelaySeconds = 0,
    double Hysteresis = 0,
    double CooldownSeconds = 30,
    bool RequiresAcknowledgement = false);

/// <summary>One sampled value; absent or stale values never qualify as threshold matches.</summary>
public sealed record WarningSample(double? Value, DateTimeOffset? ObservedAt);

/// <summary>Lifecycle state of one warning.</summary>
public enum WarningState
{
    /// <summary>No current threshold violation.</summary>
    Inactive,
    /// <summary>The activation delay has not elapsed.</summary>
    Pending,
    /// <summary>An active threshold violation.</summary>
    Active,
    /// <summary>The operator acknowledged the active violation.</summary>
    Acknowledged,
    /// <summary>A previous violation has cleared.</summary>
    Cleared,
    /// <summary>No recent valid source value is available.</summary>
    Unavailable,
    /// <summary>The rule is disabled.</summary>
    Disabled
}

/// <summary>Immutable result consumed by visual warning panels.</summary>
public sealed record WarningSnapshot(Guid RuleId, WarningState State, DateTimeOffset ChangedAt,
    double? Value, string Message, bool Notify);
