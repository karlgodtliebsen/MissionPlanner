namespace MissionPlanner.MavLink.MavFtp;

/// <summary>
/// Provides the public API for MavFtpNakError.
/// </summary>
public enum MavFtpNakError : byte
{
    /// <summary>
    /// Provides the public API for Failure.
    /// </summary>
    /// <summary>
    /// Provides the public API for Failure.
    /// </summary>
    /// <summary>No error.</summary>
    None = 0,
    /// <summary>A general failure.</summary>
    Failure = 1,
    /// <summary>A failure with an operating-system error number.</summary>
    FailureErrno = 2,
    /// <summary>The supplied data size is invalid.</summary>
    InvalidDataSize = 3,
    /// <summary>
    /// Provides the public API for EndOfFile.
    /// </summary>
    /// <summary>
    /// Provides the public API for EndOfFile.
    /// </summary>
    /// <summary>The session is invalid.</summary>
    InvalidSession = 4,
    /// <summary>No file sessions are available.</summary>
    NoSessionsAvailable = 5,
    /// <summary>The end of the file was reached.</summary>
    EndOfFile = 6,
    /// <summary>
    /// Provides the public API for FileExists.
    /// </summary>
    /// <summary>
    /// Provides the public API for FileExists.
    /// </summary>
    /// <summary>The command is not supported.</summary>
    UnknownCommand = 7,
    /// <summary>The file already exists.</summary>
    FileExists = 8,
    /// <summary>The file is protected.</summary>
    FileProtected = 9,
    /// <summary>The file was not found.</summary>
    FileNotFound = 10
}
