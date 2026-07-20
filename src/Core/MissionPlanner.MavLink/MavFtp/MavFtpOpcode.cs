namespace MissionPlanner.MavLink.MavFtp;

/// <summary>
/// Provides the public API for MavFtpOpcode.
/// </summary>
public enum MavFtpOpcode : byte
{
    /// <summary>
    /// Provides the public API for ListDirectory.
    /// </summary>
    /// <summary>
    /// Provides the public API for ListDirectory.
    /// </summary>
    /// <summary>No operation.</summary>
    None = 0,
    /// <summary>Terminates a file session.</summary>
    TerminateSession = 1,
    /// <summary>Resets all file sessions.</summary>
    ResetSessions = 2,
    /// <summary>Lists a directory.</summary>
    ListDirectory = 3,
    /// <summary>
    /// Provides the public API for CreateFile.
    /// </summary>
    /// <summary>
    /// Provides the public API for CreateFile.
    /// </summary>
    /// <summary>Opens a file for reading.</summary>
    OpenFileReadOnly = 4,
    /// <summary>Reads file data.</summary>
    ReadFile = 5,
    /// <summary>Creates a file.</summary>
    CreateFile = 6,
    /// <summary>Writes file data.</summary>
    WriteFile = 7,
    /// <summary>
    /// Provides the public API for CreateDirectory.
    /// </summary>
    /// <summary>
    /// Provides the public API for CreateDirectory.
    /// </summary>
    /// <summary>Removes a file.</summary>
    RemoveFile = 8,
    /// <summary>Creates a directory.</summary>
    CreateDirectory = 9,
    /// <summary>Removes a directory.</summary>
    RemoveDirectory = 10,
    /// <summary>Opens a file for writing.</summary>
    OpenFileWriteOnly = 11,
    /// <summary>
    /// Provides the public API for BurstReadFile.
    /// </summary>
    /// <summary>
    /// Provides the public API for BurstReadFile.
    /// </summary>
    /// <summary>Truncates a file.</summary>
    TruncateFile = 12,
    /// <summary>Renames a file or directory.</summary>
    Rename = 13,
    /// <summary>Calculates a file CRC-32 value.</summary>
    CalculateFileCrc32 = 14,
    /// <summary>Reads file data in a burst.</summary>
    BurstReadFile = 15,
    /// <summary>
    /// Provides the public API for Ack.
    /// </summary>
    /// <summary>
    /// Provides the public API for Ack.
    /// </summary>
    /// <summary>Lists a directory with timestamps.</summary>
    ListDirectoryWithTime = 16,
    /// <summary>Acknowledges a request.</summary>
    Ack = 128,
    /// <summary>Rejects a request.</summary>
    Nak = 129
}
