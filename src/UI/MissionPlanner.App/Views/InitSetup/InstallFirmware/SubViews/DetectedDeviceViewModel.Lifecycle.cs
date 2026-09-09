using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using MissionPlanner.Core.Vehicles;
using MissionPlanner.Core.Vehicles.Abstractions;
using MissionPlanner.Firmware.Devices;
using MissionPlanner.Firmware.Model;

namespace MissionPlanner.App.Views.InitSetup.InstallFirmware.SubViews;

public sealed partial class DetectedDeviceViewModel
{
    private readonly IFirmwareSerialDeviceCatalog deviceCatalog;
    private readonly MissionPlanner.Firmware.Betaflight.IFirmwareDeviceIdentityService identityService;
    private readonly IActiveVehicleContext activeVehicle;
    private readonly FirmwarePanelLoader loader = new();
    internal bool DiscoveryOwnedByPage
    {
        get; set;
    }
    private IReadOnlyList<FirmwareManifestEntry> entries = [];
    /// <summary>Gets the latest device descriptors for catalogue recommendations.</summary>
    public IReadOnlyList<SerialDeviceDescriptor> Descriptors { get; private set; } = [];
    /// <summary>Gets whether a device scan is running.</summary>
    [ObservableProperty]
    public partial bool IsRefreshing
    {
        get; private set;
    }
    /// <summary>Gets or sets the installation interlock shared by the parent.</summary>
    [ObservableProperty]
    public partial bool InstallationRunning
    {
        get; set;
    }
    /// <summary>Notifies catalogue consumers about newly discovered device evidence.</summary>
    public event Action<IReadOnlyList<SerialDeviceDescriptor>>? DevicesChanged;
    /// <summary>Notifies the active parent about device scan ownership.</summary>
    public event Action<bool>? RefreshStateChanged;
    partial void OnIsRefreshingChanged(bool value) => RefreshStateChanged?.Invoke(value);
    partial void OnInstallationRunningChanged(bool value)
    {
        if (value)
        {
            loader.Cancel();
            identityService.Invalidate();
        }
    }

    /// <inheritdoc />
    public override async Task ActivateAsync()
    {
        if (!await loader.ActivateAsync())
        {
            return;
        }
        activeVehicle.Changed += VehicleChanged;
        await RefreshAsync();
    }

    /// <inheritdoc />
    public override async Task DeactivateAsync()
    {
        if (DiscoveryOwnedByPage)
        {
            return;
        }
        activeVehicle.Changed -= VehicleChanged;
        await loader.DeactivateAsync();
    }

    /// <summary>Updates match evidence without performing another device scan.</summary>
    public void SetCatalogue(IReadOnlyList<FirmwareManifestEntry> catalogue)
    {
        entries = catalogue;
        RebuildChoices();
    }

    /// <summary>Scans serial devices using the platform's existing device catalogue.</summary>
    [RelayCommand]
    public Task RefreshAsync(CancellationToken cancellationToken = default)
    {
        return InstallationRunning || activeVehicle.IsOnline
            ? Task.CompletedTask
            : loader.RunAsync(async token =>
        {
            if (InstallationRunning || activeVehicle.IsOnline)
            {
                return;
            }
            try
            {
                IsRefreshing = true;
                DeviceStatus = "Looking for flight controllers…";
                var devices = await Task.Run(() => deviceCatalog.GetDevicesAsync(token), token);
                devices = await identityService.EnrichAsync(devices, cancellationToken: token);
                await Dispatcher.DispatchAsync(() =>
                {
                    if (token.IsCancellationRequested)
                    {
                        return;
                    }
                    Descriptors = devices;
                    RebuildChoices();
                    DevicesChanged?.Invoke(devices);
                });
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested) { }
            catch (Exception exception)
            {
                Logger.LogWarning(exception, "Firmware serial-device discovery failed.");
                await Dispatcher.DispatchAsync(() =>
                {
                    if (!token.IsCancellationRequested)
                    {
                        DeviceStatus = "Unable to enumerate serial devices. Check platform support and permissions, then refresh.";
                    }
                });
            }
            finally { await Dispatcher.DispatchAsync(() => IsRefreshing = false); }
        }, cancellationToken);
    }

    private void RebuildChoices()
    {
        var port = SelectedDevice?.Descriptor.PortName;
        DetectedDevices = CreateItems(entries, Descriptors);
        var retained = DetectedDevices.FirstOrDefault(item => string.Equals(item.Descriptor.PortName, port, StringComparison.OrdinalIgnoreCase));
        if (retained is not null)
        {
            SelectedDevice = retained;
            DeviceStatus = $"SelectedFirmwareModel device: {retained}";
        }
        else
        {
            SelectRecommendedDevice();
        }
    }

    private void VehicleChanged(ActiveVehicleChangedEventArgs args)
    {
        Dispatcher.Dispatch(() =>
    {
        if (!loader.IsActive)
        {
            return;
        }
        if (args.Current.IsOnline)
        {
            loader.Cancel();
        }
        else
        {
            _ = RefreshAsync();
        }
    });
    }

    /// <inheritdoc />
    public override void Dispose()
    {
        _ = DeactivateAsync();
        base.Dispose();
    }
}
