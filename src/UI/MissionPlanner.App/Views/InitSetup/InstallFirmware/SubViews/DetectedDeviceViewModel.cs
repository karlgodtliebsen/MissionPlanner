using System.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using MissionPlanner.App.Utilities.Dispatching;
using MissionPlanner.App.Views.InitSetup.InstallFirmware.SubViews.Models;
using MissionPlanner.Firmware.Model;
using MissionPlanner.Library.EventHub.Abstractions;

namespace MissionPlanner.App.Views.InitSetup.InstallFirmware.SubViews;

/// <summary>Owns devices panel state and commands.</summary>
public sealed partial class DetectedDeviceViewModel : ViewModelBase
{
    /// <summary>
    /// Initializes the devices panel.
    /// </summary>
    public DetectedDeviceViewModel(
        Firmware.Devices.IFirmwareSerialDeviceCatalog deviceCatalog,
        Firmware.Betaflight.IFirmwareDeviceIdentityService identityService,
        Core.Vehicles.Abstractions.IActiveVehicleContext activeVehicle,
        ILogger<DetectedDeviceViewModel> logger,
        IUiDispatcher dispatcher,
        IDomainEventHub eventHub) : base(logger, dispatcher, eventHub)
    {
        this.deviceCatalog = deviceCatalog;
        this.identityService = identityService;
        this.activeVehicle = activeVehicle;
    }
    /// <summary>
    /// Gets discovered serial devices.
    /// </summary>
    [ObservableProperty]
    public partial IReadOnlyList<FirmwareDeviceItemViewModel> DetectedDevices
    {
        get;
        set;
    } = [];

    [ObservableProperty]
    public partial FirmwareDeviceItemViewModel? SelectedDevice
    {
        get;
        set;
    }

    [ObservableProperty]
    public partial string DeviceStatus
    {
        get;
        set;
    } = "No flight controller detected";

    /// <summary>
    /// Gets whether a serial flight-controller device is selected.
    /// </summary>
    //public bool HasDevice => SelectedDevice is not null;
    public bool HasDevice => DetectedDevices.Any();

    /// <summary>
    /// Notifies the active parent about panel changes.
    /// </summary>
    public event Action<FirmwareDeviceItemViewModel?>? SelectionChanged;

    /// <summary>
    /// Notifies the active parent about panel changes.
    /// </summary>
    public event Action<FirmwarePanelRequest>? OperationRequested;

    [RelayCommand(CanExecute = nameof(CanInstall))]
    private Task InstallAsync(CancellationToken cancellationToken)
    {
        return FirmwarePanelRequest.SendAsync(OperationRequested, FirmwarePanelAction.Install, cancellationToken);
    }

    public void Reset()
    {
        CanInstall = false;
    }

    /// <summary>Discards cached runtime identity and probes the current devices again.</summary>
    [RelayCommand]
    private Task ReprobeAsync(CancellationToken cancellationToken)
    {
        identityService.Invalidate();
        return RefreshAsync(cancellationToken);
    }

    /// <summary>
    /// Gets whether the parent permits installation.
    /// </summary>
    [ObservableProperty, NotifyCanExecuteChangedFor(nameof(DetectedDeviceViewModel.InstallCommand))]
    public partial bool CanInstall
    {
        get; set;
    }

    partial void OnSelectedDeviceChanged(FirmwareDeviceItemViewModel? value)
    {
        OnPropertyChanged(nameof(HasDevice));
        SelectionChanged?.Invoke(value);
    }

    /// <summary>
    /// Ranks serial-device choices using catalogue USB and board hints.
    /// </summary>
    public static IReadOnlyList<FirmwareDeviceItemViewModel> CreateItems(IReadOnlyList<FirmwareManifestEntry> entries, IReadOnlyList<SerialDeviceDescriptor> devices)
    {
        Debug.Print("InstallFirmware CreateItems");

        var deviceItems = devices.Select(device =>
        {
            var usbMatch = entries.Any(entry => entry.Target.UsbIdentifiers.Contains(device.UsbIdentifier ?? default));
            var hintMatch = entries.Any(entry => entry.Target.BootloaderNames.Any(hint =>
                (
                    !string.IsNullOrWhiteSpace(device.ProductName)
                    && device.ProductName.Contains(hint, StringComparison.OrdinalIgnoreCase))
                ||
                device.BoardHints.Any(value => value.Contains(hint, StringComparison.OrdinalIgnoreCase)))
            );

            return new FirmwareDeviceItemViewModel(device, usbMatch || hintMatch, usbMatch ? "USB compatibility hint" : hintMatch ? "Product/board name hint" : "Manual device selection");
        }).ToArray();

        Debug.Print($"InstallFirmware CreateItems found {deviceItems.Length} items");
        return deviceItems;
    }

    /// <summary>Selects a unique recommended device, otherwise the first available device.</summary>
    public void SelectRecommendedDevice()
    {
        var recommendedDevices = DetectedDevices.Where(item => item.IsRecommended).ToArray();
        SelectedDevice = recommendedDevices.Length == 1 ? recommendedDevices[0] : DetectedDevices.FirstOrDefault();
        DeviceStatus = DetectedDevices.Count == 0
            ? "No flight controller detected"
            : recommendedDevices.Length == 1
                ? $"Recommended device: {SelectedDevice}"
                : $"Selected device: {SelectedDevice}. Check that this is the intended flight controller.";
    }
}
