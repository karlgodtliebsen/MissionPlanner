namespace MissionPlanner.Core.Firmware;

/// <summary>Identifies a firmware distribution channel.</summary>
public enum FirmwareReleaseChannel
{
    /// <summary>Stable production releases.</summary>
    Stable,
    /// <summary>Public beta releases.</summary>
    Beta,
    /// <summary>Development releases.</summary>
    Development
}
