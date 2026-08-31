using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Mapsui.Utilities;
using Microsoft.Extensions.Logging;
using MissionPlanner.Firmware.Devices;

namespace MissionPlanner.App.Views.InitSetup.OptionalHardware.Sections;

public abstract partial class ExternalSerialToolViewModel(IFirmwareSerialDeviceCatalog devices, ILogger<ExternalSerialToolViewModel> logger) : OptionalHardwareBaseViewModel(logger)
{
    private CancellationTokenSource lifetime = new();
    public ObservableRangeCollection<string> Ports { get; } = [];
    [ObservableProperty]
    public partial string? SelectedPort
    {
        get; set;
    }

    [ObservableProperty] public partial string Status { get; protected set; } = "Select a serial device. The port is opened only when you explicitly connect.";

    [RelayCommand]
    private async Task RefreshPortsAsync()
    {
        var snapshot = await devices.GetDevicesAsync(lifetime.Token);
        Ports.ReplaceRange(snapshot.Select(d => d.PortName));
        SelectedPort ??= Ports.FirstOrDefault();

        SelectedPort = Ports.Contains(SelectedPort!) ? SelectedPort : Ports.FirstOrDefault();
    }

    /// <summary>
    /// 
    /// </summary>
    protected CancellationToken Token => lifetime.Token;

    /// <inheritdoc />
    public override Task ActivateAsync()
    {
        lifetime = new CancellationTokenSource();
        return base.ActivateAsync();
    }

    /// <inheritdoc />
    public override Task DeactivateAsync()
    {
        lifetime.Cancel();
        lifetime.Dispose();
        lifetime = new CancellationTokenSource();
        return base.DeactivateAsync();
    }

}
