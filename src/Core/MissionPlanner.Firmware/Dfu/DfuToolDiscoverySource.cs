namespace MissionPlanner.Firmware.Dfu;

/// <summary>Identifies the source of a CubeProgrammer executable candidate.</summary>
public enum DfuToolDiscoverySource
{
    /// <summary>The operator explicitly configured the path.</summary>
    UserConfigured,

    /// <summary>The path came from a known STM32CubeProgrammer installation directory.</summary>
    KnownInstallation,

    /// <summary>The path came from Windows uninstall registration.</summary>
    Registry,

    /// <summary>The executable was found through PATH.</summary>
    Path
}
