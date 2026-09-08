using MissionPlanner.App.Presentation;
using MissionPlanner.Firmware.Entry;
using MissionPlanner.Firmware.Dfu;
using MissionPlanner.Firmware.Installation;
using MissionPlanner.Firmware.Model;

namespace MissionPlanner.App.Views.InitSetup.InstallFirmware;

/// <summary>Presents firmware-domain safety interactions through the shared resilient dialog service.</summary>
public sealed class FirmwareInteractionService(IUserConfirmationService confirmation) :
    IFirmwareUserInteraction,
    IBootloaderEntryInteraction,
    IDfuUserInteraction
{
    /// <inheritdoc />
    public Task<bool> ConfirmInstallationAsync(FirmwareInstallationConfirmation request, CancellationToken cancellationToken = default)
    {
        var message = $"Firmware source: {request.Source}\n" +
                      $"Firmware package board ID: {request.FirmwareBoardId}\n" +
                      $"Detected bootloader board ID: {request.DetectedBoardId}\n" +
                      $"Application image size: {request.ImageSize:N0} bytes\n" +
                      $"Detected bootloader revision: {request.BootloaderRevision}\n\n" +
                      "Keep power connected during erase, programming, and verification.";
        if (request.BoardIdMismatchOverrideUsed && request.RequiredPhrase is { Length: > 0 } phrase)
        {
            return confirmation.ConfirmPhraseAsync(
                "Board identity mismatch — advanced firmware override",
                $"{message}\n\nThe selected local firmware target does not match the detected controller identity. An incompatible image may make the controller unbootable.",
                phrase,
                cancellationToken);
        }

        return confirmation.ConfirmAsync("Confirm firmware installation", message, "Erase and Install", cancellationToken);
    }

    /// <inheritdoc />
    public Task<bool> AcknowledgeManualActionAsync(FirmwareManualAction action, CancellationToken cancellationToken = default) =>
        confirmation.ConfirmAsync("Firmware", Message(action.Code), "Continue", cancellationToken);

    /// <inheritdoc />
    public Task<bool> RequestAsync(string interactionCode, CancellationToken cancellationToken = default) =>
        confirmation.ConfirmAsync("Flight controller bootloader", Message(interactionCode), "Continue", cancellationToken);

    /// <inheritdoc />
    public Task<bool> ConfirmAsync(DfuInstallationConfirmation request, CancellationToken cancellationToken = default) =>
        confirmation.ConfirmAsync(
            "Erase Betaflight and install ArduPilot",
            $"STM32 DFU device VID_{request.Device.VendorId:X4}&PID_{request.Device.ProductId:X4} will be programmed with {request.Artifact.FileName} for {request.Platform} (board ID {request.BoardId}). Programming and verification must finish without loss of power.",
            "Erase and Install",
            cancellationToken);

    /// <inheritdoc />
    public Task<bool> AcknowledgePowerCycleAsync(DfuInstallationConfirmation request, CancellationToken cancellationToken = default) =>
        confirmation.ConfirmAsync(
            "Restart the flight controller",
            "Programming and verification completed. Release BOOT/DFU, then press RESET or reconnect USB so the new ArduPilot firmware can start.",
            "Continue",
            cancellationToken);

    private static string Message(string code) => code switch
    {
        FirmwareInteractionCodes.ManualBootloaderReconnect => "Click Continue, then immediately unplug and reconnect the flight controller or press its hardware reset button. Mission Planner will watch for the ArduPilot bootloader.",
        FirmwareInteractionCodes.ReconnectAfterReboot => "The controller has rebooted. Wait for ArduPilot to reappear before reconnecting Mission Planner.",
        _ => "Mission Planner requires an additional firmware action. Copy the diagnostic report if this message persists."
    };
}
