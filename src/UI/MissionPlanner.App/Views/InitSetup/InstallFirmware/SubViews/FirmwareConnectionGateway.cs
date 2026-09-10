using MissionPlanner.Core.Vehicles.Abstractions;
using MissionPlanner.Firmware.Installation;
using MissionPlanner.Firmware.Workflow;

namespace MissionPlanner.App.Views.InitSetup.InstallFirmware.SubViews;

/// <summary>Adapts active Mission Planner connection ownership to firmware policy.</summary>
public sealed class FirmwareConnectionGateway(IActiveVehicleContext activeVehicle, IVehicleConnectionService connection,
    IVehicleConnectionSession? session = null) : IFirmwareConnectionGateway
{
    /// <summary>MAV_AUTOPILOT_ARDUPILOTMEGA from the MAVLink heartbeat autopilot enumeration.</summary>
    private const byte ArduPilotMegaAutopilot = 3;

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
    public FirmwareRuntimeKind? IdentifyOwnedSerialRuntime(string? portName)
    {
        // Only reuse a session that owns this exact serial port; never reopen or steal it.
        if (ActiveTransportKind != ConnectionTransportKind.Serial
            || string.IsNullOrWhiteSpace(ActiveSerialPort) || string.IsNullOrWhiteSpace(portName)
            || !string.Equals(Normalize(ActiveSerialPort), Normalize(portName), StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }
        var state = activeVehicle.State;
        if (state is null || !activeVehicle.IsOnline)
        {
            return null;
        }
        // Reuse the authoritative autopilot identity from the existing session's heartbeat.
        // Do not classify a generic MAVLink autopilot as ArduPilot.
        return state.Autopilot == ArduPilotMegaAutopilot ? FirmwareRuntimeKind.ArduPilot : null;
    }

    private static string Normalize(string value)
    {
        return value.Trim().Replace("\\\\.\\", string.Empty, StringComparison.Ordinal);
    }

    /// <inheritdoc />
    public Task RequestDisconnectAsync(CancellationToken cancellationToken = default)
    {
        throw new NotSupportedException("Disconnect the active vehicle explicitly before installing firmware.");
    }
}
