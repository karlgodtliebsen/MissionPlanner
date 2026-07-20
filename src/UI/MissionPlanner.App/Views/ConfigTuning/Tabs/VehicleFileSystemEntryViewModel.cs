using MissionPlanner.Core.Vehicles.Models;

namespace MissionPlanner.App.Views.ConfigTuning.Tabs;

/// <summary>
/// Provides the public API for VehicleFileSystemEntryViewModel.
/// </summary>
public sealed record VehicleFileSystemEntryViewModel(string Name, VehicleFileSystemEntryType Type, long? Size)
{
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
