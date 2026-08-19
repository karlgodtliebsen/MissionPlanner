using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MissionPlanner.Core.Setup.OptionalHardware;

namespace MissionPlanner.App.Views.InitSetup.OptionalHardware.Sections;

public sealed partial class JoystickViewModel(IJoystickProvider provider, IJoystickVehicleOutput output) : OptionalHardwareBaseViewModel
{
    public ObservableCollection<JoystickDeviceDescriptor> Devices { get; } = [];
    [ObservableProperty] public partial JoystickDeviceDescriptor? SelectedDevice { get; set; }
    [ObservableProperty] public partial bool VehicleOutputEnabled { get; set; }
    public string PlatformStatus => provider.IsSupported ? "Joystick adapter available." : "No joystick platform adapter is installed on this platform.";

    [RelayCommand]
    private async Task RefreshAsync()
    {
        var devices = await provider.EnumerateAsync(CancellationToken.None);
        await Dispatcher.DispatchAsync(() => { Devices.Clear(); foreach (var device in devices) Devices.Add(device); });
        StatusMessage = $"{Devices.Count} device(s) found.";
    }
    [RelayCommand]
    private async Task DisableAsync()
    {
        VehicleOutputEnabled = false;
        await output.ReleaseAsync(CancellationToken.None);
        StatusMessage = "Vehicle output disabled and released.";
    }
    public override void Dispose() { VehicleOutputEnabled = false; _ = output.ReleaseAsync(CancellationToken.None); base.Dispose(); }
}
