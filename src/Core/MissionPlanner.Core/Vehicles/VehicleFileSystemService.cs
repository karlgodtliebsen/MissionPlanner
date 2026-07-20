using Microsoft.Extensions.Logging;
using MissionPlanner.Core.Vehicles.Abstractions;
using MissionPlanner.Core.Vehicles.Models;
using MissionPlanner.MavLink.MavFtp;
using MissionPlanner.MavLink.MavFtp.Abstractions;

namespace MissionPlanner.Core.Vehicles;

/// <summary>
/// Service for interacting with the vehicle file system.
/// </summary>
/// <param name="client">The MAVFTP client.</param>
/// <param name="vehicleRegistry">The vehicle registry.</param>
/// <param name="logger">The logger.</param>
public sealed class VehicleFileSystemService(IMavFtpClient client, IVehicleRegistry vehicleRegistry, ILogger<VehicleFileSystemService> logger) : IVehicleFileSystemService
{
    /// <summary>
    /// Lists the contents of a directory on the vehicle's file system.
    /// </summary>
    /// <param name="vehicleId">The ID of the vehicle.</param>
    /// <param name="remotePath">The path of the directory to list.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A list of file system entries.</returns>
    public async Task<IReadOnlyList<VehicleFileSystemEntry>> ListDirectoryAsync(VehicleId vehicleId, string remotePath, CancellationToken cancellationToken = default)
    {
        var entries = await client.ListDirectoryAsync(Resolve(vehicleId), remotePath, cancellationToken).ConfigureAwait(false);
        return entries.Select(x => new VehicleFileSystemEntry(x.Name, x.Type == MavFtpDirectoryEntryType.Directory ? VehicleFileSystemEntryType.Directory : VehicleFileSystemEntryType.File, x.Size)).ToArray();
    }

    /// <inheritdoc />
    public async Task<VehicleFileInfo> GetFileInfoAsync(VehicleId vehicleId, string remotePath, CancellationToken cancellationToken = default)
    {
        var info = await client.GetFileInfoAsync(Resolve(vehicleId), remotePath, cancellationToken).ConfigureAwait(false);
        return new VehicleFileInfo(info.RemotePath, info.Size);
    }

    /// <inheritdoc />
    public Task DownloadFileAsync(VehicleId vehicleId, string remotePath, Stream destination, IProgress<VehicleFileTransferProgress>? progress = null, CancellationToken cancellationToken = default)
    {
        IProgress<MavFtpProgress>? mapped = progress is null
            ? null
            : new MappingProgress<MavFtpProgress, VehicleFileTransferProgress>(
                progress,
                static x => new VehicleFileTransferProgress(
                    x.RemotePath, x.BytesTransferred, x.TotalBytes, x.BytesPerSecond));
        return client.DownloadFileAsync(Resolve(vehicleId), remotePath, destination, mapped, cancellationToken);
    }

    /// <inheritdoc />
    public Task ResetSessionsAsync(VehicleId vehicleId, CancellationToken cancellationToken = default)
    {
        try
        {
            return client.ResetSessionsAsync(Resolve(vehicleId), cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "Non Critical Error while Executing ResetSessionsAsync for vehicle {VehicleId}", vehicleId);
        }

        return Task.CompletedTask;
    }

    private MavFtpTarget Resolve(VehicleId vehicleId)
    {
        var session = vehicleRegistry.GetRequired(vehicleId) ?? throw new InvalidOperationException($"Vehicle {vehicleId} is not connected.");
        return new MavFtpTarget(vehicleId.SystemId, vehicleId.ComponentId, session.EndPoint);
    }

    private sealed class MappingProgress<TSource, TTarget>(IProgress<TTarget> target, Func<TSource, TTarget> map) : IProgress<TSource>
    {
        public void Report(TSource value)
        {
            target.Report(map(value));
        }
    }

    /// <inheritdoc />
    public ValueTask DisposeAsync()
    {
        return client.DisposeAsync();
    }
}
