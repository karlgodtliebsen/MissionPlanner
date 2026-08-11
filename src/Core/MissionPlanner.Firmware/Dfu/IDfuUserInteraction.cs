namespace MissionPlanner.Firmware.Dfu;

/// <summary>Owns final DFU confirmation and manual recovery prompts in the host UI.</summary>
public interface IDfuUserInteraction
{
    /// <summary>Requests final confirmation after all target evidence is available.</summary>
    Task<bool> ConfirmAsync(DfuInstallationConfirmation confirmation, CancellationToken cancellationToken = default);

    /// <summary>Requests an acknowledged power-cycle or reset action when automatic detach is unavailable.</summary>
    Task<bool> AcknowledgePowerCycleAsync(DfuInstallationConfirmation confirmation, CancellationToken cancellationToken = default);
}
