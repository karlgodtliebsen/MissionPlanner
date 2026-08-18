namespace MissionPlanner.Core.Setup.MandatoryHardware;

/// <summary>Describes the operational presentation of an RC input channel.</summary>
public enum RadioChannelKind
{
    /// <summary>A centered pilot axis.</summary>
    CenteredAxis,

    /// <summary>The mapped pilot throttle.</summary>
    Throttle,

    /// <summary>An unmapped or auxiliary continuous input.</summary>
    Auxiliary
}
