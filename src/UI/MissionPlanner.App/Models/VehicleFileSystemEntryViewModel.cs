using CommunityToolkit.Mvvm.ComponentModel;
using MissionPlanner.Core.Vehicles.Models;

namespace MissionPlanner.App.Models;

/// <summary>
/// Provides the public API for VehicleFileSystemEntryViewModel.
/// </summary>
public sealed partial class VehicleFileSystemEntryViewModel(string name, VehicleFileSystemEntryType type, long? size) : ObservableObject
{
    /// <summary>
    /// Gets the name of the file system entry.
    /// </summary>
    public string Name { get; } = name;

    /// <summary>
    /// Gets the type of the file system entry.
    /// </summary>
    public VehicleFileSystemEntryType Type { get; } = type;

    /// <summary>
    /// Gets the size of the file system entry.
    /// </summary>
    public long? Size { get; } = size;

    [ObservableProperty]
    public partial bool IsSelected
    {
        get; set;
    }

    /// <summary>
    /// Provides the public API for IsDirectory.
    /// </summary>
    public bool IsDirectory => Type == VehicleFileSystemEntryType.Directory;

    /// <summary>
    /// Provides the public API for Icon.
    /// </summary>
    public string Icon => IsDirectory ? "📁" : "📄";

    /// <summary>
    /// Provides the public API for TypeText.
    /// </summary>
    public string TypeText => IsDirectory ? "Directory" : "File";

    /// <summary>
    /// Provides the public API for SizeText.
    /// </summary>
    public string SizeText => Size.HasValue ? $"{Size.Value:N0} bytes" : string.Empty;
}

