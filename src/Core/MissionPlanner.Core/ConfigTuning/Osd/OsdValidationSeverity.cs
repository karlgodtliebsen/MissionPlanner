namespace MissionPlanner.Core.ConfigTuning.Osd;

/// <summary>Identifies the severity of an OSD layout validation result.</summary>
public enum OsdValidationSeverity
{
    /// <summary>The layout cannot be applied.</summary>
    Error,

    /// <summary>The layout can be applied only after explicit acknowledgement.</summary>
    Warning
}
