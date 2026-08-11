namespace MissionPlanner.Firmware.Dfu;

/// <summary>Describes STM32CubeProgrammer availability.</summary>
public enum DfuToolAvailability
{
    /// <summary>A validated supported tool is available.</summary>
    Available,

    /// <summary>No installation was discovered.</summary>
    NotInstalled,

    /// <summary>A configured or discovered path is invalid.</summary>
    PathInvalid,

    /// <summary>The discovered tool version is not supported.</summary>
    UnsupportedVersion,

    /// <summary>The host prevented validation or execution.</summary>
    ExecutionBlocked
}
