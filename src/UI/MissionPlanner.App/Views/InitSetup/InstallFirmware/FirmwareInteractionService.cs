using MissionPlanner.App.Presentation;
using MissionPlanner.Firmware.Entry;
using MissionPlanner.Firmware.Installation;

namespace MissionPlanner.App.Views.InitSetup.InstallFirmware;

/// <summary>Presents firmware-domain safety interactions through the shared resilient dialog service.</summary>
public sealed class FirmwareInteractionService(IUserConfirmationService confirmation) :
    IFirmwareUserInteraction,
    IBootloaderEntryInteraction
{
    /// <inheritdoc />
    public Task<bool> ConfirmInstallationAsync(FirmwareInstallationConfirmation request, CancellationToken cancellationToken = default) =>
        confirmation.ConfirmAsync(
            "Confirm firmware installation",
            $"Detected bootloader board ID {request.DetectedBoardId}. The selected firmware targets board ID {request.FirmwareBoardId} and will write {request.ImageSize:N0} bytes. Keep power connected during erase, programming, and verification.",
            "Erase and Install",
            cancellationToken);

    /// <inheritdoc />
    public async Task AcknowledgeManualActionAsync(FirmwareManualAction action, CancellationToken cancellationToken = default) =>
        _ = await confirmation.ConfirmAsync("Firmware", Message(action.Code), "Continue", cancellationToken);

    /// <inheritdoc />
    public async Task RequestAsync(string interactionCode, CancellationToken cancellationToken = default) =>
        _ = await confirmation.ConfirmAsync("Flight controller bootloader", Message(interactionCode), "Continue", cancellationToken);

    private static string Message(string code) => code switch
    {
        "bootloader.manual-reconnect" => "Unplug and reconnect the flight controller, or press its hardware reset button, to enter the bootloader.",
        "installation.reconnect-after-reboot" => "The controller has rebooted. Wait for ArduPilot to reappear before reconnecting Mission Planner.",
        _ => code
    };
}
