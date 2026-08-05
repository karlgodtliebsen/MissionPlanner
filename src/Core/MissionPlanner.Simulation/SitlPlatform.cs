namespace MissionPlanner.Simulation;

/// <summary>Identifies a supported SITL host platform.</summary>
public enum SitlPlatform
{
    /// <summary>Native Windows executable.</summary>
    Windows,

    /// <summary>Native Linux executable.</summary>
    Linux,

    /// <summary>Linux executable hosted by Windows Subsystem for Linux.</summary>
    WindowsSubsystemForLinux,

    /// <summary>Native macOS executable.</summary>
    MacOS
}
