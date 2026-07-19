namespace MissionPlanner.MavLink.MavFtp.Abstractions;

/// <summary>
/// Represents a client for interacting with a MAVLink FTP server.
/// </summary>
public interface IMavFtpClient : IAsyncDisposable
{
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
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the list of directory entries.</returns>
    Task<IReadOnlyList<MavFtpDirectoryEntry>> ListDirectoryAsync(MavFtpTarget target, string remotePath, CancellationToken cancellationToken = default);

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
}
