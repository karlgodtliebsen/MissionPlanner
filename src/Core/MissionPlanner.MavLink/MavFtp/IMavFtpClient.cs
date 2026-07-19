namespace MissionPlanner.MavLink.MavFtp;

public interface IMavFtpClient
{
    Task ResetSessionsAsync(MavFtpTarget target, CancellationToken cancellationToken = default);
    Task<MavFtpFileInfo> GetFileInfoAsync(MavFtpTarget target, string remotePath, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<MavFtpDirectoryEntry>> ListDirectoryAsync(MavFtpTarget target, string remotePath, CancellationToken cancellationToken = default);
    Task DownloadFileAsync(MavFtpTarget target, string remotePath, Stream destination, IProgress<MavFtpProgress>? progress = null, CancellationToken cancellationToken = default);
    Task<byte[]> DownloadFileAsync(MavFtpTarget target, string remotePath, IProgress<MavFtpProgress>? progress = null, CancellationToken cancellationToken = default);
}
