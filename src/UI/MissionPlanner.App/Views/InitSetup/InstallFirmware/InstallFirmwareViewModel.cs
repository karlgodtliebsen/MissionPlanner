using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using MissionPlanner.App.Presentation;
using MissionPlanner.Core.Vehicles.Abstractions;
using MissionPlanner.Firmware.Catalog;
using MissionPlanner.Firmware.Connected;
using MissionPlanner.Firmware.Devices;
using MissionPlanner.Firmware.Discovery;
using MissionPlanner.Firmware.Entry;
using MissionPlanner.Firmware.Images;
using MissionPlanner.Firmware.Installation;
using MissionPlanner.Firmware.Model;
using MissionPlanner.Firmware.Presentation;
using UraniumUI.Extensions;

namespace MissionPlanner.App.Views.InitSetup.InstallFirmware;

/// <summary>Drives connected and disconnected firmware installation experiences.</summary>
public sealed partial class InstallFirmwareViewModel : ObservableObject, IDisposable
{
    private readonly IFirmwareCatalogService catalogService;
    private readonly IFirmwareInstallationService installationService;
    private readonly IEmbeddedBootloaderUpdateService bootloaderUpdateService;
    private readonly IFirmwareSerialDeviceCatalog deviceCatalog;
    private readonly IFirmwarePageModeResolver modeResolver;
    private readonly IFirmwarePackageReader packageReader;
    private readonly IFirmwareFilePicker filePicker;
    private readonly IActiveVehicleContext activeVehicle;
    private readonly ILogger<InstallFirmwareViewModel> logger;
    private readonly IUserConfirmationService confirmation;
    private readonly IDispatcher dispatcher;
    private CancellationTokenSource? lifetime;
    private int operationRunning;

    /// <summary>Initializes the firmware page.</summary>
    public InstallFirmwareViewModel(
        IFirmwareCatalogService catalogService,
        IFirmwareInstallationService installationService,
        IEmbeddedBootloaderUpdateService bootloaderUpdateService,
        IFirmwareSerialDeviceCatalog deviceCatalog,
        IFirmwarePageModeResolver modeResolver,
        IFirmwarePackageReader packageReader,
        IFirmwareFilePicker filePicker,
        IActiveVehicleContext activeVehicle,
        IUserConfirmationService confirmation,
        IDispatcher dispatcher,
        ILogger<InstallFirmwareViewModel> logger)
    {
        this.catalogService = catalogService;
        this.installationService = installationService;
        this.bootloaderUpdateService = bootloaderUpdateService;
        this.deviceCatalog = deviceCatalog;
        this.modeResolver = modeResolver;
        this.packageReader = packageReader;
        this.filePicker = filePicker;
        this.activeVehicle = activeVehicle;
        this.confirmation = confirmation;
        this.dispatcher = dispatcher;
        this.logger = logger;
        ActivateAsync().FireAndForget();
        ApplyMode();
    }

    /// <summary>Gets catalogue choices.</summary>
    public ObservableCollection<FirmwareCatalogItemViewModel> FirmwareChoices { get; } = [];

    /// <summary>Gets discovered serial devices.</summary>
    public ObservableCollection<string> Devices { get; } = [];

    /// <summary>Gets release channels.</summary>
    public IReadOnlyList<FirmwareReleaseChannel> Channels { get; } =
        [FirmwareReleaseChannel.Stable, FirmwareReleaseChannel.Beta, FirmwareReleaseChannel.Latest];

    /// <summary>Gets operation progress.</summary>
    public FirmwareProgressViewModel OperationProgress { get; } = new();

    [ObservableProperty] public partial FirmwareReleaseChannel SelectedChannel { get; set; } = FirmwareReleaseChannel.Stable;
    [ObservableProperty] public partial FirmwareCatalogItemViewModel? SelectedFirmware { get; set; }
    [ObservableProperty] public partial ApjFirmwarePackage? CustomPackage { get; private set; }
    [ObservableProperty] public partial string? CustomFirmwareName { get; private set; }
    [ObservableProperty] public partial string? CustomFirmwareDescription { get; private set; }
    [ObservableProperty] public partial string? CustomFirmwarePlatform { get; private set; }
    [ObservableProperty] public partial string? CustomFirmwareBuild { get; private set; }
    [ObservableProperty] public partial int CustomFirmwareBoardId { get; private set; }
    [ObservableProperty] public partial long CustomFirmwareImageSize { get; private set; }

    /// <summary>Gets whether parsed custom metadata is available.</summary>
    public bool HasCustomFirmware => CustomPackage is not null;

    [ObservableProperty] public partial bool IsConnectedMode { get; private set; }
    [ObservableProperty] public partial bool IsDisconnectedMode { get; private set; }
    [ObservableProperty] public partial bool IsUnsupportedMode { get; private set; }
    [ObservableProperty] public partial bool IsOperationInProgress { get; private set; }

    /// <summary>Gets whether Shell navigation may safely leave this page.</summary>
    public bool CanNavigateAway => !IsOperationInProgress;

    [ObservableProperty] public partial bool CanUpdateBootloader { get; private set; }
    [ObservableProperty] public partial bool CanInstall { get; private set; }
    [ObservableProperty] public partial string StatusMessage { get; private set; } = "Ready";
    [ObservableProperty] public partial string DeviceStatus { get; private set; } = "No flight controller detected";
    [ObservableProperty] public partial string? LastDiagnosticReport { get; private set; }

    /// <summary>Gets whether a terminal diagnostic report can be copied.</summary>
    public bool HasDiagnosticReport => !string.IsNullOrWhiteSpace(LastDiagnosticReport);

    /// <summary>Starts observing connection state and refreshes disconnected data.</summary>
    private async Task ActivateAsync()
    {
        if (lifetime is not null)
        {
            return;
        }

        lifetime = new CancellationTokenSource();
        activeVehicle.Changed += OnActiveVehicleChanged;
        StatusMessage = "Ready";
        OperationProgress.Stage = "Ready";
        OperationProgress.Progress = 0;
        OperationProgress.HasPercentage = false;
        OperationProgress.IsPowerCritical = false;
        OperationProgress.TechnicalDetail = null;
        LastDiagnosticReport = null;
        OnPropertyChanged(nameof(HasDiagnosticReport));
        ApplyMode();
        if (IsDisconnectedMode)
        {
            await RefreshAsync(false, lifetime.Token);
        }
    }

    /// <summary>Stops page-owned observation without cancelling an unsafe firmware operation.</summary>
    private void Deactivate()
    {
        activeVehicle.Changed -= OnActiveVehicleChanged;
        if (!IsOperationInProgress)
        {
            lifetime?.Cancel();
        }

        lifetime?.Dispose();
        lifetime = null;
    }

    partial void OnSelectedChannelChanged(FirmwareReleaseChannel value)
    {
        if (lifetime is not null && IsDisconnectedMode)
        {
            _ = RefreshAsync(false, lifetime.Token);
        }
    }

    [RelayCommand]
    private Task RefreshCatalogAsync()
    {
        return RefreshAsync(true, lifetime?.Token ?? CancellationToken.None);
    }

    [RelayCommand]
    private Task ShowAllOptionsAsync()
    {
        return RefreshAsync(true, lifetime?.Token ?? CancellationToken.None, true);
    }

    [RelayCommand]
    private void SelectFirmware(FirmwareCatalogItemViewModel item)
    {
        SelectedFirmware = item;
        CustomPackage = null;
        OnPropertyChanged(nameof(HasCustomFirmware));
        InstallCommand.NotifyCanExecuteChanged();
    }

    [RelayCommand]
    private async Task LoadCustomFirmwareAsync(CancellationToken cancellationToken)
    {
        try
        {
            var file = await filePicker.PickAsync(cancellationToken);
            if (file is null)
            {
                return;
            }

            var extension = Path.GetExtension(file.FileName);
            if (extension.Equals(".hex", StringComparison.OrdinalIgnoreCase))
            {
                throw new NotSupportedException(".hex firmware requires a future DFU/legacy workflow. Select a GCS-loadable .apj or .px4 package.");
            }

            if (!extension.Equals(".apj", StringComparison.OrdinalIgnoreCase) && !extension.Equals(".px4", StringComparison.OrdinalIgnoreCase))
            {
                throw new NotSupportedException("Only .apj and .px4 firmware packages are supported by the modern bootloader workflow.");
            }

            await using var stream = await file.OpenReadAsync(cancellationToken);
            var package = await packageReader.ReadAsync(stream, cancellationToken);
            CustomPackage = package;
            CustomFirmwareName = file.FileName;
            CustomFirmwareDescription = package.Description ?? "Custom ArduPilot firmware";
            CustomFirmwarePlatform = package.Summary ?? "Platform declared by board ID";
            CustomFirmwareBuild = package.Version ?? package.GitIdentity ?? "Unknown build";
            CustomFirmwareBoardId = package.BoardId;
            CustomFirmwareImageSize = package.Image.Length;
            SelectedFirmware = null;
            OnPropertyChanged(nameof(HasCustomFirmware));
            InstallCommand.NotifyCanExecuteChanged();
            StatusMessage = "Custom firmware parsed and validated. Connect the target in bootloader mode to install.";
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Custom firmware selection failed.");
            CustomPackage = null;
            OnPropertyChanged(nameof(HasCustomFirmware));
            InstallCommand.NotifyCanExecuteChanged();
            StatusMessage = exception.Message;
        }
    }

    [RelayCommand(CanExecute = nameof(CanStartInstall), AllowConcurrentExecutions = false)]
    private async Task InstallAsync(CancellationToken cancellationToken)
    {
        if ((SelectedFirmware is null && CustomPackage is null) || Interlocked.CompareExchange(ref operationRunning, 1, 0) != 0)
        {
            return;
        }

        try
        {
            SetOperation(true, FirmwareOperationState.Downloading);
            var target = SelectedFirmware?.Entry.Target;
            var request = new FirmwareInstallationRequest(
                new BootloaderEntryContext(new BootloaderDiscoveryRequest(
                    ExpectedUsbIdentifiers: target?.UsbIdentifiers,
                    BootloaderHints: target?.BootloaderNames)),
                SelectedFirmware?.Entry.Artifact,
                CustomPackage);
            var progress = new Progress<FirmwareProgress>(UpdateProgress);
            var result = await installationService.InstallAsync(request, progress, cancellationToken);
            LastDiagnosticReport = result.DiagnosticReport?.CreateReport();
            OnPropertyChanged(nameof(HasDiagnosticReport));
            StatusMessage = result.State == FirmwareOperationState.Completed
                ? result.ApplicationDevice is null
                    ? "Firmware installation completed; reconnect was not detected. Reconnect the flight controller manually."
                    : $"Firmware installation completed. ArduPilot returned on {result.ApplicationDevice.PortName}; reconnect is available."
                : $"Firmware installation {result.State}";
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Firmware installation failed.");
            StatusMessage = exception.Message;
        }
        finally
        {
            Interlocked.Exchange(ref operationRunning, 0);
            SetOperation(false, null);
        }
    }

    private bool CanStartInstall()
    {
        return CanInstall && (SelectedFirmware is not null || CustomPackage is not null) && !IsOperationInProgress;
    }

    [RelayCommand]
    private Task CopyDiagnosticReportAsync()
    {
        return string.IsNullOrWhiteSpace(LastDiagnosticReport)
            ? Task.CompletedTask
            : Clipboard.Default.SetTextAsync(LastDiagnosticReport);
    }

    [RelayCommand(CanExecute = nameof(CanStartBootloaderUpdate), AllowConcurrentExecutions = false)]
    private async Task UpdateBootloaderAsync(CancellationToken cancellationToken)
    {
        if (Interlocked.CompareExchange(ref operationRunning, 1, 0) != 0)
        {
            return;
        }

        try
        {
            var accepted = await confirmation.ConfirmAsync(
                "Update embedded bootloader",
                "This writes the bootloader stored inside the connected flight controller. The vehicle must remain disarmed and powered. Reboot is required after the command is accepted.",
                "Update Bootloader",
                cancellationToken);
            if (!accepted)
            {
                return;
            }

            SetOperation(true, FirmwareOperationState.Programming);
            var result = await bootloaderUpdateService.UpdateAsync(new BootloaderUpdateRequest(true), cancellationToken);
            StatusMessage = result.Code + (result.RebootRequired ? " — reboot the flight controller to use the new bootloader." : string.Empty);
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Embedded bootloader update failed.");
            StatusMessage = exception.Message;
        }
        finally
        {
            Interlocked.Exchange(ref operationRunning, 0);
            SetOperation(false, null);
        }
    }

    private bool CanStartBootloaderUpdate()
    {
        return CanUpdateBootloader && !IsOperationInProgress;
    }

    private async Task RefreshAsync(bool forceRefresh, CancellationToken cancellationToken, bool allOptions = false)
    {
        if (!IsDisconnectedMode || IsOperationInProgress)
        {
            return;
        }

        try
        {
            StatusMessage = "Loading firmware catalogue…";
            var catalog = await catalogService.GetCatalogAsync(
                new FirmwareCatalogRequest(Channel: allOptions ? null : SelectedChannel, ForceRefresh: forceRefresh),
                cancellationToken);
            var devices = await deviceCatalog.GetDevicesAsync(cancellationToken);
            var choices = catalog.Entries
                .Where(entry => entry.Target.VehicleType != FirmwareVehicleType.Unknown &&
                                entry.Artifact.Format is FirmwareImageFormat.Apj or FirmwareImageFormat.Px4)
                .GroupBy(entry => allOptions ? $"{entry.Target.VehicleType}:{entry.Target.Platform}:{entry.Target.BoardId}" : entry.Target.VehicleType.ToString())
                .Select(group => group.FirstOrDefault(entry =>
                    entry.Target.UsbIdentifiers.Any(expected => devices.Any(device => device.UsbIdentifier == expected))) ?? group.First())
                .OrderBy(item => item.Target.VehicleType).ThenBy(item => item.Target.Platform)
                .Select(item => new FirmwareCatalogItemViewModel(item))
                .ToArray();
            FirmwareChoices.Clear();
            foreach (var choice in choices)
            {
                FirmwareChoices.Add(choice);
            }

            SelectedFirmware = choices.FirstOrDefault();
            CustomPackage = null;
            OnPropertyChanged(nameof(HasCustomFirmware));

            Devices.Clear();
            foreach (var device in devices)
            {
                Devices.Add($"{device.PortName} · {device.ProductName ?? "Serial device"} · {device.UsbIdentifier?.ToString() ?? "USB identity unavailable"}");
            }

            DeviceStatus = Devices.Count == 0 ? "No flight controller detected" : string.Join(Environment.NewLine, Devices);
            StatusMessage = catalog.IsStale ? "Showing cached firmware catalogue" : $"{FirmwareChoices.Count} vehicle firmware choices available";
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { }
        catch (Exception exception)
        {
            logger.LogError(exception, "Firmware catalogue refresh failed.");
            StatusMessage = exception.Message;
        }
    }

    private void OnActiveVehicleChanged(object? sender, Core.Vehicles.ActiveVehicleChangedEventArgs e)
    {
        dispatcher.Dispatch(() => ApplyMode());
    }

    private void SetOperation(bool active, FirmwareOperationState? stage)
    {
        IsOperationInProgress = active;
        OnPropertyChanged(nameof(CanNavigateAway));
        ApplyMode(stage);
        InstallCommand.NotifyCanExecuteChanged();
        UpdateBootloaderCommand.NotifyCanExecuteChanged();
    }

    private void ApplyMode(FirmwareOperationState? stage = null)
    {
        var state = modeResolver.Resolve(new FirmwarePageContext(
            OperatingSystem.IsWindows(),
            activeVehicle.IsOnline,
            activeVehicle.State?.IsArmed == true,
            activeVehicle.State is not null && activeVehicle.State.Identity.Firmware.Family != Core.Vehicles.Models.FirmwareFamily.Unknown,
            IsOperationInProgress,
            stage));
        IsConnectedMode = state.Mode == FirmwarePageMode.Connected;
        IsDisconnectedMode = state.Mode == FirmwarePageMode.Disconnected;
        IsUnsupportedMode = state.Mode == FirmwarePageMode.UnsupportedPlatform;
        CanInstall = state.CanInstallApplicationFirmware;
        CanUpdateBootloader = state.CanUpdateEmbeddedBootloader;
        InstallCommand.NotifyCanExecuteChanged();
        UpdateBootloaderCommand.NotifyCanExecuteChanged();
    }

    private void UpdateProgress(FirmwareProgress progress)
    {
        OperationProgress.Stage = StageText(progress);
        OperationProgress.Progress = (progress.Percentage ?? 0) / 100d;
        OperationProgress.HasPercentage = progress.Percentage.HasValue;
        OperationProgress.IsPowerCritical = progress.State is FirmwareOperationState.Erasing or FirmwareOperationState.Programming or FirmwareOperationState.Verifying;
        OperationProgress.TechnicalDetail = progress.TechnicalDetail;
        StatusMessage = OperationProgress.Stage;
    }

    private static string StageText(FirmwareProgress progress)
    {
        return progress.State switch
        {
            FirmwareOperationState.Downloading => "Downloading firmware",
            FirmwareOperationState.WaitingForDevice => "Waiting for flight controller",
            FirmwareOperationState.IdentifyingBootloader => "Identifying bootloader",
            FirmwareOperationState.CheckingCompatibility => "Checking compatibility",
            FirmwareOperationState.Erasing => "Erasing flash — do not disconnect power",
            FirmwareOperationState.Programming => $"Programming{(progress.Percentage is null ? string.Empty : $" {progress.Percentage:0}%")}",
            FirmwareOperationState.Verifying => "Verifying firmware — do not disconnect power",
            FirmwareOperationState.Rebooting => "Rebooting",
            FirmwareOperationState.WaitingForApplication => "Waiting for ArduPilot",
            FirmwareOperationState.Completed => "Completed",
            var _ => progress.MessageCode
        };
    }

    /// <inheritdoc />
    public void Dispose()
    {
        Deactivate();
    }
}
