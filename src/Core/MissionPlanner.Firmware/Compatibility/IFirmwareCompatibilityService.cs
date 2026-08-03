using MissionPlanner.Firmware.Model;

namespace MissionPlanner.Firmware.Compatibility;

/// <summary>Evaluates all known compatibility evidence before erase.</summary>
public interface IFirmwareCompatibilityService
{
    /// <summary>Checks a validated package against protocol-confirmed hardware.</summary>
    FirmwareCompatibilityResult Check(ApjFirmwarePackage firmware, BootloaderIdentity bootloader);
}
