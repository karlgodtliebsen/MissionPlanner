using MissionPlanner.Core.Vehicles.Abstractions;
using MissionPlanner.Firmware.Entry;
using MissionPlanner.Firmware.Installation;
using MissionPlanner.Firmware.Model;

namespace MissionPlanner.App.Views.InitSetup.InstallFirmware;

/// <summary>Adapts active Mission Planner connection ownership to firmware policy.</summary>
public sealed class FirmwareConnectionGateway(IActiveVehicleContext activeVehicle) : IFirmwareConnectionGateway
{
    /// <inheritdoc />
    public bool IsVehicleConnected => activeVehicle.IsOnline;
    /// <inheritdoc />
    public ConnectionTransportKind? ActiveTransportKind => IsVehicleConnected ? ConnectionTransportKind.Other : null;
    /// <inheritdoc />
    public Task RequestDisconnectAsync(CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("Disconnect the active vehicle explicitly before installing firmware.");
}

/// <summary>Declines temporary serial reboot when no host-owned application device was selected.</summary>
public sealed class TemporaryMavLinkBootloaderGateway : ITemporaryMavLinkBootloaderGateway
{
    /// <inheritdoc />
    public Task<bool> RebootToBootloaderAsync(SerialDeviceDescriptor applicationDevice, CancellationToken cancellationToken = default) =>
        Task.FromResult(false);
}
