using MissionPlanner.Firmware.Model;

namespace MissionPlanner.App.Views.InitSetup.InstallFirmware;

/// <summary>Transfers an exact advisory release to the existing installation page without starting installation.</summary>
public sealed class FirmwareUpgradeSelection
{
    /// <summary>Gets or sets the release awaiting installation-page activation.</summary>
    public FirmwareManifestEntry? Pending { get; set; }
}
