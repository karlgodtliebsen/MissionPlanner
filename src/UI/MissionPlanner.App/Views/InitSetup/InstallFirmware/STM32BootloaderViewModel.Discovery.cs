using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Extensions.Logging;
using MissionPlanner.App.Views.InitSetup.InstallFirmware.SubViews;
using MissionPlanner.Core.Vehicles;
using MissionPlanner.Core.Vehicles.Abstractions;
using MissionPlanner.Firmware.Dfu;

namespace MissionPlanner.App.Views.InitSetup.InstallFirmware;

public sealed partial class STM32BootloaderViewModel
{
    private readonly IDfuDeviceCatalog deviceCatalog;
    private readonly IDfuToolLocator toolLocator;
    private readonly IActiveVehicleContext activeVehicle;
    private readonly FirmwarePanelLoader loader = new();
    internal bool DiscoveryOwnedByPage { get; set; }
    /// <summary>Gets the most recent CubeProgrammer readiness evidence.</summary>
    [ObservableProperty, NotifyPropertyChangedFor(nameof(ToolReadiness))]
    public partial DfuToolStatus? ToolStatus { get; private set; }

    /// <summary>Gets explicit tool readiness for every DFU subview.</summary>
    public string ToolReadiness => ToolStatus is null ? "STM32CubeProgrammer has not been checked. Refresh devices."
        : $"STM32CubeProgrammer: {ToolStatus.Availability}. {ToolStatus.Diagnostic}";
    /// <summary>Gets whether DFU discovery is running.</summary>
    [ObservableProperty] public partial bool IsRefreshing { get; private set; }
    /// <summary>Gets or sets the parent's exclusive installation interlock.</summary>
    [ObservableProperty] public partial bool InstallationRunning { get; set; }
    /// <summary>Notifies the active parent about DFU scan ownership.</summary>
    public event Action<bool>? RefreshStateChanged;
    partial void OnIsRefreshingChanged(bool value) => RefreshStateChanged?.Invoke(value);
    partial void OnInstallationRunningChanged(bool value)
    {
        if (value) { loader.Cancel(); }
    }

    /// <summary>Refreshes DFU devices and tool readiness without fetching the firmware catalogue.</summary>
    public Task RefreshAsync(CancellationToken cancellationToken = default)
    {
        if (InstallationRunning || activeVehicle.IsOnline) { return Task.CompletedTask; }
        return LoadDevicesAsync(false, cancellationToken);
    }

    /// <summary>Checks device rediscovery after the parent has completed programming.</summary>
    public Task RefreshAfterInstallationAsync(CancellationToken cancellationToken) => LoadDevicesAsync(true, cancellationToken);

    private Task LoadDevicesAsync(bool afterInstallation, CancellationToken cancellationToken) => loader.RunAsync(async token =>
    {
        if (!afterInstallation && (InstallationRunning || activeVehicle.IsOnline)) { return; }
        try
        {
            IsRefreshing = true;
            DfuStatus = "Looking for STM32 DFU devices and STM32CubeProgrammer…";
            var devicesTask = Task.Run(() => deviceCatalog.GetDevicesAsync(token), token);
            var toolTask = Task.Run(() => toolLocator.LocateAsync(token), token);
            await Task.WhenAll(devicesTask, toolTask);
            var devices = await devicesTask;
            var tool = await toolTask;
            await Dispatcher.DispatchAsync(() =>
            {
                if (token.IsCancellationRequested) { return; }
                var previousId = SelectedDfuDevice?.Descriptor.ProviderId;
                ToolStatus = tool;
                DfuDevices = devices.Select(device => new DfuDeviceItemViewModel(device)).ToArray();
                SelectedDfuDevice = previousId is null
                    ? DfuDevices.Count == 1 ? DfuDevices[0] : null
                    : DfuDevices.FirstOrDefault(item => string.Equals(item.Descriptor.ProviderId, previousId, StringComparison.OrdinalIgnoreCase));
                DfuStatus = afterInstallation
                    ? DfuDevices.Count == 0
                        ? "STM32 DFU device is no longer present. The controller has left ROM bootloader mode."
                        : "STM32 DFU device is still present. Release BOOT/DFU, then reset or reconnect the controller."
                    : tool.Availability != DfuToolAvailability.Available
                        ? tool.Diagnostic ?? "Install STM32CubeProgrammer and its DFU driver on a supported platform."
                        : DfuDevices.Count == 0
                            ? "No STM32 DFU device detected. Follow the board's BOOT/RESET procedure, then refresh devices."
                            : SelectedDfuDevice?.Descriptor.DriverState == DfuDriverState.PresentReady
                                ? "STM32 DFU device and STM32CubeProgrammer are ready."
                                : "Select a DFU device and resolve any indicated driver problem.";
            });
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { }
        catch (Exception exception)
        {
            Logger.LogWarning(exception, "STM32 DFU discovery failed.");
            await Dispatcher.DispatchAsync(() =>
            {
                if (!token.IsCancellationRequested) { DfuStatus = "Unable to discover DFU devices. Check platform support, drivers and STM32CubeProgrammer, then refresh."; }
            });
        }
        finally { await Dispatcher.DispatchAsync(() => IsRefreshing = false); }
    }, cancellationToken);

    private void VehicleChanged(ActiveVehicleChangedEventArgs args) => Dispatcher.Dispatch(() =>
    {
        if (!loader.IsActive) { return; }
        if (args.Current.IsOnline) { loader.Cancel(); }
        else { _ = RefreshAsync(); }
    });
}
