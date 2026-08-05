namespace MissionPlanner.Simulation;

/// <summary>Identifies who owns a SITL installation.</summary>
public enum SitlInstallationSource
{
    /// <summary>User-selected external installation that MissionPlanner must never remove.</summary>
    External,

    /// <summary>Verified versioned cache owned by MissionPlanner.</summary>
    VerifiedCache
}
