using MissionPlanner.Firmware.Model;
using MissionPlanner.Firmware.Operations;

namespace MissionPlanner.Firmware.Connected;

/// <summary>Implements guarded connected embedded-bootloader update semantics.</summary>
public sealed class EmbeddedBootloaderUpdateService(
    IFirmwareOperationCoordinator operationCoordinator,
    IConnectedVehicleFirmwareGateway gateway) : IEmbeddedBootloaderUpdateService
{
    /// <inheritdoc />
    public async Task<BootloaderUpdateResult> UpdateAsync(BootloaderUpdateRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        using var operation = operationCoordinator.Begin(FirmwareOperationKind.UpdateEmbeddedBootloader);
        if (!gateway.IsConnected) return Finish(ConnectedFirmwareCommandResult.Failed, "bootloader-update.not-connected");
        if (gateway.IsArmed) return Finish(ConnectedFirmwareCommandResult.Denied, "bootloader-update.vehicle-armed");
        if (!gateway.IsSupportedArduPilot) return Finish(ConnectedFirmwareCommandResult.Unsupported, "bootloader-update.unsupported-autopilot");
        if (!request.WarningAccepted) return Finish(ConnectedFirmwareCommandResult.Denied, "bootloader-update.warning-not-accepted");

        operation.Transition(new FirmwareProgress(FirmwareOperationState.CheckingCompatibility, null, "bootloader-update.preconditions-passed"));
        operation.Transition(new FirmwareProgress(FirmwareOperationState.Programming, null, "bootloader-update.command-pending"));
        ConnectedFirmwareCommandResult commandResult;
        try
        {
            commandResult = await gateway.FlashEmbeddedBootloaderAsync(cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            operation.Transition(new FirmwareProgress(FirmwareOperationState.Failed, null, "bootloader-update.command-failed"));
            throw;
        }
        if (commandResult == ConnectedFirmwareCommandResult.Accepted)
        {
            operation.Transition(new FirmwareProgress(FirmwareOperationState.Completed, 100, "bootloader-update.accepted"));
            return new BootloaderUpdateResult(operation.OperationId, commandResult, "bootloader-update.accepted", true);
        }

        operation.Transition(new FirmwareProgress(FirmwareOperationState.Failed, null, ResultCode(commandResult)));
        return new BootloaderUpdateResult(operation.OperationId, commandResult, ResultCode(commandResult), false);

        BootloaderUpdateResult Finish(ConnectedFirmwareCommandResult result, string code)
        {
            operation.Transition(new FirmwareProgress(FirmwareOperationState.Failed, null, code));
            return new BootloaderUpdateResult(operation.OperationId, result, code, false);
        }
    }

    private static string ResultCode(ConnectedFirmwareCommandResult result) => result switch
    {
        ConnectedFirmwareCommandResult.TemporarilyRejected => "bootloader-update.temporarily-rejected",
        ConnectedFirmwareCommandResult.Denied => "bootloader-update.denied",
        ConnectedFirmwareCommandResult.Unsupported => "bootloader-update.unsupported-or-no-embedded-image",
        ConnectedFirmwareCommandResult.Timeout => "bootloader-update.timeout",
        _ => "bootloader-update.failed"
    };
}
