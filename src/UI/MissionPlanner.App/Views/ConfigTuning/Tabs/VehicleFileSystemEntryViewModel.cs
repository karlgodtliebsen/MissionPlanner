using MissionPlanner.Core.Vehicles.Models;

namespace MissionPlanner.App.Views.ConfigTuning.Tabs;

public sealed record VehicleFileSystemEntryViewModel(string Name, VehicleFileSystemEntryType Type, long? Size)
{
    public bool IsDirectory => Type == VehicleFileSystemEntryType.Directory;
    public string Icon => IsDirectory ? "📁" : "📄";
    public string TypeText => IsDirectory ? "Directory" : "File";
    public string SizeText => Size.HasValue ? $"{Size.Value:N0} bytes" : string.Empty;
}
