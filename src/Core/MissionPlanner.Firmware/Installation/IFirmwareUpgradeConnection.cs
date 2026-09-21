using MissionPlanner.Firmware.Model;

namespace MissionPlanner.Firmware.Installation;

/// <summary>Host boundary for normal ArduPilot upgrade handoff and verification.</summary>
public interface IFirmwareUpgradeConnection
{
    /// <summary>Validates the exact target, cancels its work and releases its serial port.</summary>
    Task<VehicleFirmwareIdentity> ReleaseAsync(SerialDeviceDescriptor device, FirmwareManifestEntry release, CancellationToken cancellationToken);

    /// <summary>Reconnects and verifies the running identity against the selected release.</summary>
    Task<VehicleFirmwareIdentity> ReconnectAsync(SerialDeviceDescriptor device, FirmwareManifestEntry release,
        VehicleFirmwareIdentity? original, CancellationToken cancellationToken);
}
