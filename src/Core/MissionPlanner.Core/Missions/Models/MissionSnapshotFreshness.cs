namespace MissionPlanner.Core.Missions.Models;

/// <summary>Describes whether a downloaded mission is proven to match the onboard mission.</summary>
public enum MissionSnapshotFreshness
{
    /// <summary>No mission has been downloaded from the vehicle.</summary>
    Missing,
    /// <summary>A mission was downloaded but cannot be matched to the vehicle's current mission.</summary>
    Unverified,
    /// <summary>The vehicle's mission has changed since the download.</summary>
    Stale,
    /// <summary>The vehicle reports the same mission that was downloaded.</summary>
    VerifiedCurrent
}
