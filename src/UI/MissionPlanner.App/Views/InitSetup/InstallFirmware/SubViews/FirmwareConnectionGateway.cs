using MissionPlanner.Core.Vehicles.Abstractions;
using MissionPlanner.Firmware.Installation;

namespace MissionPlanner.App.Views.InitSetup.InstallFirmware.SubViews;

/// <summary>Adapts active Mission Planner connection ownership to firmware policy.</summary>
public sealed class FirmwareConnectionGateway(IActiveVehicleContext activeVehicle, IVehicleConnectionService connection,
    IVehicleConnectionSession? session = null) : IFirmwareConnectionGateway
{
    /// <inheritdoc />
    public bool IsVehicleConnected => connection.IsConnected || activeVehicle.IsOnline;

    /// <inheritdoc />
    public ConnectionTransportKind? ActiveTransportKind => session?.ActiveTransportProtocol?.ToLowerInvariant() switch
    {
        "serial" => ConnectionTransportKind.Serial,
        "tcp" => ConnectionTransportKind.Tcp,
        "udp" => ConnectionTransportKind.Udp,
        _ => IsVehicleConnected ? ConnectionTransportKind.Other : null
    };

    /// <inheritdoc />
    public string? ActiveSerialPort => session?.ActiveSerialPort;

    /// <inheritdoc />
    public Task RequestDisconnectAsync(CancellationToken cancellationToken = default)
    {
        throw new NotSupportedException("Disconnect the active vehicle explicitly before installing firmware.");
    }
}
