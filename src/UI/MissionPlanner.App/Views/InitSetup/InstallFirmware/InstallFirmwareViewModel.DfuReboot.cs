using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using MissionPlanner.Firmware.Model;

namespace MissionPlanner.App.Views.InitSetup.InstallFirmware;

public sealed partial class InstallFirmwareViewModel
{
    [RelayCommand]

    private async Task RebootToDfuAsync(CancellationToken cancellationToken)
    {
        if (!landing.CanRebootToDfu || IsOperationInProgress || ArePanelsRefreshing
            || DevicesModel.SelectedDevice?.Descriptor is not { } source)
        {
            return;
        }

        using var owned = BeginOperationCancellation(cancellationToken);
        var navigateToCatalogue = false;
        try
        {
            SelectedSectionIndex = (int)FirmwareSection.Stm32Dfu;
            SelectedDfuTabIndex = (int)Stm32DfuSection.Device;
            DfuModel.CorrelatedHandoff = null;
            landing.ErrorMessage = null;
            SetOperation(true, FirmwareOperationState.RequestingBootloaderReboot);
            await ShowOperationDialogAsync("Identifying selected controller", owned);
            var identified = await deviceIdentity.EnrichAsync([source], forceRefresh: true, cancellationToken: owned.Token);
            owned.Token.ThrowIfCancellationRequested();
            CloseOperationDialog();
            var verified = identified.SingleOrDefault(device => device.PortName == source.PortName
                && device.StableIdentity == source.StableIdentity && device.ArrivedAt == source.ArrivedAt);
            if (verified?.BetaflightIdentity is not { FirmwareVariant: "BTFL" } identity
                || string.IsNullOrWhiteSpace(identity.McuUniqueId))
            {
                var message = verified?.BetaflightProbeOutcome == MissionPlanner.Firmware.Betaflight.BetaflightProbeOutcome.PortBusy
                    ? $"Cannot open {source.PortName}: the port is in use or access was denied. Disconnect Betaflight Configurator (BetaFlightApp) and close other serial applications, then retry Reboot to DFU."
                    : $"Could not verify a Betaflight controller and MCU identity on {source.PortName}. "
                    + "Close other applications using this port and retry. If the controller runs ArduPilot or another firmware, use its BOOT/RESET procedure for STM32 DFU.";
                ReportDfuRebootError(message);

                SetMessages(message);
                NotificationManager?.Show(message);

                return;
            }
            source = verified;
            var confirmed = await dialogService.ConfirmAsync(
                dialogService.CreateOptions("Reboot controller to DFU", "Propellers removed — reboot", "Cancel"),
                $"Reboot the Betaflight controller on {source.PortName} into STM32 ROM DFU? "
                + "Remove all propellers and keep USB connected. No firmware will be written.", owned.Token);
            if (!confirmed)
            {
                return;
            }

            // The confirmation is closed before the progress window opens. Keep the captured
            // physical source even if USB discovery changes the current ComboBox selection.
            await ShowOperationDialogAsync("Entering STM32 DFU mode", owned);
            var result = await dfuHandoff.RebootAsync(source, CreateProgress(), owned.Token);
            if (!result.Succeeded)
            {
                ReportDfuRebootError(result.Code.Contains("port-busy", StringComparison.OrdinalIgnoreCase)
                    ? $"Cannot open {source.PortName}: the port is in use or access was denied. Disconnect Betaflight Configurator and close other serial applications, then retry."
                    : $"DFU reboot was not confirmed ({result.Code}). Check the selected controller or use its BOOT/RESET procedure, then refresh devices.");
                return;
            }

            await DfuModel.RefreshAfterInstallationAsync(owned.Token);
            DfuModel.SelectedDfuDevice = DfuModel.DfuDevices.SingleOrDefault(item =>
                item.Descriptor.ProviderId == result.Device?.ProviderId
                && item.Descriptor.ArrivedAt == result.Device.ArrivedAt);
            if (DfuModel.SelectedDfuDevice is not null)
            {
                DfuModel.CorrelatedHandoff = result;
                OnlineFirmwareModel.SetReviewedDfuTarget(betaflightCompatibility.Resolve(source.BetaflightIdentity!));
                navigateToCatalogue = true;
            }
            DfuModel.DfuStatus = DfuModel.SelectedDfuDevice is null
                ? "The controller entered DFU but is no longer detected. Refresh devices before continuing."
                : "The selected controller entered DFU. Open STM32 Bootloader to continue.";
            SetMessages(DfuModel.DfuStatus);
            NotificationManager?.Show(StatusMessage ?? "");
        }
        catch (OperationCanceledException) when (owned.IsCancellationRequested)
        {
            DfuModel.DfuStatus = "DFU reboot cancelled. If the controller already rebooted, refresh devices to detect its current mode.";
            SetMessages(errorMessage: DfuModel.DfuStatus);
            NotificationManager?.Show(ErrorMessage ?? "");
        }
        catch (Exception exception)
        {
            Logger.LogWarning(exception, "Unable to enter DFU on {PortName}.", source.PortName);
            ReportDfuRebootError(exception is UnauthorizedAccessException
                ? $"Cannot open {source.PortName}: the port is in use or access was denied. Disconnect Betaflight Configurator and close other serial applications, then retry."
                : "Unable to enter DFU: " + exception.Message);
            SetMessages(exception);
            NotificationManager?.Show(ErrorMessage ?? "");
        }
        finally
        {
            CloseOperationDialog();
            EndOperationCancellation(owned);
            SetOperation(false, null);
            if (navigateToCatalogue)
            {
                SelectedSectionIndex = (int)FirmwareSection.Stm32Dfu;
                SelectedDfuTabIndex = (int)Stm32DfuSection.Catalogue;
            }
        }
    }

    private void ReportDfuRebootError(string message)
    {
        CloseOperationDialog();
        DfuModel.DfuStatus = message;
        landing.ErrorMessage = message;
        SetMessages(null, message);
        NotificationManager?.Show(ErrorMessage ?? "");
        Logger.LogWarning("DFU reboot failed: {Reason}", message);
    }
}
