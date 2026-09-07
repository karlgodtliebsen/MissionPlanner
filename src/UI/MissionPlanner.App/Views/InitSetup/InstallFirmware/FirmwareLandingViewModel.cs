using System.ComponentModel;
using Microsoft.Extensions.Logging;
using MissionPlanner.App.Utilities.Dispatching;
using MissionPlanner.App.Views.InitSetup.InstallFirmware.SubViews;
using MissionPlanner.Core.Vehicles;
using MissionPlanner.Core.Vehicles.Abstractions;
using MissionPlanner.Library.EventHub.Abstractions;

namespace MissionPlanner.App.Views.InitSetup.InstallFirmware;

/// <summary>Explains the current firmware connection and discovery state without starting another scan.</summary>
public sealed class FirmwareLandingViewModel : ViewModelBase
{
    private readonly IActiveVehicleContext vehicle;
    private readonly DetectedDeviceViewModel devices;
    private readonly STM32BootloaderViewModel dfu;
    private bool active;

    /// <summary>Initializes the information panel using the shared device discovery models.</summary>
    public FirmwareLandingViewModel(IActiveVehicleContext vehicle, DetectedDeviceViewModel devices,
        STM32BootloaderViewModel dfu, ILogger<FirmwareLandingViewModel> logger,
        IUiDispatcher dispatcher, IDomainEventHub eventHub) : base(logger, dispatcher, eventHub)
    {
        this.vehicle = vehicle;
        this.devices = devices;
        this.dfu = dfu;
    }

    /// <summary>Gets the live vehicle connection summary.</summary>
    public string ConnectionSummary => vehicle.IsOnline ? "Vehicle connected" : "No vehicle connected";

    /// <summary>Explains why device detection differs from a telemetry connection.</summary>
    public string ConnectionDetail => vehicle.IsOnline
        ? "Mission Planner has an active vehicle connection. Disconnect it before installing application firmware."
        : "A controller can be detected over USB without an active telemetry connection. You do not need to connect to the vehicle to install firmware.";

    /// <summary>Gets the serial discovery summary, including candidate port names.</summary>
    public string SerialSummary => vehicle.IsOnline ? "Discovery paused while connected"
        : devices.IsRefreshing ? "Looking for serial devices…"
        : devices.Descriptors.Count == 0 ? "No serial devices detected"
        : "Detected serial ports: " + string.Join(", ", devices.Descriptors.Select(device => device.PortName));

    /// <summary>Gets the latest serial discovery diagnostic.</summary>
    public string SerialDetail => vehicle.IsOnline
        ? "Disconnect the vehicle to refresh the available devices."
        : $"{devices.DeviceStatus}\nA detected serial port is a candidate; firmware compatibility is checked before installation.";

    /// <summary>Gets the DFU discovery summary.</summary>
    public string DfuSummary => vehicle.IsOnline ? "Discovery paused while connected"
        : dfu.IsRefreshing ? "Looking for STM32 DFU devices…"
        : dfu.DfuDevices.Count == 0 ? "No DFU devices detected"
        : string.Join("\n", dfu.DfuDevices.Select(device => device.ToString()));

    /// <summary>Gets DFU tool and driver readiness guidance.</summary>
    public string DfuDetail => vehicle.IsOnline
        ? "DFU discovery resumes after disconnection."
        : $"{dfu.DfuStatus}\nDFU is the controller's USB bootloader mode, not a vehicle telemetry connection.";

    /// <summary>Gets the next action appropriate to the current connection and discovery state.</summary>
    public string NextStep => !OperatingSystem.IsWindows()
        ? "Direct firmware installation is not available on this platform. Use the Windows desktop app."
        : devices.InstallationRunning || dfu.InstallationRunning
            ? "A firmware operation is in progress. Follow its progress dialog and keep the controller powered."
        : vehicle.IsOnline
            ? "Disconnect the vehicle first. Catalogue, Custom Firmware and STM32 Bootloader are disabled while connected."
        : devices.IsRefreshing || dfu.IsRefreshing
            ? "Discovery is running. Available firmware workflows will update when the scan finishes."
        : dfu.DfuDevices.Count > 0
            ? "Open STM32 Bootloader. A DFU device was detected, so Catalogue and Custom Firmware are disabled. Check the driver and programmer status in that tab before continuing."
        : devices.Descriptors.Count > 0
            ? "Choose firmware from Catalogue, or open Custom Firmware to use a local file. Select the correct controller port. STM32 Bootloader is disabled because no DFU device was detected."
        : "Attach a controller by USB, then select Refresh devices above. To use STM32 Bootloader, put the board into DFU mode first. Firmware tabs remain disabled until a device is detected.";

    /// <inheritdoc />
    public override Task ActivateAsync()
    {
        if (!active)
        {
            active = true;
            vehicle.Changed += VehicleChanged;
            devices.PropertyChanged += DiscoveryChanged;
            dfu.PropertyChanged += DiscoveryChanged;
        }
        NotifyStatus();
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public override Task DeactivateAsync()
    {
        active = false;
        vehicle.Changed -= VehicleChanged;
        devices.PropertyChanged -= DiscoveryChanged;
        dfu.PropertyChanged -= DiscoveryChanged;
        return Task.CompletedTask;
    }

    private void VehicleChanged(ActiveVehicleChangedEventArgs args) => NotifyStatus();
    private void DiscoveryChanged(object? sender, PropertyChangedEventArgs args) => NotifyStatus();
    private void NotifyStatus() => Dispatcher.Dispatch(() =>
    {
        if (active)
        {
            OnPropertyChanged(string.Empty);
        }
    });

    /// <inheritdoc />
    public override void Dispose()
    {
        _ = DeactivateAsync();
        base.Dispose();
    }
}
