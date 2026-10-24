using MissionPlanner.Core.Vehicles.Models;
using MissionPlanner.Shared.Models.Vehicles.Models;

namespace MissionPlanner.Core.Vehicles.Abstractions;

/// <summary>
/// Service for interacting with the vehicle file system.
/// </summary>
public interface IVehicleFileSystemService : IAsyncDisposable
{
    /// <summary>Reads a bounded file range and reports its current size.</summary>
    Task<(uint Size, byte[] Data)> ReadRangeAsync(VehicleId vehicleId, string remotePath, uint offset, int maximumBytes, CancellationToken cancellationToken = default);

    /// <summary>Appends to an existing remote file with offset acknowledgement.</summary>
    Task AppendAsync(VehicleId vehicleId, string remotePath, ReadOnlyMemory<byte> data, CancellationToken cancellationToken = default);
    /// <summary>
    /// Lists the contents of a directory on the vehicle's file system.
    /// </summary>
    /// <param name="vehicleId">The ID of the vehicle.</param>
    /// <param name="remotePath">The path of the directory to list.</param>
    /// <param name="progress">An optional progress reporter.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A list of file system entries.</returns>
    Task<IReadOnlyList<VehicleFileSystemEntry>> ListDirectoryAsync(VehicleId vehicleId, string remotePath, IProgress<VehicleDirectoryProgress>? progress = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets information about a file on the vehicle's file system.
    /// </summary>
    /// <param name="vehicleId">The ID of the vehicle.</param>
    /// <param name="remotePath">The path of the file.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>Information about the file.</returns>
    Task<VehicleFileInfo> GetFileInfoAsync(VehicleId vehicleId, string remotePath, CancellationToken cancellationToken = default);

    /// <summary>
    /// Downloads a file from the vehicle's file system.
    /// </summary>
    /// <param name="vehicleId">The ID of the vehicle.</param>
    /// <param name="remotePath">The path of the file to download.</param>
    /// <param name="destination">The stream to write the file to.</param>
    /// <param name="progress">An optional progress reporter.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task DownloadFileAsync(VehicleId vehicleId, string remotePath, Stream destination, IProgress<VehicleFileTransferProgress>? progress = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Resets the file system sessions for a vehicle.
    /// </summary>
    /// <param name="vehicleId">The ID of the vehicle.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task ResetSessionsAsync(VehicleId vehicleId, CancellationToken cancellationToken = default);

    /// <summary>Creates and uploads a file; existing paths are rejected unless overwrite is explicitly requested. Interrupted transfers may leave a partial file.</summary>
    Task UploadFileAsync(VehicleId target, string remotePath, Stream source, bool overwrite = false, IProgress<VehicleFileTransferProgress>? progress = null, CancellationToken cancellationToken = default);

    /// <summary>Removes a file or an empty directory after explicit operator selection.</summary>
    Task RemoveAsync(VehicleId target, string remotePath, bool directory, CancellationToken cancellationToken = default);

    /// <summary>Creates a remote directory.</summary>
    Task CreateDirectoryAsync(VehicleId target, string remotePath, CancellationToken cancellationToken = default);

    /// <summary>Renames a remote entry to a destination that must not already exist.</summary>
    Task RenameAsync(VehicleId target, string remotePath, string destinationPath, CancellationToken cancellationToken = default);

    /// <summary>Reads the server-computed CRC-32 of a remote file.</summary>
    Task<uint> CalculateCrc32Async(VehicleId target, string remotePath, CancellationToken cancellationToken = default);
}
