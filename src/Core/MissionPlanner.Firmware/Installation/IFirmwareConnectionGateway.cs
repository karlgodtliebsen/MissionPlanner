using MissionPlanner.Firmware.Workflow;

namespace MissionPlanner.Firmware.Installation;

/// <summary>Reports normal Mission Planner connection ownership.</summary>
public interface IFirmwareConnectionGateway
{
    /// <summary>Gets whether a vehicle connection currently owns transport resources.</summary>
    bool IsVehicleConnected { get; }

    /// <summary>Gets the active normal transport kind.</summary>
    ConnectionTransportKind? ActiveTransportKind { get; }

    /// <summary>Gets the serial port owned by the normal connection, including connection establishment.</summary>
    string? ActiveSerialPort => null;

    /// <summary>
    /// Reports the application runtime already proven by an active Mission Planner vehicle session
    /// that owns the given serial port, so identity can be reused without reopening or stealing the
    /// port. Returns <see langword="null"/> when no session owns that exact port or its autopilot
    /// identity does not prove a supported runtime.
    /// </summary>
    FirmwareRuntimeKind? IdentifyOwnedSerialRuntime(string? portName) => null;

    /// <summary>Requests a future host-controlled disconnect; first-release installation does not call it automatically.</summary>
    Task RequestDisconnectAsync(CancellationToken cancellationToken = default);
}
