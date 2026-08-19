using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
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
        foreach (var device in snapshot)
        {
            Ports.Add(device.PortName);
        }

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
