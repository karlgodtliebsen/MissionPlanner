namespace MissionPlanner.Core.ConfigTuning.Comparison;

/// <summary>Classifies one row in a parameter-source comparison.</summary>
public enum ParameterComparisonStatus
{
    /// <summary>Both values are equivalent.</summary>
    Equal,
    /// <summary>Both values exist and differ.</summary>
    Different,
    /// <summary>The name exists only on the left.</summary>
    OnlyOnLeft,
    /// <summary>The name exists only on the right.</summary>
    OnlyOnRight,
    /// <summary>The right-side text is not a finite number.</summary>
    InvalidRightValue,
    /// <summary>The difference targets a read-only parameter.</summary>
    ReadOnly,
    /// <summary>No target firmware metadata is available.</summary>
    MetadataMissing
}
