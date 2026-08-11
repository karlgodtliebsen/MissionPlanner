namespace MissionPlanner.Core.Vehicles.Models;

/// <summary>
/// Provides the public API for VehicleFileTransferProgress.
/// </summary>
/// <param name="RemotePath">The remote path of the directory being loaded.</param>
/// <param name="BytesTransferred">The number of bytes transferred so far.</param>
/// <param name="TotalBytes">The total number of bytes to transfer, if known.</param>
/// <param name="BytesPerSecond"></param>
public sealed record VehicleFileTransferProgress(string RemotePath, long BytesTransferred, long? TotalBytes, double? BytesPerSecond);
