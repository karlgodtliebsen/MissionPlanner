namespace MissionPlanner.MavLink.MavFtp;

/// <summary>
/// Provides the public API for MavFtpDirectoryEntry.
/// </summary>
public sealed record MavFtpDirectoryEntry(string Name, MavFtpDirectoryEntryType Type, long? Size)
{
    /// <summary>Gets the remote modification time, or null when unavailable.</summary>
    public DateTimeOffset? ModifiedUtc { get; init; }
}
