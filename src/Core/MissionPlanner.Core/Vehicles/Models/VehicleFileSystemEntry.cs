namespace MissionPlanner.Core.Vehicles.Models;

public enum VehicleFileSystemEntryType { File, Directory }
public sealed record VehicleFileSystemEntry(string Name, VehicleFileSystemEntryType Type, long? Size);
public sealed record VehicleFileInfo(string RemotePath, long Size);
public sealed record VehicleFileTransferProgress(string RemotePath, long BytesTransferred, long? TotalBytes, double? BytesPerSecond);
