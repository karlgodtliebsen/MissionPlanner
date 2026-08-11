namespace MissionPlanner.Firmware.Installation;

/// <summary>Handles safety confirmations and manual actions in the host UI.</summary>
public interface IFirmwareUserInteraction
{
    /// <summary>Requests final confirmation after compatibility and before erase.</summary>
    Task<bool> ConfirmInstallationAsync(FirmwareInstallationConfirmation confirmation, CancellationToken cancellationToken = default);

    /// <summary>Presents a manual post-operation action.</summary>
    /// <returns><see langword="true"/> when the operator acknowledges the action; otherwise, <see langword="false"/>.</returns>
    Task<bool> AcknowledgeManualActionAsync(FirmwareManualAction action, CancellationToken cancellationToken = default);
}
