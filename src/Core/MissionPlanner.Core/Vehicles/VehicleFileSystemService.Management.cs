using MissionPlanner.Core.Vehicles.Models;
using MissionPlanner.MavLink.MavFtp;
using MissionPlanner.Shared.Models.Vehicles.Models;

namespace MissionPlanner.Core.Vehicles;

public sealed partial class VehicleFileSystemService
{
    /// <inheritdoc />
    public Task<(uint Size, byte[] Data)> ReadRangeAsync(VehicleId vehicleId, string remotePath, uint offset, int maximumBytes, CancellationToken cancellationToken = default) =>
        client.ReadRangeAsync(Resolve(vehicleId), remotePath, offset, maximumBytes, cancellationToken);

    /// <inheritdoc />
    public Task AppendAsync(VehicleId vehicleId, string remotePath, ReadOnlyMemory<byte> data, CancellationToken cancellationToken = default) =>
        client.AppendAsync(Resolve(vehicleId), remotePath, data, cancellationToken);
    /// <inheritdoc />
    public Task UploadFileAsync(VehicleId target, string remotePath, Stream source, bool overwrite = false,
        IProgress<VehicleFileTransferProgress>? progress = null, CancellationToken cancellationToken = default)
    {
        IProgress<MavFtpProgress>? mapped = progress is null ? null :
            new MappingProgress<MavFtpProgress, VehicleFileTransferProgress>(progress,
                value => new(value.RemotePath, value.BytesTransferred, value.TotalBytes, value.BytesPerSecond));
        return client.UploadFileAsync(Resolve(target), remotePath, source, overwrite, mapped, cancellationToken);
    }

    /// <inheritdoc />
    public Task RemoveAsync(VehicleId target, string remotePath, bool directory, CancellationToken cancellationToken = default)
    {
        return client.RemoveAsync(Resolve(target), remotePath, directory, cancellationToken);
    }

    /// <inheritdoc />
    public Task CreateDirectoryAsync(VehicleId target, string remotePath, CancellationToken cancellationToken = default)
    {
        return client.CreateDirectoryAsync(Resolve(target), remotePath, cancellationToken);
    }

    /// <inheritdoc />
    public Task RenameAsync(VehicleId target, string remotePath, string destinationPath, CancellationToken cancellationToken = default)
    {
        return client.RenameAsync(Resolve(target), remotePath, destinationPath, cancellationToken);
    }

    /// <inheritdoc />
    public Task<uint> CalculateCrc32Async(VehicleId target, string remotePath, CancellationToken cancellationToken = default)
    {
        return client.CalculateCrc32Async(Resolve(target), remotePath, cancellationToken);
    }
}
