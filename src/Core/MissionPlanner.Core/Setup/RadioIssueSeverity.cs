namespace MissionPlanner.Core.Setup;

/// <summary>Represents the severity of an RC configuration issue.</summary>
public enum RadioIssueSeverity
{
    /// <summary>Informational guidance.</summary>
    Info,

    /// <summary>A configuration that should be reviewed before flight.</summary>
    Warning,

    /// <summary>A hazardous configuration that blocks a confirmed write.</summary>
    Hazard
}
