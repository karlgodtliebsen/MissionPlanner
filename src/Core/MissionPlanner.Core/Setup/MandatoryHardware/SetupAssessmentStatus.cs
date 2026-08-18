namespace MissionPlanner.Core.Setup.MandatoryHardware;

/// <summary>Represents the assessed status of a safety check or summary entry.</summary>
public enum SetupAssessmentStatus
{
    /// <summary>The item is configured as expected.</summary>
    Pass,

    /// <summary>The item is configured in a way that should be reviewed.</summary>
    Warning,

    /// <summary>The item is available but not configured.</summary>
    NotConfigured,

    /// <summary>The item is not supported by this firmware or vehicle.</summary>
    Unsupported,

    /// <summary>The item could not be assessed from available evidence.</summary>
    NotAssessed
}
