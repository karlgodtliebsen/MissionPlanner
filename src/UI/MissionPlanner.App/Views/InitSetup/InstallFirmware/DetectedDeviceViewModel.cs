using System.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using MissionPlanner.App.Utilities.Dispatching;
using MissionPlanner.Firmware.Model;
using MissionPlanner.Library.EventHub.Abstractions;

namespace MissionPlanner.App.Views.InitSetup.InstallFirmware;

/// <summary>Owns devices panel state and commands.</summary>
public sealed partial class DetectedDeviceViewModel : ViewModelBase
{
    /// <summary>Initializes the devices panel.</summary>
    public DetectedDeviceViewModel(
        ILogger<DetectedDeviceViewModel> logger,
        IUiDispatcher dispatcher,
        IDomainEventHub eventHub) : base(logger, dispatcher, eventHub)
    {
    }
    /// <summary>Gets discovered serial devices.</summary>
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

    /// <summary>Gets whether a serial flight-controller device is selected.</summary>
    public bool HasDevice => SelectedDevice is not null;
    /// <summary>Notifies the active parent about panel changes.</summary>
    public event Action<FirmwareDeviceItemViewModel?>? SelectionChanged;
    /// <summary>Notifies the active parent about panel changes.</summary>
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

    /// <summary>Gets whether the parent permits installation.</summary>
    [ObservableProperty, NotifyCanExecuteChangedFor(nameof(InstallCommand))]
    public partial bool CanInstall
    {
        get; set;
    }

    partial void OnSelectedDeviceChanged(FirmwareDeviceItemViewModel? value)
    {
        OnPropertyChanged(nameof(HasDevice));
        SelectionChanged?.Invoke(value);
    }

    /// <summary>Ranks serial-device choices using catalogue USB and board hints.</summary>
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

            return new FirmwareDeviceItemViewModel(device, usbMatch || hintMatch, usbMatch ? "Exact catalogue USB match" : hintMatch ? "Bootloader/board hint match" : "Manual device selection");
        }).ToArray();

        Debug.Print($"InstallFirmware CreateItems found {deviceItems.Length} items");
        return deviceItems;
    }

    /// <summary>Selects only an unambiguous device and explains ambiguous or absent devices.</summary>
    public void SelectRecommendedDevice()
    {
        var recommendedDevices = DetectedDevices.Where(item => item.IsRecommended).ToArray();
        SelectedDevice = recommendedDevices.Length == 1 ? recommendedDevices[0] : null;
        DeviceStatus = DetectedDevices.Count == 0
            ? "No flight controller detected"
            : recommendedDevices.Length > 1
                ? "Multiple matching devices detected; select the exact flight controller."
                : SelectedDevice is not null
                    ? $"Recommended device: {SelectedDevice}"
                    : "Select the flight controller explicitly.";
    }
}
