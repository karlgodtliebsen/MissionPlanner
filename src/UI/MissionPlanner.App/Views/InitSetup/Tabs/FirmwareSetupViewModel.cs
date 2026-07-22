using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using MissionPlanner.App.Presentation;
using MissionPlanner.Core.Firmware;
using MissionPlanner.Core.Setup;
using MissionPlanner.Core.Vehicles;
using MissionPlanner.Core.Vehicles.Abstractions;
using MissionPlanner.Core.Vehicles.Models;

namespace MissionPlanner.App.Views.InitSetup.Tabs;

/// <summary>Presents firmware identity and guarded discovery, verification, and flashing actions.</summary>
public sealed partial class FirmwareSetupViewModel : SetupWorkflowDetailViewModel
{
    private readonly IActiveVehicleContext activeVehicle;
    private readonly IFirmwareUpdateCoordinator coordinator;
    private readonly IFirmwareFlashingService flashingService;
    private readonly IUserConfirmationService confirmation;
    private readonly IDispatcher dispatcher;
    private readonly ILogger<FirmwareSetupViewModel> logger;
    private CancellationTokenSource? operationCancellation;

    /// <summary>Initializes the firmware setup workflow.</summary>
    /// <param name="descriptor">The firmware workflow descriptor.</param>
    /// <param name="activeVehicle">The active-vehicle context.</param>
    /// <param name="coordinator">The guarded update coordinator.</param>
    /// <param name="flashingService">The platform flashing adapter.</param>
    /// <param name="confirmation">The shared confirmation service.</param>
    /// <param name="dispatcher">The UI dispatcher.</param>
    /// <param name="logger">The logger.</param>
    public FirmwareSetupViewModel(
        SetupWorkflowDescriptor descriptor,
        IActiveVehicleContext activeVehicle,
        IFirmwareUpdateCoordinator coordinator,
        IFirmwareFlashingService flashingService,
        IUserConfirmationService confirmation,
        IDispatcher dispatcher,
        ILogger<FirmwareSetupViewModel> logger)
        : base(descriptor)
    {
        this.activeVehicle = activeVehicle;
        this.coordinator = coordinator;
        this.flashingService = flashingService;
        this.confirmation = confirmation;
        this.dispatcher = dispatcher;
        this.logger = logger;
        coordinator.StateChanged += OnStateChanged;
        UpdateVehicle(activeVehicle.State);
    }

    /// <summary>Gets the available release channels.</summary>
    public IReadOnlyList<FirmwareReleaseChannel> Channels { get; } = Enum.GetValues<FirmwareReleaseChannel>();

    /// <summary>Gets compatible manifest releases.</summary>
    public ObservableCollection<FirmwareManifestEntry> Releases { get; } = [];

    /// <summary>Gets or sets the selected release channel.</summary>
    [ObservableProperty]
    public partial FirmwareReleaseChannel SelectedChannel { get; set; } = FirmwareReleaseChannel.Stable;

    /// <summary>Gets or sets the selected compatible release.</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(DownloadCommand))]
    public partial FirmwareManifestEntry? SelectedRelease { get; set; }

    /// <summary>Gets the manifest-provided technical board target for the selected release.</summary>
    public string SelectedBoardTarget => SelectedRelease?.BoardTarget ?? "No compatible release selected";

    /// <summary>Gets release notes for the selected release.</summary>
    public string SelectedReleaseNotes => SelectedRelease?.ReleaseNotes ?? "No release notes available.";

    /// <summary>Gets the derived vehicle label.</summary>
    [ObservableProperty]
    public partial string VehicleLabel { get; private set; } = "No vehicle";

    /// <summary>Gets the formatted firmware family and version.</summary>
    [ObservableProperty]
    public partial string FirmwareVersion { get; private set; } = "Unknown";

    /// <summary>Gets the firmware release type.</summary>
    [ObservableProperty]
    public partial string ReleaseType { get; private set; } = "Unknown";

    /// <summary>Gets the flight firmware Git hash.</summary>
    [ObservableProperty]
    public partial string GitHash { get; private set; } = "Not reported";

    /// <summary>Gets the board version.</summary>
    [ObservableProperty]
    public partial string BoardVersion { get; private set; } = "Not reported";

    /// <summary>Gets vendor and product identifiers.</summary>
    [ObservableProperty]
    public partial string VendorProduct { get; private set; } = "Not reported";

    /// <summary>Gets the legacy hardware UID.</summary>
    [ObservableProperty]
    public partial string HardwareUid { get; private set; } = "Not reported";

    /// <summary>Gets the extended hardware UID.</summary>
    [ObservableProperty]
    public partial string HardwareUid2 { get; private set; } = "Not reported";

    /// <summary>Gets the reported MAVLink version.</summary>
    [ObservableProperty]
    public partial string MavLinkVersion { get; private set; } = "Unknown";

    /// <summary>Gets named and raw MAVLink capability flags.</summary>
    [ObservableProperty]
    public partial string Capabilities { get; private set; } = "None";

    /// <summary>Gets platform flashing availability.</summary>
    [ObservableProperty]
    public partial string FlashingAvailability { get; private set; } = "No platform adapter available.";

    /// <summary>Gets the firmware workflow status.</summary>
    [ObservableProperty]
    public partial string Status { get; private set; } = "Identity is read-only until a compatible manifest release is selected.";

    /// <summary>Gets the firmware workflow state.</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(FlashCommand))]
    public partial FirmwareUpdateState UpdateState { get; private set; }

    partial void OnSelectedChannelChanged(FirmwareReleaseChannel value)
    {
        Cancel();
        coordinator.Reset();
        Releases.Clear();
        SelectedRelease = null;
        Status = "Discover releases for the selected channel.";
    }

    partial void OnSelectedReleaseChanged(FirmwareManifestEntry? value)
    {
        if (coordinator.Package?.Release != value)
        {
            coordinator.Reset();
        }

        OnPropertyChanged(nameof(SelectedBoardTarget));
        OnPropertyChanged(nameof(SelectedReleaseNotes));
    }

    /// <summary>Updates all read-only identity values from the current immutable vehicle state.</summary>
    /// <param name="state">The current vehicle state, or <see langword="null"/>.</param>
    public void UpdateVehicle(VehicleState? state)
    {
        if (state is null)
        {
            VehicleLabel = "No vehicle";
            FirmwareVersion = "Unknown";
            FlashingAvailability = "Connect a vehicle to evaluate platform support.";
            return;
        }

        var identity = state.Identity.Firmware;
        VehicleLabel = state.DisplayName;
        FirmwareVersion = VehicleFirmwareDisplayFormatter.Format(identity);
        ReleaseType = identity.FlightVersion?.ReleaseType.ToString() ?? "Unknown";
        GitHash = identity.FlightGitHash ?? "Not reported";
        BoardVersion = identity.BoardVersion == 0 ? "Not reported" : $"{identity.BoardVersion} (0x{identity.BoardVersion:X8})";
        VendorProduct = identity.VendorId == 0 || identity.ProductId == 0
            ? "Not reported"
            : $"VID 0x{identity.VendorId:X4} · PID 0x{identity.ProductId:X4}";
        HardwareUid = identity.HardwareUid?.ToString("X16", CultureInfo.InvariantCulture) ?? "Not reported";
        HardwareUid2 = identity.HardwareUid2 ?? "Not reported";
        MavLinkVersion = state.Identity.MavLinkVersion.ToString(CultureInfo.InvariantCulture);
        var names = Enum.GetValues<MavProtocolCapability>()
            .Where(value => value != MavProtocolCapability.None && identity.Supports(value))
            .Select(value => value.ToString());
        Capabilities = $"0x{identity.Capabilities:X16} ({string.Join(", ", names.DefaultIfEmpty("none named"))})";
        FlashingAvailability = flashingService.GetPlatformSupport(identity).Reason;
    }

    /// <inheritdoc />
    public override void Cancel()
    {
        operationCancellation?.Cancel();
        operationCancellation?.Dispose();
        operationCancellation = null;
    }

    /// <inheritdoc />
    public override void Dispose()
    {
        coordinator.StateChanged -= OnStateChanged;
        coordinator.Dispose();
        base.Dispose();
    }

    [RelayCommand]
    private async Task DiscoverAsync()
    {
        if (activeVehicle.State is not { } state || !activeVehicle.IsOnline)
        {
            Status = "Connect a vehicle before discovering firmware.";
            return;
        }

        var operationToken = StartOperation();
        try
        {
            var releases = await coordinator.DiscoverAsync(state.Identity.Firmware, SelectedChannel, operationToken);
            Releases.Clear();
            foreach (var release in releases)
            {
                Releases.Add(release);
            }

            SelectedRelease = Releases.FirstOrDefault();
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Firmware discovery failed.");
            Error = exception.Message;
        }
    }

    private bool CanDownload()
    {
        return SelectedRelease is not null && activeVehicle.IsOnline;
    }

    [RelayCommand(CanExecute = nameof(CanDownload))]
    private async Task DownloadAsync()
    {
        if (SelectedRelease is not { } release)
        {
            return;
        }

        var operationToken = StartOperation();
        try
        {
            await coordinator.PrepareAsync(release, operationToken);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Firmware package preparation failed.");
            Error = exception.Message;
        }
    }

    private bool CanFlash()
    {
        return UpdateState == FirmwareUpdateState.ReadyToFlash && activeVehicle.IsOnline;
    }

    [RelayCommand(CanExecute = nameof(CanFlash))]
    private async Task FlashAsync()
    {
        if (activeVehicle.State is not { } state)
        {
            return;
        }

        var backupConfirmed = await confirmation.ConfirmAsync(
            "Back up parameters",
            "Save a parameter backup before firmware flashing. Continue only when the backup is complete.",
            "Backup complete");
        if (!backupConfirmed)
        {
            return;
        }

        var flashConfirmed = await confirmation.ConfirmAsync(
            "Flash verified firmware",
            "Normal MAVLink will disconnect before the platform bootloader adapter starts. Do not remove power during flashing.",
            "Disconnect and flash");
        if (!flashConfirmed)
        {
            return;
        }

        var operationToken = StartOperation(false);
        var result = await coordinator.FlashAsync(state.VehicleId, state.Identity.Firmware, true, operationToken);
        if (!result.Succeeded)
        {
            Error = result.Message;
        }
    }

    private CancellationToken StartOperation(bool linkToConnection = true)
    {
        Cancel();
        operationCancellation = linkToConnection
            ? CancellationTokenSource.CreateLinkedTokenSource(activeVehicle.ConnectionCancellationToken)
            : new CancellationTokenSource();
        Error = null;
        return operationCancellation.Token;
    }

    private void OnStateChanged(object? sender, FirmwareUpdateStateChangedEventArgs args)
    {
        dispatcher.Dispatch(() =>
        {
            UpdateState = args.State;
            Status = args.Status;
            Progress = args.Progress;
        });
    }
}
