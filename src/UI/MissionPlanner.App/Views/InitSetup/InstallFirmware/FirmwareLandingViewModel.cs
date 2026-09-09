using System.ComponentModel;
using AsyncAwaitBestPractices;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using MissionPlanner.App.Utilities.Dispatching;
using MissionPlanner.App.Views.InitSetup.InstallFirmware.SubViews;
using MissionPlanner.Core.Vehicles;
using MissionPlanner.Core.Vehicles.Abstractions;
using MissionPlanner.Library.EventHub.Abstractions;

namespace MissionPlanner.App.Views.InitSetup.InstallFirmware;

/// <summary>Explains the current firmware connection and discovery state without starting another scan.</summary>
public sealed partial class FirmwareLandingViewModel : ViewModelBase
{
    private readonly IActiveVehicleContext vehicle;
    private readonly DetectedDeviceViewModel devices;
    private readonly STM32BootloaderViewModel dfu;
    private bool active;

    /// <summary>Gets the shared controller selection.</summary>
    public DetectedDeviceViewModel Devices => devices;

    /// <summary>Gets the existing DFU device selection and tool readiness.</summary>
    public STM32BootloaderViewModel Dfu => dfu;

    /// <summary>Gets protocol-reported identity without inferring an ArduPilot target.</summary>
    public string SourceIdentitySummary => (dfu.HasCorrelatedSource ? dfu.CorrelatedHandoff?.Source.BetaflightIdentity
        : devices.SelectedDevice?.Descriptor.BetaflightIdentity) is { } identity
        ? $"Firmware: {identity.FirmwareVariant} {identity.FirmwareVersion}\nBoard: {identity.Board?.BoardName ?? "Unknown"}\nTarget: {identity.Board?.TargetName ?? "Unknown"}\nManufacturer: {identity.Board?.ManufacturerId ?? "Unknown"}\nMCU: {identity.McuType ?? "Unknown"}\nUID: {identity.McuUniqueId ?? "Unavailable"}"
        : "Runtime firmware identity is unknown. A serial port name does not identify the controller firmware or exact board.";

    /// <summary>Explains the limits of anonymous ROM USB identity.</summary>
    public string DfuIdentitySummary => dfu.SelectedDfuDevice is { } selected
        ? $"USB {selected.Descriptor.VendorId:X4}:{selected.Descriptor.ProductId:X4}\nUSB serial: {selected.Descriptor.SerialNumber ?? "Unknown"}\nPhysical device: {selected.Descriptor.PnpInstanceId ?? selected.Descriptor.DevicePath ?? selected.Descriptor.ProviderId}\n"
            + (dfu.HasCorrelatedSource ? "Physical handoff: matched. Source identity is preserved; review the exact ArduPilot target before installation."
                : "No preceding controller identity is available. The exact flight-controller target cannot be inferred from STM32 DFU alone.")
        : "Hold BOOT/DFU while reconnecting USB; some boards require BOOT + RESET. STM32 ROM DFU is a USB endpoint, normally not a COM port. Refresh after changing mode.";

    /// <summary>Requests a page-owned boot-mode operation.</summary>
    public event Action<FirmwarePanelRequest>? OperationRequested;

    /// <summary>Gets whether the selected disconnected controller supports the Betaflight reboot workflow.</summary>
    public bool CanRebootToDfu => active && OperatingSystem.IsWindows() && !vehicle.IsOnline
        && !devices.InstallationRunning && !dfu.InstallationRunning
        && !devices.IsRefreshing && !dfu.IsRefreshing
        && devices.SelectedDevice is not null;

    /// <summary>Explains the software reboot capability and manual alternative.</summary>
    public string DfuRebootGuidance => !OperatingSystem.IsWindows()
        ? "Software DFU reboot requires the Windows desktop app."
        : vehicle.IsOnline ? "Disconnect the vehicle before requesting DFU mode."
        : devices.InstallationRunning || dfu.InstallationRunning ? "Wait for the current firmware operation to finish."
        : devices.IsRefreshing || dfu.IsRefreshing ? "Wait for device discovery to finish."
        : devices.SelectedDevice is null ? "Select a controller port to check for software DFU reboot support."
        : "Reboot to DFU first verifies the selected port as a Betaflight controller. You will be asked to confirm before it reboots. Other controllers require the board's BOOT/RESET procedure. No firmware is flashed.";

    [RelayCommand(CanExecute = nameof(CanRebootToDfu))]
    private Task RebootToDfuAsync(CancellationToken cancellationToken)
    {
        return FirmwarePanelRequest.SendAsync(OperationRequested, FirmwarePanelAction.RebootToDfu, cancellationToken);
    }

    /// <summary>Initializes the information panel using the shared device discovery models.</summary>
    public FirmwareLandingViewModel(IActiveVehicleContext vehicle, DetectedDeviceViewModel devices,
        STM32BootloaderViewModel dfu,
        IUiDispatcher dispatcher, IDomainEventHub eventHub, ILogger<FirmwareLandingViewModel> logger) : base(logger, dispatcher, eventHub)
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
        RebootToDfuCommand.NotifyCanExecuteChanged();
        return Task.CompletedTask;
    }

    private void VehicleChanged(ActiveVehicleChangedEventArgs args)
    {
        NotifyStatus();
    }

    private void DiscoveryChanged(object? sender, PropertyChangedEventArgs args)
    {
        NotifyStatus();
    }

    private void NotifyStatus()
    {
        Dispatcher.Dispatch(() =>
        {
            if (active)
            {
                OnPropertyChanged(string.Empty);
                RebootToDfuCommand.NotifyCanExecuteChanged();
            }
        });
    }

    /// <inheritdoc />
    public override void Dispose()
    {
        DeactivateAsync().SafeFireAndForget();
        base.Dispose();
    }
}
