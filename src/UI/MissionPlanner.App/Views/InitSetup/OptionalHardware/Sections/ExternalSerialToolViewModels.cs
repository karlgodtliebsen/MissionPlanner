using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MissionPlanner.Core.Setup.OptionalHardware;
using MissionPlanner.Firmware.Devices;

namespace MissionPlanner.App.Views.InitSetup.OptionalHardware.Sections;

public abstract partial class ExternalSerialToolViewModel(IFirmwareSerialDeviceCatalog devices) : OptionalHardwareBaseViewModel
{
    private CancellationTokenSource lifetime = new();
    public ObservableCollection<string> Ports { get; } = [];
    [ObservableProperty] public partial string? SelectedPort { get; set; }
    [ObservableProperty] public partial string Status { get; protected set; } = "Select a serial device. The port is opened only when you explicitly connect.";

    [RelayCommand]
    protected async Task RefreshPortsAsync()
    {
        var snapshot = await devices.GetDevicesAsync(lifetime.Token);
        Ports.Clear();
        foreach (var device in snapshot) Ports.Add(device.PortName);
        SelectedPort = Ports.Contains(SelectedPort) ? SelectedPort : Ports.FirstOrDefault();
    }

    protected CancellationToken Token => lifetime.Token;

    public override void Dispose()
    {
        lifetime.Cancel();
        lifetime.Dispose();
        lifetime = new CancellationTokenSource();
    }
}

public sealed partial class SikRadioViewModel(IFirmwareSerialDeviceCatalog devices, ISikRadioConfigurator configurator)
    : ExternalSerialToolViewModel(devices)
{
    [ObservableProperty] public partial string Identity { get; private set; } = string.Empty;
    [ObservableProperty] public partial string SettingsText { get; set; } = string.Empty;
    [ObservableProperty] public partial int BaudRate { get; set; } = 57600;

    [RelayCommand]
    private async Task ConnectAsync()
    {
        if (SelectedPort is null) return;
        try
        {
            IsBusy = true;
            var snapshot = await configurator.ReadAsync(SelectedPort, BaudRate, Token);
            Identity = snapshot.Identity;
            SettingsText = string.Join(Environment.NewLine, snapshot.LocalSettings.Select(pair => $"{pair.Key}={pair.Value}"));
            Status = snapshot.RemoteSettings.Count == 0 ? "Local radio detected; remote settings are unavailable." : "Local and remote radio settings loaded.";
        }
        catch (Exception exception) when (exception is not OperationCanceledException) { Status = exception.Message; }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private async Task ApplyAsync()
    {
        if (SelectedPort is null) return;
        var settings = SettingsText.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
            .Select(line => line.Split('=', 2))
            .Where(parts => parts.Length == 2)
            .ToDictionary(parts => parts[0].Trim(), parts => parts[1].Trim(), StringComparer.Ordinal);
        await configurator.ApplyAsync(SelectedPort, BaudRate, settings, Token);
        Status = "Settings saved; the radio was rebooted.";
    }
}

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
        if (SelectedPort is null) return;
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
        if (SelectedPort is null || module is null) return;
        await configurator.ApplyAsync(SelectedPort, module, new BluetoothModuleSettings(Name, BaudRate, Pin), Token);
        Pin = string.Empty;
        Status = "Supported Bluetooth settings applied. Reconnect at the selected baud if it changed.";
    }
}
