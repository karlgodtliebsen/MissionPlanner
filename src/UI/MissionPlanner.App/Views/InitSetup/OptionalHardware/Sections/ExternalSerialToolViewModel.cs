using AsyncAwaitBestPractices;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Mapsui.Utilities;
using Microsoft.Extensions.Logging;
using MissionPlanner.Firmware.Devices;

using MissionPlanner.App.Views.Navigation;

namespace MissionPlanner.App.Views.InitSetup.OptionalHardware.Sections;

/// <summary>Base class for tools that talk to a device on a serial port outside the MAVLink connection.</summary>
public abstract partial class ExternalSerialToolViewModel(IFirmwareSerialDeviceCatalog devices, ILogger<ExternalSerialToolViewModel> logger, INavigationService navigation) : OptionalHardwareBaseViewModel(logger)
{
    /// <summary>Refreshes the current page state without starting a hardware operation.</summary>
    [RelayCommand]
    private Task RefreshAsync()
    {
        return RefreshPortsAsync();
    }

    private CancellationTokenSource lifetime = new();
    private bool disposed;
    /// <summary>Gets the available serial ports.</summary>
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

    /// <summary>Gets the current page lifetime, cancelled before outstanding operations are drained.</summary>
    protected CancellationToken Token => lifetime.Token;

    /// <inheritdoc />
    public override Task ActivateAsync()
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        if (lifetime.IsCancellationRequested) { lifetime.Dispose(); lifetime = new(); }
        return base.ActivateAsync();
    }

    /// <inheritdoc />
    public override async Task DeactivateAsync()
    {
        lifetime.Cancel();
        await Task.WhenAll(new[] { RefreshCommand.ExecutionTask, RefreshPortsCommand.ExecutionTask, StopOperationsAsync() }.OfType<Task>());
        await base.DeactivateAsync();
    }

    /// <summary>Waits for derived serial operations after their page token has been cancelled.</summary>
    protected virtual Task StopOperationsAsync() => Task.CompletedTask;

    /// <inheritdoc />
    public override void Dispose()
    {
        if (disposed) return;
        disposed = true;
        DeactivateAsync().SafeFireAndForget(exception => Logger.LogWarning(exception, "Serial tool cleanup failed."));
        base.Dispose();
    }

}

