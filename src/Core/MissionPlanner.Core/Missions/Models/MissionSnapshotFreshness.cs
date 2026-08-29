namespace MissionPlanner.Core.Missions.Models;

/// <summary>Describes whether a downloaded mission is proven to match the onboard mission.</summary>
public enum MissionSnapshotFreshness
{
    Missing,
    Unverified,
    Stale,
    VerifiedCurrent
}
