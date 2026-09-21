using MissionPlanner.Firmware.Model;

namespace MissionPlanner.Firmware.Installation;

/// <summary>Host boundary for normal ArduPilot upgrade handoff and verification.</summary>
public interface IFirmwareUpgradeConnection
{
    /// <summary>Validates the exact target, cancels its work and releases its serial port.</summary>
    Task<VehicleFirmwareIdentity> ReleaseAsync(SerialDeviceDescriptor device, FirmwareManifestEntry release, CancellationToken cancellationToken);

    /// <summary>Releases a normal local-package target using embedded expectations.</summary>
    Task<VehicleFirmwareIdentity> ReleaseAsync(SerialDeviceDescriptor device, SelectedFirmwareIdentity selected, CancellationToken cancellationToken) =>
        throw new NotSupportedException("Local identity handoff is unavailable.");

    /// <summary>Reconnects using embedded local expectations without catalogue provenance.</summary>
    Task<VehicleFirmwareIdentity> ReconnectAsync(SerialDeviceDescriptor device, SelectedFirmwareIdentity selected,
        VehicleFirmwareIdentity? original, CancellationToken cancellationToken) =>
        throw new NotSupportedException("Local identity verification is unavailable.");

    /// <summary>Reads the selected active controller's application claims without probing or changing it.</summary>
    RunningFirmwareIdentity? ReadRunningIdentity(SerialDeviceDescriptor device) => null;

    /// <summary>Releases a disarmed target for explicit recovery; implementations must retain ownership guards.</summary>
    Task<VehicleFirmwareIdentity> ReleaseForRecoveryAsync(SerialDeviceDescriptor device, CancellationToken cancellationToken) =>
        throw new NotSupportedException("Explicit connected recovery is unavailable.");

    /// <summary>Reconnects and verifies the running identity against the selected release.</summary>
    Task<VehicleFirmwareIdentity> ReconnectAsync(SerialDeviceDescriptor device, FirmwareManifestEntry release,
        VehicleFirmwareIdentity? original, CancellationToken cancellationToken);
}
