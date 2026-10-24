using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using MissionPlanner.Core.Setup.OptionalHardware;
using MissionPlanner.Firmware.Devices;

using MissionPlanner.App.Views.Navigation;

namespace MissionPlanner.App.Views.InitSetup.OptionalHardware.Sections;

/// <summary>Configures a serial Bluetooth module.</summary>
public sealed partial class BluetoothSetupViewModel(IFirmwareSerialDeviceCatalog devices, IBluetoothSerialConfigurator configurator, ILogger<BluetoothSetupViewModel> logger, INavigationService navigation)
    : ExternalSerialToolViewModel(devices, logger, navigation)
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
            SetBusy();
            module = await configurator.ProbeAsync(SelectedPort, Token);
            ModuleIdentity = $"{module.Dialect} at {module.BaudRate} baud — {module.Identity}";
            BaudRate = module.BaudRate;
            Status = "Classic serial Bluetooth module detected.";
        }
        catch (OperationCanceledException) { Status = "Bluetooth discovery cancelled."; }
        catch (Exception exception) { Status = exception.Message; }
        finally { ResetBusy(); }
    }

    [RelayCommand]
    private async Task ApplyAsync()
    {
        if (SelectedPort is null || module is null)
        {
            return;
        }

        if (IsBusy) return;
        SetBusy();
        try
        {
            await configurator.ApplyAsync(SelectedPort, module, new BluetoothModuleSettings(Name, BaudRate, Pin), Token);
            Pin = string.Empty;
            Status = "Supported Bluetooth settings applied. Reconnect at the selected baud if it changed.";
        }
        catch (OperationCanceledException) { Status = "Bluetooth update cancelled; read settings before retrying."; }
        catch (Exception exception) { Status = exception.Message; }
        finally { ResetBusy(); }
    }
    /// <inheritdoc />
    protected override async Task StopOperationsAsync()
    {
        await Task.WhenAll(new[] { ProbeCommand.ExecutionTask, ApplyCommand.ExecutionTask }.OfType<Task>());
        module = null;
        Pin = string.Empty;
    }

}

