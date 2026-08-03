using MissionPlanner.Firmware.Model;

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

/// <summary>Handles safety confirmations and manual actions in the host UI.</summary>
public interface IFirmwareUserInteraction
{
    /// <summary>Requests final confirmation after compatibility and before erase.</summary>
    Task<bool> ConfirmInstallationAsync(FirmwareInstallationConfirmation confirmation, CancellationToken cancellationToken = default);
    /// <summary>Presents a manual post-operation action.</summary>
    Task AcknowledgeManualActionAsync(FirmwareManualAction action, CancellationToken cancellationToken = default);
}

/// <summary>Runs the disconnected application-firmware workflow.</summary>
public interface IFirmwareInstallationService
{
    /// <summary>Installs firmware only after validation, identification, compatibility, and confirmation.</summary>
    Task<FirmwareOperationResult> InstallAsync(
        FirmwareInstallationRequest request,
        IProgress<FirmwareProgress>? progress = null,
        CancellationToken cancellationToken = default);
}
