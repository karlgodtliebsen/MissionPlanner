using CommunityToolkit.Mvvm.Input;

namespace MissionPlanner.App.Views.InitSetup.MandatoryHardware.Sections;

public sealed partial class EscMotorSetupViewModel
{
    [RelayCommand(IncludeCancelCommand = true)]
    private Task PrepareEscCalibrationAsync(CancellationToken cancellationToken)
    {
        return SetCalibrationPendingAsync(true, cancellationToken);
    }

    [RelayCommand(IncludeCancelCommand = true)]
    private Task ClearEscCalibrationAsync(CancellationToken cancellationToken)
    {
        return SetCalibrationPendingAsync(false, cancellationToken);
    }

    private async Task SetCalibrationPendingAsync(bool prepare, CancellationToken cancellationToken)
    {
        if (IsBusy || IsRunning || !activeVehicle.IsOnline || activeVehicle.VehicleId is not { } id)
        {
            SetMessages("Connect and disarm the vehicle, and finish the current operation first.");
            return;
        }
        using var lifetime = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, activeVehicle.ConnectionCancellationToken);
        try
        {
            if (!await confirmation.ConfirmAsync(prepare ? "Prepare ESC calibration" : "Clear pending ESC calibration",
                    prepare ? "Confirm ALL propellers are removed and ESC power is disconnected. This stores ESC_CALIBRATION=3, causing the next power-up to run calibration. Follow the firmware's power-cycle, safety-button and tone instructions."
                        : "Restore normal startup by clearing ESC_CALIBRATION to zero? Keep propellers removed while verifying the configuration.",
                    prepare ? "Prepare next startup" : "Restore normal startup", lifetime.Token))
            {
                return;
            }
            lifetime.Token.ThrowIfCancellationRequested();
            if (activeVehicle.VehicleId != id)
            {
                return;
            }
            SetBusy();
            var result = await actuatorService.SetEscCalibrationPendingAsync(id, prepare, true, lifetime.Token);
            SetMessages(result.Message);
        }
        catch (OperationCanceledException)
        {
            SetMessages("Operation cancelled. Verify ESC_CALIBRATION before applying flight power; the write may have reached the vehicle.");
        }
        catch (Exception exception)
        {
            SetMessages(exception);
        }
        finally
        {
            ResetBusy();
        }
    }
}
