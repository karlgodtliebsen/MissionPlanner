namespace MissionPlanner.Core.Simulation;

/// <summary>Identifies current installation availability.</summary>
public enum SitlInstallationState
{
    /// <summary>The executable is present and compatible with this host.</summary>
    Available,

    /// <summary>The selected installation is absent.</summary>
    Missing,

    /// <summary>The installation exists but targets another host platform or architecture.</summary>
    Incompatible,

    /// <summary>The cached installation failed integrity or structure validation.</summary>
    Corrupt
}
