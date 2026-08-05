namespace MissionPlanner.Simulation;

/// <summary>Identifies a supported, side-effect-free telemetry comparison.</summary>
public enum SimulationComparisonOperator
{
    /// <summary>Values are equal, within numeric tolerance where applicable.</summary>
    Equal,

    /// <summary>Values are not equal.</summary>
    NotEqual,

    /// <summary>The observed number is greater than the expected number.</summary>
    GreaterThan,

    /// <summary>The observed number is greater than or equal to the expected number.</summary>
    GreaterThanOrEqual,

    /// <summary>The observed number is less than the expected number.</summary>
    LessThan,

    /// <summary>The observed number is less than or equal to the expected number.</summary>
    LessThanOrEqual
}
