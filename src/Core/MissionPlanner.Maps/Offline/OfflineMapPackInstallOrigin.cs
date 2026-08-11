namespace MissionPlanner.Maps.Offline;

/// <summary>Identifies how an installed pack entered the repository.</summary>
public enum OfflineMapPackInstallOrigin
{
    /// <summary>An older manifest has no recorded provenance.</summary>
    LegacyUnknown,

    /// <summary>The operator imported a local archive.</summary>
    UserImported,

    /// <summary>The archive came from an approved signed feed.</summary>
    ApprovedFeed
}
