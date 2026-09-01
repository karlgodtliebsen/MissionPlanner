using MissionPlanner.Firmware.Model;

namespace MissionPlanner.AvaloniaUI.App.Views.InitSetup.InstallFirmware;

/// <summary>Captures presentation evidence used to choose concise contextual guidance.</summary>
public sealed record FirmwareSupportContext(
    bool DfuDevicePresent = false,
    bool CubeProgrammerAvailable = true,
    bool WrongDfuDriver = false,
    bool SerialDevicePresent = true,
    bool TargetAmbiguous = false,
    bool PackageBoardMismatch = false,
    FirmwareReleaseChannel Channel = FirmwareReleaseChannel.Stable,
    bool CustomPackageSelected = false);

