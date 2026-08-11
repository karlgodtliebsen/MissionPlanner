namespace MissionPlanner.Core.Setup;

/// <summary>Represents the severity of a compass configuration issue.</summary>
public enum CompassIssueSeverity
{
    /// <summary>Informational guidance.</summary>
    Info,

    /// <summary>A configuration that should be reviewed before flight.</summary>
    Warning
}
