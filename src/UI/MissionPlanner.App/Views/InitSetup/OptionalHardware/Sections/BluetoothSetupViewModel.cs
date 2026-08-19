using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MissionPlanner.Core.Setup.OptionalHardware;
using MissionPlanner.Firmware.Devices;

namespace MissionPlanner.App.Views.InitSetup.OptionalHardware.Sections;

public sealed partial class BluetoothSetupViewModel(IFirmwareSerialDeviceCatalog devices, IBluetoothSerialConfigurator configurator)
    : ExternalSerialToolViewModel(devices)
{
    private BluetoothModuleSnapshot? module;
    [ObservableProperty] public partial string ModuleIdentity { get; private set; } = string.Empty;
    [ObservableProperty] public partial string Name { get; set; } = string.Empty;
    [ObservableProperty] public partial string Pin { get; set; } = string.Empty;
    [ObservableProperty] public partial int BaudRate { get; set; } = 9600;

    [RelayCommand]
    private async Task ProbeAsync()
    {
        if (SelectedPort is null)
        {
            return;
        }

        try
        {
            IsBusy = true;
            module = await configurator.ProbeAsync(SelectedPort, Token);
            ModuleIdentity = $"{module.Dialect} at {module.BaudRate} baud — {module.Identity}";
            BaudRate = module.BaudRate;
            Status = "Classic serial Bluetooth module detected.";
        }
        catch (Exception exception) when (exception is not OperationCanceledException) { Status = exception.Message; }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private async Task ApplyAsync()
    {
        if (SelectedPort is null || module is null)
        {
            return;
        }

        await configurator.ApplyAsync(SelectedPort, module, new BluetoothModuleSettings(Name, BaudRate, Pin), Token);
        Pin = string.Empty;
        Status = "Supported Bluetooth settings applied. Reconnect at the selected baud if it changed.";
    }
}
