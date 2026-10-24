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
    bool RequiresAcknowledgement = false)
{
    /// <summary>Gets additional conditions that must all match; each has independent units and freshness.</summary>
    public IReadOnlyList<WarningCondition> AndConditions { get; init; } = [];

    /// <summary>Gets whether activations are also delivered to the global notification history and banner.</summary>
    public bool NotifyGlobally { get; init; } = true;

    /// <summary>Compares rules structurally so persistence does not reset unchanged warning state.</summary>
    public bool Equals(WarningRule? other)
    {
        return other is not null && Id == other.Id && Name == other.Name && Enabled == other.Enabled &&
               Source == other.Source && Comparison == other.Comparison && Threshold.Equals(other.Threshold) &&
               UpperThreshold.Equals(other.UpperThreshold) && Severity == other.Severity && Message == other.Message &&
               DelaySeconds.Equals(other.DelaySeconds) && Hysteresis.Equals(other.Hysteresis) &&
               CooldownSeconds.Equals(other.CooldownSeconds) && RequiresAcknowledgement == other.RequiresAcknowledgement &&
               NotifyGlobally == other.NotifyGlobally &&
               (ReferenceEquals(AndConditions, other.AndConditions) ||
                AndConditions is not null && other.AndConditions is not null && AndConditions.SequenceEqual(other.AndConditions));
    }

    /// <inheritdoc />
    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(Id);
        hash.Add(Name);
        hash.Add(Enabled);
        hash.Add(Source);
        hash.Add(Comparison);
        hash.Add(Threshold);
        hash.Add(UpperThreshold);
        hash.Add(Severity);
        hash.Add(Message);
        hash.Add(DelaySeconds);
        hash.Add(Hysteresis);
        hash.Add(CooldownSeconds);
        hash.Add(RequiresAcknowledgement);
        hash.Add(NotifyGlobally);
        if (AndConditions is not null)
        {
            foreach (var condition in AndConditions)
            {
                hash.Add(condition);
            }
        }
        return hash.ToHashCode();
    }
}

/// <summary>One additional numeric condition in a warning's explicit AND expression.</summary>
public sealed record WarningCondition(string Source, WarningComparison Comparison, double Threshold,
    double UpperThreshold = 0, double Hysteresis = 0);

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
