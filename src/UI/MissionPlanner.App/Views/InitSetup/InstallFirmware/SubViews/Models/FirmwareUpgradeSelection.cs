using MissionPlanner.Firmware.Model;

namespace MissionPlanner.App.Views.InitSetup.InstallFirmware.SubViews.Models;

/// <summary>Transfers an exact advisory release to the existing installation page without starting installation.</summary>
public sealed class FirmwareUpgradeSelection
{
    /// <summary>Gets or sets the release awaiting installation-page activation.</summary>
    public FirmwareManifestEntry? Pending
    {
        get; set;
    }

    /// <summary>Notifies the currently active installation page of a new advisory selection.</summary>
    public event Action<FirmwareManifestEntry>? Requested;

    /// <summary>Queues a release and updates the installer if it is already open.</summary>
    public void Request(FirmwareManifestEntry entry)
    {
        Pending = entry;
        Requested?.Invoke(entry);
    }
}
