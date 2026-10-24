using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MissionPlanner.Core.Commands;

namespace MissionPlanner.App.Views.InitSetup.MandatoryHardware.Sections;

public sealed partial class CompassSetupViewModel
{
    /// <summary>Gets or sets the independently measured true heading.</summary>
    [ObservableProperty]
    public partial double FixedYawDegrees { get; set; }

    /// <summary>Gets or sets whether the flight controller's current GPS fix supplies the location.</summary>
    [ObservableProperty]
    public partial bool FixedYawUseGps { get; set; } = true;

    /// <summary>Gets or sets the operator-supplied calibration latitude.</summary>
    [ObservableProperty]
    public partial double FixedYawLatitude { get; set; }

    /// <summary>Gets or sets the operator-supplied calibration longitude.</summary>
    [ObservableProperty]
    public partial double FixedYawLongitude { get; set; }

    [RelayCommand(IncludeCancelCommand = true)]
    private async Task CalibrateFixedYawAsync(CancellationToken cancellationToken)
    {
        if (!CanStart || activeVehicle.VehicleId is not { } id)
        {
            SetMessages("Connect and disarm the vehicle, resolve pending changes and finish other calibration work first.");
            return;
        }
        using var lifetime = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, activeVehicle.ConnectionCancellationToken);
        var heading = FixedYawDegrees;
        var latitude = FixedYawUseGps ? 0 : FixedYawLatitude;
        var longitude = FixedYawUseGps ? 0 : FixedYawLongitude;
        try
        {
            if (!await confirmation.ConfirmAsync("Known-heading compass calibration",
                    $"Confirm the vehicle is level and stationary, its independently measured true heading is {heading:F2}°, and it is away from magnetic interference. This recalculates and saves compass offsets.", "Calibrate", lifetime.Token))
            {
                return;
            }
            lifetime.Token.ThrowIfCancellationRequested();
            if (activeVehicle.VehicleId != id || !CanStart)
            {
                return;
            }
            SetBusy();
            var result = await commands.CalibrateCompassFixedYawAsync(id, heading, latitude, longitude, true, lifetime.Token);
            SetMessages(result.Result == VehicleCommandResult.Accepted
                ? "Known-heading calibration accepted by firmware. Refresh and inspect compass health and heading before flight."
                : $"Known-heading calibration: {result.Result}. {result.Message}");
        }
        catch (OperationCanceledException)
        {
            SetMessages("Calibration wait cancelled. The vehicle may have processed the command; refresh and inspect its state.");
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
