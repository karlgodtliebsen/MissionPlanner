namespace MissionPlanner.MavLink.MavFtp.Abstractions;

/// <summary>
/// Represents a client for interacting with a MAVLink FTP server.
/// </summary>
public interface IMavFtpClient : IAsyncDisposable
{
    /// <summary>Reads a bounded section and the current file size, without downloading earlier bytes.</summary>
    Task<(uint Size, byte[] Data)> ReadRangeAsync(MavFtpTarget target, string remotePath, uint offset, int maximumBytes, CancellationToken cancellationToken = default);

    /// <summary>Appends bounded data to an existing file using its reported size and confirmed write offsets.</summary>
    Task AppendAsync(MavFtpTarget target, string remotePath, ReadOnlyMemory<byte> data, CancellationToken cancellationToken = default);
    /// <summary>
    /// Resets all active sessions for the specified MAVLink FTP target.
    /// </summary>
    /// <param name="target">The MAVLink FTP target.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    Task ResetSessionsAsync(MavFtpTarget target, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets information about a file on the specified MAVLink FTP target.
    /// </summary>
    /// <param name="target">The MAVLink FTP target.</param>
    /// <param name="remotePath">The path to the remote file.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the file information.</returns>
    Task<MavFtpFileInfo> GetFileInfoAsync(MavFtpTarget target, string remotePath, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists the contents of a directory on the specified MAVLink FTP target.
    /// </summary>
    /// <param name="target">The MAVLink FTP target.</param>
    /// <param name="remotePath">The path to the remote directory.</param>
    /// <param name="progress"></param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the list of directory entries.</returns>
    Task<IReadOnlyList<MavFtpDirectoryEntry>> ListDirectoryAsync(MavFtpTarget target, string remotePath, IProgress<MavFtpProgress>? progress = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Downloads a file from the specified MAVLink FTP target.
    /// </summary>
    /// <param name="target">The MAVLink FTP target.</param>
    /// <param name="remotePath">The path to the remote file.</param>
    /// <param name="destination">The stream to which the file will be written.</param>
    /// <param name="progress">An optional progress reporter.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    Task DownloadFileAsync(MavFtpTarget target, string remotePath, Stream destination, IProgress<MavFtpProgress>? progress = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Downloads a file from the specified MAVLink FTP target and returns its contents as a byte array.
    /// </summary>
    /// <param name="target">The MAVLink FTP target.</param>
    /// <param name="remotePath">The path to the remote file.</param>
    /// <param name="progress">An optional progress reporter.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the file contents.</returns>
    Task<byte[]> DownloadFileAsync(MavFtpTarget target, string remotePath, IProgress<MavFtpProgress>? progress = null, CancellationToken cancellationToken = default);

    /// <summary>Creates and uploads a file; existing paths are rejected unless overwrite is explicitly requested. Interrupted transfers may leave a partial file.</summary>
    Task UploadFileAsync(MavFtpTarget target, string remotePath, Stream source, bool overwrite = false, IProgress<MavFtpProgress>? progress = null, CancellationToken cancellationToken = default);

    /// <summary>Removes a file or an empty directory after explicit operator selection.</summary>
    Task RemoveAsync(MavFtpTarget target, string remotePath, bool directory, CancellationToken cancellationToken = default);

    /// <summary>Creates a remote directory.</summary>
    Task CreateDirectoryAsync(MavFtpTarget target, string remotePath, CancellationToken cancellationToken = default);

    /// <summary>Renames a remote entry to a destination that must not already exist.</summary>
    Task RenameAsync(MavFtpTarget target, string remotePath, string destinationPath, CancellationToken cancellationToken = default);

    /// <summary>Reads the server-computed CRC-32 of a remote file.</summary>
    Task<uint> CalculateCrc32Async(MavFtpTarget target, string remotePath, CancellationToken cancellationToken = default);
}
