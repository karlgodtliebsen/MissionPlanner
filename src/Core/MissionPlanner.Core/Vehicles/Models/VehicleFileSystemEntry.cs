namespace MissionPlanner.Core.Vehicles.Models;

/// <summary>
/// Provides the public API for VehicleFileSystemEntryType.
/// </summary>
/// <summary>
/// Provides the public API for Directory.
/// </summary>
public enum VehicleFileSystemEntryType
{
    /// <summary>A file entry.</summary>
    File,
    /// <summary>A directory entry.</summary>
    Directory
}
/// <summary>
/// Provides the public API for VehicleFileSystemEntry.
/// </summary>
public sealed record VehicleFileSystemEntry(string Name, VehicleFileSystemEntryType Type, long? Size);
/// <summary>
/// Provides the public API for VehicleFileInfo.
/// </summary>
public sealed record VehicleFileInfo(string RemotePath, long Size);
/// <summary>
/// Provides the public API for VehicleFileTransferProgress.
/// </summary>
public sealed record VehicleFileTransferProgress(string RemotePath, long BytesTransferred, long? TotalBytes, double? BytesPerSecond);
