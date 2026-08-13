using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using MissionPlanner.App.Presentation;
using MissionPlanner.Core.Vehicles.Abstractions;
using MissionPlanner.Firmware;
using MissionPlanner.Firmware.Catalog;
using MissionPlanner.Firmware.Connected;
using MissionPlanner.Firmware.Devices;
using MissionPlanner.Firmware.Discovery;
using MissionPlanner.Firmware.Entry;
using MissionPlanner.Firmware.Images;
using MissionPlanner.Firmware.Installation;
using MissionPlanner.Firmware.Model;
using MissionPlanner.Firmware.Preparation;
using MissionPlanner.Firmware.Presentation;

namespace MissionPlanner.App.Views.InitSetup.InstallFirmware;

/// <summary>Drives connected and disconnected firmware installation experiences.</summary>
public sealed partial class InstallFirmwareViewModel : ObservableObject, IDisposable
{
    private readonly IFirmwareCatalogService catalogService;
    private readonly IFirmwareInstallationService installationService;
    private readonly IFirmwarePreparationService preparationService;
    private readonly IEmbeddedBootloaderUpdateService bootloaderUpdateService;
    private readonly IFirmwareSerialDeviceCatalog deviceCatalog;
    private readonly IFirmwarePageModeResolver modeResolver;
    private readonly IFirmwarePackageReader packageReader;
    private readonly IFirmwareFilePicker filePicker;
    private readonly IActiveVehicleContext activeVehicle;
    private readonly ILogger<InstallFirmwareViewModel> logger;
    private readonly IUserConfirmationService confirmation;
    private readonly IDispatcher dispatcher;
    private readonly IExternalLinkLauncher externalLinkLauncher;
    private readonly IDeviceManagerLauncher deviceManagerLauncher;
    private readonly object refreshSync = new();
    private CancellationTokenSource? lifetime;
    private CancellationTokenSource? refreshCancellation;
    private CancellationTokenSource? operationCancellation;
    private long refreshVersion;
    private int operationRunning;
    private IReadOnlyList<FirmwareManifestEntry> availableEntries = [];
    private IReadOnlyList<SerialDeviceDescriptor> availableDevices = [];
    private bool showingAllOptions;
    private bool active;

    /// <summary>Initializes the firmware page.</summary>
    public InstallFirmwareViewModel(
        IFirmwareCatalogService catalogService,
        IFirmwareInstallationService installationService,
        IFirmwarePreparationService preparationService,
        IEmbeddedBootloaderUpdateService bootloaderUpdateService,
        IFirmwareSerialDeviceCatalog deviceCatalog,
        IFirmwarePageModeResolver modeResolver,
        IFirmwarePackageReader packageReader,
        IFirmwareFilePicker filePicker,
        IActiveVehicleContext activeVehicle,
        IUserConfirmationService confirmation,
        IFirmwareSupportLinkProvider supportLinkProvider,
        IExternalLinkLauncher externalLinkLauncher,
        IDeviceManagerLauncher deviceManagerLauncher,
        IDispatcher dispatcher,
        ILogger<InstallFirmwareViewModel> logger)
    {
        this.catalogService = catalogService;
        this.installationService = installationService;
        this.preparationService = preparationService;
        this.bootloaderUpdateService = bootloaderUpdateService;
        this.deviceCatalog = deviceCatalog;
        this.modeResolver = modeResolver;
        this.packageReader = packageReader;
        this.filePicker = filePicker;
        this.activeVehicle = activeVehicle;
        this.confirmation = confirmation;
        SupportLinks = supportLinkProvider.GetLinks();
        this.externalLinkLauncher = externalLinkLauncher;
        this.deviceManagerLauncher = deviceManagerLauncher;
        this.dispatcher = dispatcher;
        this.logger = logger;
    }

    /// <summary>Gets catalogue choices.</summary>
    [ObservableProperty]
    public partial IReadOnlyList<FirmwareCatalogItemViewModel> FirmwareChoices { get; private set; } = [];

    /// <summary>Gets discovered serial devices.</summary>
    [ObservableProperty]
    public partial IReadOnlyList<FirmwareDeviceItemViewModel> DetectedDevices { get; private set; } = [];

    /// <summary>Gets release channels.</summary>
    public IReadOnlyList<FirmwareReleaseChannel> Channels { get; } =
        [FirmwareReleaseChannel.Stable, FirmwareReleaseChannel.Beta, FirmwareReleaseChannel.Latest];

    /// <summary>Gets operation progress.</summary>
    public FirmwareProgressViewModel OperationProgress { get; } = new();

    /// <summary>Gets concise help that remains available offline.</summary>
    public IReadOnlyList<FirmwareSupportSection> SupportSections { get; } = FirmwareSupportContent.Sections;

    /// <summary>Gets curated official and fallback support destinations.</summary>
    public IReadOnlyList<FirmwareSupportLink> SupportLinks { get; }

    /// <summary>Gets whether this host can open Windows Device Manager.</summary>
    public bool CanOpenDeviceManager => deviceManagerLauncher.IsAvailable;

    [ObservableProperty] public partial FirmwareReleaseChannel SelectedChannel { get; set; } = FirmwareReleaseChannel.Stable;
    [ObservableProperty] public partial FirmwareCatalogItemViewModel? SelectedFirmware { get; set; }
    [ObservableProperty] public partial FirmwareDeviceItemViewModel? SelectedDevice { get; set; }
    [ObservableProperty] public partial string? TargetSearchText { get; set; }
    [ObservableProperty] public partial FirmwarePreparationResult? PreparedFirmware { get; private set; }

    /// <summary>Gets whether a validated downloadable artifact is ready.</summary>
    public bool HasPreparedFirmware => PreparedFirmware is not null;

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
    [ObservableProperty] public partial bool IsCatalogRefreshRunning { get; private set; }
    [ObservableProperty] public partial bool IsCancellationDeferred { get; private set; }
    [ObservableProperty] public partial FirmwareOperationState? CurrentOperationState { get; private set; }
    [ObservableProperty] public partial bool IsHelpVisible { get; private set; }

    [ObservableProperty]
    public partial FirmwareContextHelp ContextHelp { get; private set; } =
        FirmwareContextHelpResolver.Resolve(new FirmwareSupportContext(SerialDevicePresent: false));

    /// <summary>Gets whether the current non-terminal work accepts a cancellation request.</summary>
    public bool CanRequestCancellation => IsCatalogRefreshRunning || IsOperationInProgress;

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
    public Task ActivateAsync()
    {
        if (active)
        {
            return Task.CompletedTask;
        }

        active = true;
        if (lifetime is not null)
        {
            return Task.CompletedTask;
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
        return IsDisconnectedMode
            ? RefreshSafelyAsync(false, lifetime.Token)
            : Task.CompletedTask;
    }

    private async Task RefreshSafelyAsync(bool forceRefresh, CancellationToken cancellationToken, bool allOptions = false)
    {
        try
        {
            await RefreshAsync(forceRefresh, cancellationToken, allOptions);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Refresh failed");
        }
    }

    /// <summary>Stops page-owned observation without cancelling an unsafe firmware operation.</summary>
    private void Deactivate()
    {
        if (!active && lifetime is null)
        {
            return;
        }

        active = false;
        activeVehicle.Changed -= OnActiveVehicleChanged;
        CancelRefresh();

        var current = lifetime;
        lifetime = null;

        current?.Cancel();
        current?.Dispose();
    }

    partial void OnSelectedChannelChanged(FirmwareReleaseChannel value)
    {
        UpdateContextHelp();
        if (lifetime is not null && IsDisconnectedMode)
        {
            _ = RefreshSafelyAsync(false, lifetime.Token);
        }
    }

    partial void OnTargetSearchTextChanged(string? value)
    {
        ApplyTargetQuery();
    }

    partial void OnIsCatalogRefreshRunningChanged(bool value)
    {
        OnPropertyChanged(nameof(CanRequestCancellation));
        CancelCommand.NotifyCanExecuteChanged();
    }

    [RelayCommand]
    private Task RefreshCatalogAsync()
    {
        return RefreshSafelyAsync(true, lifetime?.Token ?? CancellationToken.None);
    }

    [RelayCommand]
    private Task ShowAllOptionsAsync()
    {
        return RefreshSafelyAsync(true, lifetime?.Token ?? CancellationToken.None, true);
    }

    [RelayCommand]
    private void SelectFirmware(FirmwareCatalogItemViewModel item)
    {
        SelectedFirmware = item;
        PreparedFirmware = null;
        OnPropertyChanged(nameof(HasPreparedFirmware));
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
            UpdateContextHelp();
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

        using var ownedCancellation = BeginOperationCancellation(cancellationToken);
        try
        {
            SetOperation(true, FirmwareOperationState.Downloading);
            var target = SelectedFirmware?.Entry.Target;
            var prepared = PreparedFirmware is not null && ReferenceEquals(PreparedFirmware.ManifestEntry, SelectedFirmware?.Entry) ? PreparedFirmware : null;
            var request = new FirmwareInstallationRequest(
                new BootloaderEntryContext(new BootloaderDiscoveryRequest(
                        SelectedDevice?.Descriptor,
                        target?.UsbIdentifiers,
                        target?.BootloaderNames),
                    SelectedDevice?.Descriptor),
                prepared is null ? SelectedFirmware?.Entry.Artifact : null,
                CustomPackage ?? prepared?.Package);
            var progress = new Progress<FirmwareProgress>(UpdateProgress);
            var result = await installationService.InstallAsync(request, progress, ownedCancellation.Token);
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
            EndOperationCancellation(ownedCancellation);
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

        var (version, refreshToken) = BeginRefresh(cancellationToken);
        try
        {
            await DispatchAsync(() =>
            {
                IsCatalogRefreshRunning = true;
                StatusMessage = "Loading firmware catalogue…";
            });
            var channel = SelectedChannel;
            // Both manifest parsing and Windows device discovery can perform substantial
            // synchronous work before their returned tasks complete. Run them away from the
            // UI context and concurrently so opening the Connect dialog remains responsive.
            var catalogTask = Task.Run(
                () => catalogService.GetCatalogAsync(
                    new FirmwareCatalogRequest(Channel: allOptions ? null : channel, ForceRefresh: forceRefresh),
                    refreshToken),
                refreshToken);
            var devicesTask = Task.Run(
                () => deviceCatalog.GetDevicesAsync(refreshToken),
                refreshToken);
            await Task.WhenAll(catalogTask, devicesTask).ConfigureAwait(false);
            var catalog = await catalogTask.ConfigureAwait(false);
            var devices = await devicesTask.ConfigureAwait(false);
            var entries = catalog.Entries.Where(entry => entry.Target.VehicleType != FirmwareVehicleType.Unknown &&
                                                         entry.Artifact.Format is FirmwareImageFormat.Apj or FirmwareImageFormat.Px4).ToArray();
            var deviceItems = await Task.Run(
                () => CreateDeviceItems(entries, devices),
                refreshToken).ConfigureAwait(false);
            refreshToken.ThrowIfCancellationRequested();
            await DispatchAsync(() =>
            {
                if (!IsLatestRefresh(version))
                {
                    return;
                }

                availableEntries = entries;
                availableDevices = devices;
                showingAllOptions = allOptions;
                ApplyTargetQuery();
                CustomPackage = null;
                OnPropertyChanged(nameof(HasCustomFirmware));

                DetectedDevices = deviceItems;

                var recommendedDevices = DetectedDevices.Where(item => item.IsRecommended).ToArray();
                SelectedDevice = recommendedDevices.Length == 1 ? recommendedDevices[0] : null;
                DeviceStatus = DetectedDevices.Count == 0
                    ? "No flight controller detected"
                    : recommendedDevices.Length > 1
                        ? "Multiple matching devices detected; select the exact flight controller."
                        : SelectedDevice is not null
                            ? $"Recommended device: {SelectedDevice}"
                            : "Select the flight controller explicitly.";
                StatusMessage = catalog.IsStale ? "Showing cached firmware catalogue" : $"{FirmwareChoices.Count} vehicle firmware choices available";
                UpdateContextHelp();
            });
        }
        catch (OperationCanceledException) when (refreshToken.IsCancellationRequested) { }
        catch (Exception exception)
        {
            logger.LogError(exception, "Firmware catalogue refresh failed.");
            await DispatchAsync(() =>
            {
                if (IsLatestRefresh(version))
                {
                    StatusMessage = exception.Message;
                }
            });
        }
        finally
        {
            await DispatchAsync(() =>
            {
                if (IsLatestRefresh(version))
                {
                    IsCatalogRefreshRunning = false;
                }
            });
        }
    }

    private void ApplyTargetQuery()
    {
        var previousEntry = SelectedFirmware?.Entry;
        var recommendations = FirmwareTargetSelector.Query(availableEntries,
            new FirmwareTargetQuery(ReleaseChannel: showingAllOptions ? null : SelectedChannel, SearchText: TargetSearchText),
            availableDevices, SelectedFirmware?.BoardId);
        FirmwareChoices = recommendations.Select(recommendation => new FirmwareCatalogItemViewModel(recommendation)).ToArray();

        var retained = previousEntry is null ? null : FirmwareChoices.FirstOrDefault(item => SameEntry(item.Entry, previousEntry));
        var automatic = FirmwareTargetSelector.UnambiguousHighConfidence(recommendations);
        SelectedFirmware = retained ?? (automatic is null ? null : FirmwareChoices.Single(item => ReferenceEquals(item.Entry, automatic.Entry)));
        InstallCommand.NotifyCanExecuteChanged();
    }

    private static IReadOnlyList<FirmwareDeviceItemViewModel> CreateDeviceItems(
        IReadOnlyList<FirmwareManifestEntry> entries,
        IReadOnlyList<SerialDeviceDescriptor> devices)
    {
        return devices.Select(device =>
        {
            var usbMatch = entries.Any(entry => entry.Target.UsbIdentifiers.Contains(device.UsbIdentifier ?? default));
            var hintMatch = entries.Any(entry => entry.Target.BootloaderNames.Any(hint =>
                (!string.IsNullOrWhiteSpace(device.ProductName) && device.ProductName.Contains(hint, StringComparison.OrdinalIgnoreCase)) ||
                device.BoardHints.Any(value => value.Contains(hint, StringComparison.OrdinalIgnoreCase))));
            return new FirmwareDeviceItemViewModel(
                device,
                usbMatch || hintMatch,
                usbMatch ? "Exact catalogue USB match" : hintMatch ? "Bootloader/board hint match" : "Manual device selection");
        }).ToArray();
    }

    private (long Version, CancellationToken Token) BeginRefresh(CancellationToken cancellationToken)
    {
        lock (refreshSync)
        {
            refreshCancellation?.Cancel();
            refreshCancellation?.Dispose();
            refreshCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            return (++refreshVersion, refreshCancellation.Token);
        }
    }

    private void CancelRefresh()
    {
        lock (refreshSync)
        {
            refreshVersion++;
            refreshCancellation?.Cancel();
            refreshCancellation?.Dispose();
            refreshCancellation = null;
        }
    }

    private bool IsLatestRefresh(long version)
    {
        lock (refreshSync)
        {
            return version == refreshVersion;
        }
    }

    private Task DispatchAsync(Action action)
    {
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        if (!dispatcher.Dispatch(() =>
            {
                try
                {
                    action();
                    completion.SetResult();
                }
                catch (Exception exception)
                {
                    completion.SetException(exception);
                }
            }))
        {
            completion.SetException(new InvalidOperationException("Unable to dispatch firmware catalogue update."));
        }

        return completion.Task;
    }

    private static bool SameEntry(FirmwareManifestEntry left, FirmwareManifestEntry right)
    {
        return left.Target.BoardId == right.Target.BoardId &&
               left.Channel == right.Channel &&
               left.Artifact.DownloadUri == right.Artifact.DownloadUri;
    }

    [RelayCommand]
    private async Task DownloadAndValidateAsync(CancellationToken cancellationToken)
    {
        if (SelectedFirmware is null || IsOperationInProgress)
        {
            return;
        }

        using var ownedCancellation = BeginOperationCancellation(cancellationToken);
        try
        {
            SetOperation(true, FirmwareOperationState.Downloading);
            PreparedFirmware = await preparationService.PrepareAsync(new FirmwarePreparationRequest(SelectedFirmware.Entry), new Progress<FirmwareProgress>(UpdateProgress), ownedCancellation.Token);
            OnPropertyChanged(nameof(HasPreparedFirmware));
            StatusMessage = PreparedFirmware.WasCacheHit ? "Validated cached firmware package." : "Firmware downloaded and validated.";
        }
        catch (OperationCanceledException) when (ownedCancellation.IsCancellationRequested)
        {
            StatusMessage = "Firmware download and validation cancelled.";
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Firmware preparation failed.");
            StatusMessage = exception.Message;
            UpdateContextHelp(exception is Firmware.Exceptions.FirmwarePackageException);
        }
        finally
        {
            EndOperationCancellation(ownedCancellation);
            SetOperation(false, null);
        }
    }

    [RelayCommand(CanExecute = nameof(CanRequestCancellation))]
    private void Cancel()
    {
        if (IsCatalogRefreshRunning)
        {
            CancelRefresh();
            StatusMessage = "Firmware catalogue refresh cancelled.";
        }

        var cancellation = operationCancellation;
        if (cancellation is null || cancellation.IsCancellationRequested)
        {
            return;
        }

        IsCancellationDeferred = CurrentOperationState is FirmwareOperationState.Erasing or
            FirmwareOperationState.Programming or FirmwareOperationState.Verifying or FirmwareOperationState.Rebooting;
        StatusMessage = IsCancellationDeferred
            ? "Cancellation requested. The flash will continue through verify and reboot before stopping at a safe boundary. Do not disconnect power."
            : "Cancelling firmware operation…";
        cancellation.Cancel();
        CancelCommand.NotifyCanExecuteChanged();
    }

    [RelayCommand]
    private Task CopyDownloadUrlAsync()
    {
        return SelectedFirmware is null ? Task.CompletedTask : Clipboard.Default.SetTextAsync(SelectedFirmware.Entry.Artifact.DownloadUri.AbsoluteUri);
    }

    [RelayCommand]
    private Task OpenSupportLinkAsync(FirmwareSupportLink link, CancellationToken cancellationToken)
    {
        return externalLinkLauncher.OpenAsync(link.Uri, cancellationToken);
    }

    [RelayCommand]
    private Task OpenDeviceManagerAsync(CancellationToken cancellationToken)
    {
        return deviceManagerLauncher.OpenAsync(cancellationToken);
    }

    [RelayCommand]
    private void ToggleHelp()
    {
        IsHelpVisible = !IsHelpVisible;
    }

    private void OnActiveVehicleChanged(object? sender, Core.Vehicles.ActiveVehicleChangedEventArgs e)
    {
        if (e.Current.IsOnline)
        {
            // The disconnected catalogue/device scan is no longer relevant once a vehicle
            // becomes active. Cancel it before queuing UI work for the connected mode.
            CancelRefresh();
        }

        dispatcher.Dispatch(() =>
        {
            if (!active)
            {
                return;
            }

            ApplyMode();
        });
    }

    private void SetOperation(bool active, FirmwareOperationState? stage)
    {
        IsOperationInProgress = active;
        CurrentOperationState = stage;
        if (!active)
        {
            IsCancellationDeferred = false;
        }

        OnPropertyChanged(nameof(CanNavigateAway));
        OnPropertyChanged(nameof(CanRequestCancellation));
        ApplyMode(stage);
        InstallCommand.NotifyCanExecuteChanged();
        UpdateBootloaderCommand.NotifyCanExecuteChanged();
        CancelCommand.NotifyCanExecuteChanged();
    }

    private void ApplyMode(FirmwareOperationState? stage = null)
    {
        var state = modeResolver.Resolve(new FirmwarePageContext(
            OperatingSystem.IsWindows(),
            activeVehicle.IsOnline,
            activeVehicle.State?.IsArmed == true,
            activeVehicle.State is not null && activeVehicle.State.Identity.Firmware.Family != FirmwareFamily.Unknown,
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
        CurrentOperationState = progress.State;
        OperationProgress.Stage = StageText(progress);
        OperationProgress.Progress = (progress.Percentage ?? 0) / 100d;
        OperationProgress.HasPercentage = progress.Percentage.HasValue;
        OperationProgress.IsPowerCritical = progress.State is FirmwareOperationState.Erasing or FirmwareOperationState.Programming or FirmwareOperationState.Verifying;
        OperationProgress.TechnicalDetail = progress.TechnicalDetail;
        StatusMessage = OperationProgress.Stage;
    }

    private CancellationTokenSource BeginOperationCancellation(CancellationToken cancellationToken)
    {
        var owned = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, lifetime?.Token ?? CancellationToken.None);
        operationCancellation = owned;
        IsCancellationDeferred = false;
        return owned;
    }

    private void EndOperationCancellation(CancellationTokenSource owned)
    {
        if (ReferenceEquals(operationCancellation, owned))
        {
            operationCancellation = null;
        }

        CancelCommand.NotifyCanExecuteChanged();
    }

    private void UpdateContextHelp(bool packageBoardMismatch = false)
    {
        ContextHelp = FirmwareContextHelpResolver.Resolve(new FirmwareSupportContext(
            SerialDevicePresent: DetectedDevices.Count > 0,
            TargetAmbiguous: FirmwareChoices.Count > 0 && SelectedFirmware is null,
            PackageBoardMismatch: packageBoardMismatch,
            Channel: SelectedChannel,
            CustomPackageSelected: CustomPackage is not null));
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
