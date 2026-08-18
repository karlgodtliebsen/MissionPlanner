namespace MissionPlanner.Core.Setup.MandatoryHardware;

/// <summary>Represents the severity of an optional-hardware configuration issue.</summary>
public enum PeripheralIssueSeverity
{
    /// <summary>Informational guidance.</summary>
    Info,

    /// <summary>A configuration that should be reviewed before flight.</summary>
    Warning,

    /// <summary>A configuration that must not be saved.</summary>
    Blocking
}
