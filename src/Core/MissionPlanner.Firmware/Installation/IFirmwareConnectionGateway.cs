namespace MissionPlanner.Firmware.Installation;

/// <summary>Reports normal Mission Planner connection ownership.</summary>
public interface IFirmwareConnectionGateway
{
    /// <summary>Gets whether a vehicle connection currently owns transport resources.</summary>
    bool IsVehicleConnected { get; }

    /// <summary>Gets the active normal transport kind.</summary>
    ConnectionTransportKind? ActiveTransportKind { get; }

    /// <summary>Requests a future host-controlled disconnect; first-release installation does not call it automatically.</summary>
    Task RequestDisconnectAsync(CancellationToken cancellationToken = default);
}
