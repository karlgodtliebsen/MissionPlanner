using MissionPlanner.Core.Vehicles.Abstractions;
using MissionPlanner.Firmware.Installation;

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
