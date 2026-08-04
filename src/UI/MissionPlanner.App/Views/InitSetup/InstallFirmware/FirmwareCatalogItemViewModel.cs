using CommunityToolkit.Mvvm.ComponentModel;
using MissionPlanner.Firmware.Model;

namespace MissionPlanner.App.Views.InitSetup.InstallFirmware;

/// <summary>One data-driven firmware choice displayed by the install page.</summary>
public sealed partial class FirmwareCatalogItemViewModel(FirmwareManifestEntry entry) : ObservableObject
{
    /// <summary>Gets the normalized release.</summary>
    public FirmwareManifestEntry Entry { get; } = entry;

    /// <summary>Gets the vehicle label.</summary>
    public string VehicleType => Entry.Target.VehicleType.ToString();

    /// <summary>Gets the version label.</summary>
    public string Version => Entry.Version.ToString();

    /// <summary>Gets the platform label.</summary>
    public string Platform => Entry.Target.Platform;

    /// <summary>Gets the board identifier.</summary>
    public int BoardId => Entry.Target.BoardId;

    /// <summary>Gets the release channel.</summary>
    public FirmwareReleaseChannel Channel => Entry.Channel;
}
