using MissionPlanner.Core.Vehicles.Models;

namespace MissionPlanner.Core.Vehicles.Abstractions;

/// <summary>
/// Service for interacting with the vehicle file system.
/// </summary>
public interface IVehicleFileSystemService
{
    /// <summary>
    /// Lists the contents of a directory on the vehicle's file system.
    /// </summary>
    /// <param name="vehicleId">The ID of the vehicle.</param>
    /// <param name="remotePath">The path of the directory to list.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A list of file system entries.</returns>
    Task<IReadOnlyList<VehicleFileSystemEntry>> ListDirectoryAsync(VehicleId vehicleId, string remotePath, CancellationToken cancellationToken = default);

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
}
