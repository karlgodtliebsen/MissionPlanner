namespace MissionPlanner.MavLink.MavFtp;

/// <summary>
/// Provides the public API for MavFtpDirectoryEntryType.
/// </summary>
/// <summary>
/// Provides the public API for Directory.
/// </summary>
public enum MavFtpDirectoryEntryType
{
    /// <summary>A file entry.</summary>
    File,
    /// <summary>A directory entry.</summary>
    Directory,
    /// <summary>An entry that should be skipped.</summary>
    Skip
}
