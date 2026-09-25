using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Mapsui.Utilities;
using Microsoft.Extensions.Logging;
using MissionPlanner.Core.Setup.OptionalHardware;
using MissionPlanner.App.Views.Navigation;

namespace MissionPlanner.App.Views.InitSetup.OptionalHardware.Sections;

public sealed partial class JoystickViewModel(IJoystickProvider provider, IJoystickVehicleOutput output, ILogger<JoystickViewModel> logger, INavigationService navigation) : OptionalHardwareBaseViewModel(logger)
{
    /// <summary>
    ///
    /// </summary>
    public ObservableRangeCollection<JoystickDeviceDescriptor> Devices { get; } = [];

    [ObservableProperty]
    public partial JoystickDeviceDescriptor? SelectedDevice
    {
        get; set;
    }

    [ObservableProperty]
    public partial bool VehicleOutputEnabled
    {
        get; set;
    }

    /// <summary>
    ///
    /// </summary>
    public string PlatformStatus => provider.IsSupported ? "Joystick adapter available." : "No joystick platform adapter is installed on this platform.";

    /// <summary>Opens the shared Full Parameters workspace.</summary>
    [RelayCommand]
    private Task OpenFullParametersAsync()
    {
        return navigation.NavigateAsync(MissionPlannerRoutes.ConfigFullParameters);
    }

    [RelayCommand]
    private async Task RefreshAsync()
    {
        var devices = await provider.EnumerateAsync(CancellationToken.None);
        await Dispatcher.DispatchAsync(() =>
        {
            Devices.ReplaceRange(devices);
            SetMessages($"{Devices.Count} device(s) found.");
        });
    }

    [RelayCommand]
    private async Task DisableAsync()
    {
        VehicleOutputEnabled = false;
        await output.ReleaseAsync(CancellationToken.None);
        SetMessages("Vehicle output disabled and released.");
    }

    /// <inheritdoc />
    public override Task ActivateAsync()
    {
        VehicleOutputEnabled = true;
        return base.ActivateAsync();
    }

    /// <inheritdoc />
    public override async Task DeactivateAsync()
    {
        VehicleOutputEnabled = false;
        await output.ReleaseAsync(CancellationToken.None);
        await base.DeactivateAsync();
    }

    /// <summary>
    ///
    /// </summary>
    public override void Dispose()
    {
        VehicleOutputEnabled = false;
        base.Dispose();
    }
}

