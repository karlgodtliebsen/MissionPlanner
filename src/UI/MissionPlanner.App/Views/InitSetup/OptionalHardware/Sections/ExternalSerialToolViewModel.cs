using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Mapsui.Utilities;
using Microsoft.Extensions.Logging;
using MissionPlanner.Firmware.Devices;

using MissionPlanner.App.Views.Navigation;

namespace MissionPlanner.App.Views.InitSetup.OptionalHardware.Sections;

public abstract partial class ExternalSerialToolViewModel(IFirmwareSerialDeviceCatalog devices, ILogger<ExternalSerialToolViewModel> logger, INavigationService navigation) : OptionalHardwareBaseViewModel(logger)
{
    /// <summary>Refreshes the current page state without starting a hardware operation.</summary>
    [RelayCommand]
    private Task RefreshAsync()
    {
        return RefreshPortsAsync();
    }

    /// <summary>Opens the shared Parameters Editor workspace when no operation is running.</summary>
    [RelayCommand]
    private async Task OpenFullParametersAsync()
    {
        if (IsBusy)
        {
            return;
        }
        await navigation.NavigateAsync(MissionPlannerRoutes.Configuration);
    }

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
        var token = lifetime.Token;
        try
        {
            var snapshot = await devices.GetDevicesAsync(token);
            token.ThrowIfCancellationRequested();
            Ports.ReplaceRange(snapshot.Select(d => d.PortName));
            SelectedPort ??= Ports.FirstOrDefault();

            SelectedPort = Ports.Contains(SelectedPort!) ? SelectedPort : Ports.FirstOrDefault();
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception exception)
        {
            SetMessages(exception);
        }
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

