namespace MissionPlanner.Firmware.Model;

/// <summary>Identifies a firmware release stream.</summary>
public enum FirmwareReleaseChannel
{
    /// <summary>A tested production release.</summary>
    Stable,

    /// <summary>A prerelease candidate.</summary>
    Beta,

    /// <summary>The latest development build.</summary>
    Latest,

    /// <summary>An archived release.</summary>
    Historical,

    /// <summary>A user-supplied image.</summary>
    Custom
}
