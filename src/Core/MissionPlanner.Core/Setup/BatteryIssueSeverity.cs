namespace MissionPlanner.Core.Setup;

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
