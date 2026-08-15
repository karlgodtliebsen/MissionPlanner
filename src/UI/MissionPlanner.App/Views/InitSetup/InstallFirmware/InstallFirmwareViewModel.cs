using System.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Mapsui.Utilities;
using Microsoft.Extensions.Logging;
using MissionPlanner.App.Presentation;
using MissionPlanner.Core.Vehicles.Abstractions;
using MissionPlanner.Firmware;
using MissionPlanner.Firmware.Catalog;
using MissionPlanner.Firmware.Connected;
using MissionPlanner.Firmware.Devices;
using MissionPlanner.Firmware.Dfu;
using MissionPlanner.Firmware.Discovery;
using MissionPlanner.Firmware.Entry;
using MissionPlanner.Firmware.Images;
using MissionPlanner.Firmware.Installation;
using MissionPlanner.Firmware.Model;
using MissionPlanner.Firmware.Preparation;
using MissionPlanner.Firmware.Presentation;
using UraniumUI.Material.Dialogs;

namespace MissionPlanner.App.Views.InitSetup.InstallFirmware;

/// <summary>Drives connected and disconnected firmware installation experiences.</summary>
public sealed partial class InstallFirmwareViewModel : ObservableObject, IDisposable
{
    private readonly IFirmwareCatalogService catalogService;
    private readonly IFirmwareInstallationService installationService;
    private readonly IFirmwarePreparationService preparationService;
    private readonly IDfuInstallationService dfuInstallationService;
    private readonly IDfuDeviceCatalog dfuDeviceCatalog;
    private readonly IDfuToolLocator dfuToolLocator;
    private readonly IEmbeddedBootloaderUpdateService bootloaderUpdateService;
    private readonly IFirmwareSerialDeviceCatalog deviceCatalog;
    private readonly IFirmwarePageModeResolver modeResolver;
    private readonly IFirmwarePackageReader packageReader;
    private readonly IFirmwareFilePicker filePicker;
    private readonly IActiveVehicleContext activeVehicle;
    private readonly ILogger<InstallFirmwareViewModel> logger;
    private readonly IUserConfirmationService confirmation;
    private readonly IDispatcher dispatcher;
    private readonly IExtendedDialogService dialogService;
    private readonly IExternalLinkLauncher externalLinkLauncher;
    private readonly IDeviceManagerLauncher deviceManagerLauncher;
    private readonly object refreshSync = new();
    private CancellationTokenSource? lifetime;
    private CancellationTokenSource? refreshCancellation;
    private CancellationTokenSource? operationCancellation;
    private IDisposable? progressDialog;
    private long refreshVersion;
    private int operationRunning;
    private IReadOnlyList<FirmwareManifestEntry> availableEntries = [];
    private IReadOnlyList<SerialDeviceDescriptor> availableDevices = [];
    private FirmwareManifestEntry? selectedFirmwareTarget;
    private bool showingAllOptions;
    private bool active;


    /// <summary>
    /// Initializes the firmware page.
    /// </summary>
    /// <param name="catalogService"></param>
    /// <param name="installationService"></param>
    /// <param name="preparationService"></param>
    /// <param name="dfuInstallationService"></param>
    /// <param name="dfuDeviceCatalog"></param>
    /// <param name="dfuToolLocator"></param>
    /// <param name="bootloaderUpdateService"></param>
    /// <param name="deviceCatalog"></param>
    /// <param name="modeResolver"></param>
    /// <param name="packageReader"></param>
    /// <param name="filePicker"></param>
    /// <param name="activeVehicle"></param>
    /// <param name="confirmation"></param>
    /// <param name="supportLinkProvider"></param>
    /// <param name="externalLinkLauncher"></param>
    /// <param name="deviceManagerLauncher"></param>
    /// <param name="dispatcher"></param>
    /// <param name="dialogService">Displays the cancellable firmware-operation progress dialog.</param>
    /// <param name="logger"></param>
    public InstallFirmwareViewModel(
        IFirmwareCatalogService catalogService,
        IFirmwareInstallationService installationService,
        IFirmwarePreparationService preparationService,
        IDfuInstallationService dfuInstallationService,
        IDfuDeviceCatalog dfuDeviceCatalog,
        IDfuToolLocator dfuToolLocator,
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
        IExtendedDialogService dialogService,
        ILogger<InstallFirmwareViewModel> logger)
    {
        this.catalogService = catalogService;
        this.installationService = installationService;
        this.preparationService = preparationService;
        this.dfuInstallationService = dfuInstallationService;
        this.dfuDeviceCatalog = dfuDeviceCatalog;
        this.dfuToolLocator = dfuToolLocator;
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
        this.dialogService = dialogService;
        this.logger = logger;
    }

    /// <summary>
    /// Gets whether an operation is running.
    /// </summary>
    [ObservableProperty]
    public partial bool IsBusy { get; private set; }

    /// <summary>Gets the message displayed by the active firmware progress dialog.</summary>
    [ObservableProperty]
    public partial string ProgressMessage { get; private set; } = string.Empty;

    ///
    /// <summary>Gets catalogue choices.
    /// </summary>
    [ObservableProperty]
    public partial ObservableRangeCollection<FirmwareCatalogItemViewModel> FirmwareChoices { get; private set; } = [];

    /// <summary>
    /// Gets catalogue choices.
    /// </summary>
    [ObservableProperty]
    public partial ObservableRangeCollection<FirmwareCatalogItemViewModel> FilteredFirmwareChoices { get; private set; } = [];

    /// <summary>
    /// Gets the distinct firmware versions available in the catalogue.
    /// </summary>
    [ObservableProperty]
    public partial ObservableRangeCollection<string> Versions { get; private set; } = [];

    /// <summary>
    /// Gets or sets the selected firmware version for filtering the catalogue.
    /// </summary>
    [ObservableProperty]
    public partial string? SelectedVersion { get; set; }

    /// <summary>
    ///  Gets the distinct FrameTypes available in the catalogue.
    /// </summary>
    [ObservableProperty]
    public partial ObservableRangeCollection<string> FrameTypes { get; private set; } = [];

    /// <summary>
    /// Gets or sets the selected FrameType for filtering the catalogue.
    /// </summary>
    [ObservableProperty]
    public partial string? SelectedFrameType { get; set; }


    /// <summary>
    ///  Gets the distinct Manufacturer available in the catalogue.
    /// </summary>
    [ObservableProperty]
    public partial ObservableRangeCollection<string> Manufacturers { get; private set; } = [];

    /// <summary>
    ///  Gets or sets the selected Manufacturer for filtering the catalogue.
    /// </summary>
    [ObservableProperty]
    public partial string? SelectedManufacturer { get; set; }


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


    [ObservableProperty] public partial FirmwareReleaseChannel SelectedChannel { get; set; } = FirmwareReleaseChannel.Stable;

    [ObservableProperty] public partial FirmwareCatalogItemViewModel? SelectedFirmware { get; set; }
    [ObservableProperty] public partial FirmwareDeviceItemViewModel? SelectedDevice { get; set; }
    [ObservableProperty] public partial IReadOnlyList<DfuDeviceItemViewModel> DfuDevices { get; private set; } = [];
    [ObservableProperty] public partial DfuDeviceItemViewModel? SelectedDfuDevice { get; set; }
    [ObservableProperty] public partial string DfuStatus { get; private set; } = "Enter STM32 DFU mode, then refresh the catalogue.";

    [ObservableProperty] public partial FirmwarePreparationResult? PreparedFirmware { get; private set; }

    /// <summary>Gets whether this host can open Windows Device Manager.</summary>
    public bool CanOpenDeviceManager => deviceManagerLauncher.IsAvailable;

    /// <summary>Gets whether a validated downloadable artifact is ready.</summary>
    public bool HasPreparedFirmware => PreparedFirmware is not null;

    /// <summary>Gets whether parsed custom metadata is available.</summary>
    public bool HasCustomFirmware => CustomPackage is not null;

    /// <summary>Gets whether the current non-terminal work accepts a cancellation request.</summary>
    public bool CanRequestCancellation => IsCatalogRefreshRunning || IsOperationInProgress;

    /// <summary>Gets whether Shell navigation may safely leave this page.</summary>
    public bool CanNavigateAway => !IsOperationInProgress;

    /// <summary>Gets whether a terminal diagnostic report can be copied.</summary>
    public bool HasDiagnosticReport => !string.IsNullOrWhiteSpace(LastDiagnosticReport);

    /// <summary>Gets whether the selected official target can be installed through STM32 ROM DFU.</summary>
    public bool CanInstallInitialDfuFirmware => SelectedFirmware is not null &&
                                                SelectedDfuDevice?.Descriptor.DriverState == DfuDriverState.PresentReady && !IsOperationInProgress;

    [ObservableProperty] public partial bool IsHelpVisible { get; private set; }

    [ObservableProperty] public partial ApjFirmwarePackage? CustomPackage { get; private set; }
    [ObservableProperty] public partial string? CustomFirmwareName { get; private set; }
    [ObservableProperty] public partial string? CustomFirmwareDescription { get; private set; }
    [ObservableProperty] public partial string? CustomFirmwarePlatform { get; private set; }
    [ObservableProperty] public partial string? CustomFirmwareBuild { get; private set; }
    [ObservableProperty] public partial int CustomFirmwareBoardId { get; private set; }
    [ObservableProperty] public partial long CustomFirmwareImageSize { get; private set; }


    [ObservableProperty] public partial bool IsConnectedMode { get; private set; }

    [ObservableProperty] public partial bool IsDisconnectedMode { get; private set; }
    [ObservableProperty] public partial bool IsUnsupportedMode { get; private set; }
    [ObservableProperty] public partial bool IsOperationInProgress { get; private set; }
    [ObservableProperty] public partial bool IsCatalogRefreshRunning { get; private set; }
    [ObservableProperty] public partial bool IsCancellationDeferred { get; private set; }
    [ObservableProperty] public partial FirmwareOperationState? CurrentOperationState { get; private set; }

    [ObservableProperty]
    public partial FirmwareContextHelp ContextHelp { get; private set; } =
        FirmwareContextHelpResolver.Resolve(new FirmwareSupportContext(SerialDevicePresent: false));


    [ObservableProperty] public partial bool CanUpdateBootloader { get; private set; }
    [ObservableProperty] public partial bool CanInstall { get; private set; }
    [ObservableProperty] public partial string StatusMessage { get; private set; } = "Ready";
    [ObservableProperty] public partial string DeviceStatus { get; private set; } = "No flight controller detected";
    [ObservableProperty] public partial string? LastDiagnosticReport { get; private set; }


    /// <summary>
    /// Starts observing connection state and refreshes disconnected data.
    /// </summary>
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
        IsBusy = true;
        activeVehicle.Changed += OnActiveVehicleChanged;
        StatusMessage = "Ready";
        OperationProgress.Stage = "Ready";
        OperationProgress.Progress = 0;
        OperationProgress.HasStage = false;
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
        IsBusy = true;
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

        IsBusy = false;
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
        selectedFirmwareTarget = null;
        UpdateContextHelp();
        if (lifetime is not null && IsDisconnectedMode)
        {
            _ = RefreshSafelyAsync(false, lifetime.Token);
        }
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
            selectedFirmwareTarget = null;
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
            await ShowOperationDialogAsync("Installing firmware", ownedCancellation);
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

            var progress = CreateProgress();
            var result = await installationService.InstallAsync(request, progress, ownedCancellation.Token);
            LastDiagnosticReport = result.DiagnosticReport?.CreateReport();
            OnPropertyChanged(nameof(HasDiagnosticReport));


            StatusMessage = result.State == FirmwareOperationState.Completed
                ? result.ApplicationDevice is null
                    ? "Firmware installation completed; reconnect was not detected. Reconnect the flight controller manually."
                    : $"Firmware installation completed. ArduPilot returned on {result.ApplicationDevice.PortName}; reconnect is available."
                : result.Failure?.TechnicalDetail is { Length: > 0 } detail
                    ? $"Firmware installation {result.State}: {detail}"
                    : $"Firmware installation {result.State}";
        }
        catch (OperationCanceledException) when (ownedCancellation.IsCancellationRequested)
        {
            StatusMessage = "Firmware installation cancelled.";
        }
        catch (Exception exception)
        {
            Debug.Print("Firmware installation failed.\n{0}", exception.ToString());
            logger.LogError(exception, "Firmware installation failed.");
            StatusMessage = exception.Message;
        }
        finally
        {
            CloseOperationDialog();
            EndOperationCancellation(ownedCancellation);
            Interlocked.Exchange(ref operationRunning, 0);
            SetOperation(false, null);
        }
    }

    private bool CanStartInstall()
    {
        return CanInstall && (SelectedFirmware is not null || CustomPackage is not null) && !IsOperationInProgress;
    }

    [RelayCommand(CanExecute = nameof(CanInstallInitialDfuFirmware), AllowConcurrentExecutions = false)]
    private async Task InstallInitialDfuFirmwareAsync(CancellationToken cancellationToken)
    {
        if (SelectedFirmware is null || SelectedDfuDevice is null ||
            Interlocked.CompareExchange(ref operationRunning, 1, 0) != 0)
        {
            return;
        }

        var selectedFirmware = SelectedFirmware;
        var selectedDfuDevice = SelectedDfuDevice;
        var requiredPhrase = $"FLASH {selectedFirmware.Platform}";
        var phrase = await dialogService.DisplayPromptAsync(
            "Confirm initial ArduPilot installation",
            $"This replaces Betaflight and installs ArduPilot plus its bootloader for {selectedFirmware.Platform} (board ID {selectedFirmware.BoardId}). Type exactly: {requiredPhrase}",
            string.Empty,
            "Continue");
        if (!string.Equals(phrase?.Trim(), requiredPhrase, StringComparison.Ordinal))
        {
            StatusMessage = phrase is null ? "Initial DFU installation cancelled." : $"Confirmation did not match {requiredPhrase}.";
            Interlocked.Exchange(ref operationRunning, 0);
            return;
        }

        using var ownedCancellation = BeginOperationCancellation(cancellationToken);
        try
        {
            SetOperation(true, FirmwareOperationState.Downloading);
            await ShowOperationDialogAsync("Installing ArduPilot through STM32 DFU", ownedCancellation);
            var progress = new Progress<DfuProgress>(value => dispatcher.Dispatch(() =>
            {
                ProgressMessage = DfuStageText(value);
                StatusMessage = ProgressMessage;
            }));
            var result = await dfuInstallationService.InstallAsync(
                new DfuInstallationRequest(
                    selectedFirmware.Platform,
                    selectedFirmware.BoardId,
                    selectedDfuDevice.Descriptor,
                    ConfirmationPhrase: requiredPhrase,
                    ManifestEntry: selectedFirmware.Entry),
                progress,
                ownedCancellation.Token);

            await RefreshDfuDevicesAsync(CancellationToken.None);

            LastDiagnosticReport = BuildDfuDiagnosticReport(result, selectedFirmware, selectedDfuDevice.Descriptor);
            OnPropertyChanged(nameof(HasDiagnosticReport));
            StatusMessage = result.State == DfuOperationState.Completed
                ? result.ApplicationRediscovered
                    ? "Initial ArduPilot installation completed and the application device was detected."
                    : "Programming and verification completed. Reconnect or reset the controller if ArduPilot does not appear."
                : result.Failure?.Message ?? $"STM32 DFU installation {result.State}.";
        }
        catch (OperationCanceledException) when (ownedCancellation.IsCancellationRequested)
        {
            StatusMessage = "Initial DFU installation cancelled.";
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Initial STM32 DFU installation failed.");
            StatusMessage = exception.Message;
        }
        finally
        {
            CloseOperationDialog();
            EndOperationCancellation(ownedCancellation);
            Interlocked.Exchange(ref operationRunning, 0);
            SetOperation(false, null);
        }
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

        Debug.Print("RefreshAsync");

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
            var catalogTask = Task.Run(() => catalogService.GetCatalogAsync(new FirmwareCatalogRequest(Channel: allOptions ? null : channel, ForceRefresh: forceRefresh), refreshToken), refreshToken);
            var devicesTask = Task.Run(() => deviceCatalog.GetDevicesAsync(refreshToken), refreshToken);
            var dfuDevicesTask = Task.Run(() => dfuDeviceCatalog.GetDevicesAsync(refreshToken), refreshToken);
            var dfuToolTask = Task.Run(() => dfuToolLocator.LocateAsync(refreshToken), refreshToken);

            await Task.WhenAll(catalogTask, devicesTask, dfuDevicesTask, dfuToolTask).ConfigureAwait(false);

            var catalog = await catalogTask.ConfigureAwait(false);
            var devices = await devicesTask.ConfigureAwait(false);
            var dfuDevices = await dfuDevicesTask.ConfigureAwait(false);
            var dfuTool = await dfuToolTask.ConfigureAwait(false);
            Debug.Print("RefreshAsync Completed task 1 & 2");


            var entries = catalog.Entries.Where(entry =>
                entry.Target.VehicleType != FirmwareVehicleType.Unknown
                &&
                entry.Artifact.Format is FirmwareImageFormat.Apj or FirmwareImageFormat.Px4).ToArray();

            var deviceItems = await Task.Run(() => CreateDeviceItems(entries, devices), refreshToken).ConfigureAwait(false);

            Debug.Print($"RefreshAsync Completed task 3 with entries count: {entries.Length}");


            refreshToken.ThrowIfCancellationRequested();
            if (!IsLatestRefresh(version))
            {
                return;
            }

            availableEntries = entries;
            availableDevices = devices;
            showingAllOptions = allOptions;

            await DispatchAsync(() =>
            {
                ApplyTargetQuery();
                CustomPackage = null;
                OnPropertyChanged(nameof(HasCustomFirmware));

                DetectedDevices = deviceItems;
                DfuDevices = dfuDevices.Select(device => new DfuDeviceItemViewModel(device)).ToArray();
                SelectedDfuDevice = DfuDevices.Count == 1 ? DfuDevices[0] : null;
                DfuStatus = DfuDevices.Count == 0
                    ? "No STM32 DFU device detected. Hold BOOT/DFU while connecting USB, or use the board's documented BOOT and RESET sequence, then refresh."
                    : dfuTool.Availability != DfuToolAvailability.Available
                        ? dfuTool.Diagnostic ?? "Install STM32CubeProgrammer and its bundled DFU driver before continuing."
                        : SelectedDfuDevice?.Descriptor.DriverState == DfuDriverState.PresentReady
                            ? "STM32 DFU device and STM32CubeProgrammer are ready."
                            : "Select a DFU device and resolve any indicated driver problem.";

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
            Debug.Print("Firmware catalogue refresh failed.\n" + exception.Message);

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

    partial void OnSelectedVersionChanged(string? value)
    {
        FilterData(SelectedVersion, SelectedFrameType, SelectedManufacturer);
    }

    partial void OnSelectedFrameTypeChanged(string? value)
    {
        FilterData(SelectedVersion, SelectedFrameType, SelectedManufacturer);
    }

    partial void OnSelectedManufacturerChanged(string? value)
    {
        FilterData(SelectedVersion, SelectedFrameType, SelectedManufacturer);
    }


    private void FilterData(string? version, string? vehicleType, string? manufacturer)
    {
        var choices = FirmwareChoices.ToList();

        if (!string.IsNullOrEmpty(version))
        {
            choices = choices.Where(x => x.FirmwareVersion.ToString() == version).ToList();
        }

        if (!string.IsNullOrEmpty(vehicleType))
        {
            choices = choices.Where(x => x.VehicleType == vehicleType).ToList();
        }

        if (!string.IsNullOrEmpty(manufacturer))
        {
            choices = choices.Where(x => x.Manufacturer == manufacturer).ToList();
        }

        FilteredFirmwareChoices.Clear();
        FilteredFirmwareChoices.AddRange(choices);
    }

    private void ApplyTargetQuery()
    {
        SelectedVersion = null;
        SelectedFrameType = null;
        SelectedManufacturer = null;

        // The grid may transiently clear SelectedFirmware while its collection is rebuilt.
        // Preserve the last deliberate/non-null selection independently of that UI event.
        var previousEntry = selectedFirmwareTarget;
        var recommendations =
            FirmwareTargetSelector.Query(availableEntries, new FirmwareTargetQuery(ReleaseChannel: showingAllOptions ? null : SelectedChannel),
                availableDevices, SelectedFirmware?.BoardId);

        var choices = recommendations.Select(recommendation => new FirmwareCatalogItemViewModel(recommendation))
            .ToArray();

        FirmwareChoices.Clear();
        FirmwareChoices.AddRange(choices);


        var versions = choices
            .Select(x => x.FirmwareVersion)
            .Distinct()
            .OrderByDescending(v => v.SemanticVersion ?? new System.Version(0, 0))
            .ThenByDescending(v => v.Value, StringComparer.OrdinalIgnoreCase)
            .Select(x => x.ToString())
            .ToList();

        Versions.Clear();
        Versions.AddRange(versions);

        //FirmwareManifestEntry -> FirmwareBoardTarget Target  -> FirmwareVehicleType VehicleType 
        var frameTypes = choices
            .Select(x => x.VehicleType)
            .Distinct()
            .Order()
            .ToList();

        FrameTypes.Clear();
        FrameTypes.AddRange(frameTypes);

        var manufacturers = choices
            .Select(x => x.Manufacturer)
            .Distinct()
            .Order()
            .ToList();

        Manufacturers.Clear();
        Manufacturers.AddRange(manufacturers);

        FilteredFirmwareChoices.Clear();
        FilteredFirmwareChoices.AddRange(choices);

        Debug.Print($"ApplyTargetQuery with FirmwareChoices count: {FirmwareChoices.Count}");

        var retained = previousEntry is null ? null : FirmwareChoices.FirstOrDefault(item => SameEntry(item.Entry, previousEntry));
        var automatic = FirmwareTargetSelector.UnambiguousHighConfidence(recommendations);
        SelectedFirmware = retained ?? (automatic is null ? null : FirmwareChoices.Single(item => ReferenceEquals(item.Entry, automatic.Entry)));
        InstallCommand.NotifyCanExecuteChanged();
    }

    private static IReadOnlyList<FirmwareDeviceItemViewModel> CreateDeviceItems(IReadOnlyList<FirmwareManifestEntry> entries, IReadOnlyList<SerialDeviceDescriptor> devices)
    {
        Debug.Print("CreateDeviceItems");

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

        Debug.Print($"CreateDeviceItems found {deviceItems.Length} items");
        return deviceItems;
    }

    private (long Version, CancellationToken Token) BeginRefresh(CancellationToken cancellationToken)
    {
        Debug.Print("BeginRefresh");
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
        Debug.Print("CancelRefresh");

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
            await ShowOperationDialogAsync("Downloading firmware", ownedCancellation);
            PreparedFirmware = await preparationService.PrepareAsync(new FirmwarePreparationRequest(SelectedFirmware.Entry), CreateProgress(), ownedCancellation.Token);
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
            CloseOperationDialog();
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
            IsBusy = false;
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
        OnPropertyChanged(nameof(CanInstallInitialDfuFirmware));
        InstallInitialDfuFirmwareCommand.NotifyCanExecuteChanged();
        UpdateBootloaderCommand.NotifyCanExecuteChanged();
        CancelCommand.NotifyCanExecuteChanged();
    }

    private void ApplyMode(FirmwareOperationState? stage = null)
    {
        var directInstallationSupported = OperatingSystem.IsWindows();
        var vehicleConnected = activeVehicle.IsOnline;
        var state = modeResolver.Resolve(new FirmwarePageContext(
            directInstallationSupported, vehicleConnected, activeVehicle.State?.IsArmed == true,
            activeVehicle.State is not null && activeVehicle.State.Identity.Firmware.Family != FirmwareFamily.Unknown, IsOperationInProgress, stage));

        // OperationInProgress is a capability/progress state, not a different page layout.
        // Keep the existing visual tree mounted so starting or completing an operation does
        // not reset ScrollView position, focus, selections, or expensive child controls.
        var visibleMode = state.Mode == FirmwarePageMode.OperationInProgress
            ? !directInstallationSupported
                ? FirmwarePageMode.UnsupportedPlatform
                : vehicleConnected
                    ? FirmwarePageMode.Connected
                    : FirmwarePageMode.Disconnected
            : state.Mode;

        IsConnectedMode = visibleMode == FirmwarePageMode.Connected;
        IsDisconnectedMode = visibleMode == FirmwarePageMode.Disconnected;
        IsUnsupportedMode = visibleMode == FirmwarePageMode.UnsupportedPlatform;
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
        OperationProgress.HasStage = progress.Percentage.HasValue;
        OperationProgress.IsPowerCritical = progress.State is FirmwareOperationState.Erasing or FirmwareOperationState.Programming or FirmwareOperationState.Verifying;
        OperationProgress.TechnicalDetail = progress.TechnicalDetail;
        StatusMessage = OperationProgress.Stage;
    }

    partial void OnSelectedFirmwareChanged(FirmwareCatalogItemViewModel? value)
    {
        if (value is not null)
        {
            selectedFirmwareTarget = value.Entry;
        }

        OnPropertyChanged(nameof(CanInstallInitialDfuFirmware));
        InstallInitialDfuFirmwareCommand.NotifyCanExecuteChanged();
    }

    partial void OnSelectedDfuDeviceChanged(DfuDeviceItemViewModel? value)
    {
        OnPropertyChanged(nameof(CanInstallInitialDfuFirmware));
        InstallInitialDfuFirmwareCommand.NotifyCanExecuteChanged();
    }

    private IProgress<FirmwareProgress> CreateProgress()
    {
        return new Progress<FirmwareProgress>(progress => dispatcher.Dispatch(() =>
        {
            UpdateProgress(progress);
            ProgressMessage = BuildProgressMessage(progress);
        }));
    }

    private async Task ShowOperationDialogAsync(string title, CancellationTokenSource cancellation)
    {
        CloseOperationDialog();
        ProgressMessage = title + "…";
        progressDialog = await dialogService.DisplayProgressCancellableAsync(
            title,
            () => ProgressMessage,
            tokenSource: cancellation);
    }

    private void CloseOperationDialog()
    {
        progressDialog?.Dispose();
        progressDialog = null;
    }

    private static string BuildProgressMessage(FirmwareProgress progress)
    {
        var stage = StageText(progress);
        return string.IsNullOrWhiteSpace(progress.TechnicalDetail)
            ? stage
            : $"{stage}\n{progress.TechnicalDetail}";
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

    private static string DfuStageText(DfuProgress progress)
    {
        var stage = progress.State switch
        {
            DfuOperationState.LocatingTool => "Locating STM32CubeProgrammer",
            DfuOperationState.ResolvingArtifact => "Downloading the matching with_bl.hex firmware",
            DfuOperationState.InspectingHex => "Validating Intel HEX addresses and target evidence",
            DfuOperationState.WaitingForDevice => "Waiting for the STM32 DFU device",
            DfuOperationState.InspectingDevice => "Inspecting the STM32 device and driver",
            DfuOperationState.AwaitingConfirmation => "Checking the selected hardware target",
            DfuOperationState.Programming => progress.Percentage is double percentage ? $"Programming {percentage:0}% — do not disconnect power" : "Programming — do not disconnect power",
            DfuOperationState.Verifying => "Verifying programmed firmware — do not disconnect power",
            DfuOperationState.Detaching => "Resetting the flight controller",
            DfuOperationState.WaitingForApplication => "Waiting for ArduPilot to appear",
            DfuOperationState.Completed => "Initial ArduPilot installation completed",
            DfuOperationState.Cancelled => "STM32 DFU installation cancelled",
            DfuOperationState.Failed => "STM32 DFU installation failed",
            var _ => "Preparing STM32 DFU installation"
        };
        var detail = SanitizeDfuProgressDetail(progress.TechnicalDetail);
        return detail is null ? stage : $"{stage}\n{detail}";
    }

    private async Task RefreshDfuDevicesAsync(CancellationToken cancellationToken)
    {
        IReadOnlyList<DfuDeviceDescriptor> devices;
        try
        {
            devices = await dfuDeviceCatalog.GetDevicesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogWarning(exception, "Unable to refresh STM32 DFU devices after installation.");
            return;
        }

        var selectedId = SelectedDfuDevice?.Descriptor.ProviderId;
        await DispatchAsync(() =>
        {
            DfuDevices = devices.Select(device => new DfuDeviceItemViewModel(device)).ToArray();
            SelectedDfuDevice = selectedId is null
                ? DfuDevices.Count == 1 ? DfuDevices[0] : null
                : DfuDevices.FirstOrDefault(item => string.Equals(item.Descriptor.ProviderId, selectedId, StringComparison.OrdinalIgnoreCase));
            DfuStatus = DfuDevices.Count == 0
                ? "STM32 DFU device is no longer present. The controller has left ROM bootloader mode."
                : "STM32 DFU device is still present. Release BOOT/DFU, then reset or reconnect the controller.";
        });
    }

    private static string? SanitizeDfuProgressDetail(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        // CubeProgrammer draws its console progress bar with code-page glyphs. Those bytes
        // become replacement characters when redirected; keep useful ASCII status text only.
        var ascii = new string(value.Where(character => character is >= ' ' and <= '~').ToArray());
        var normalized = string.Join(' ', ascii.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        return normalized.Any(char.IsLetter) ? normalized : null;
    }

    private static string BuildDfuDiagnosticReport(
        DfuProgrammingResult result,
        FirmwareCatalogItemViewModel firmware,
        DfuDeviceDescriptor device)
    {
        var warnings = result.Warnings is { Count: > 0 } ? string.Join(", ", result.Warnings) : "None";
        return $"Operation: {result.OperationId}\n" +
               $"State: {result.State}\n" +
               $"Platform: {firmware.Platform}\n" +
               $"Firmware board ID: {firmware.BoardId}\n" +
               $"DFU device: VID_{device.VendorId:X4}&PID_{device.ProductId:X4}\n" +
               $"Programming succeeded: {result.ProgrammingSucceeded}\n" +
               $"Verification succeeded: {result.VerificationSucceeded}\n" +
               $"Application rediscovered: {result.ApplicationRediscovered}\n" +
               $"Failure: {result.Failure?.Code ?? "None"}\n" +
               $"Failure stage: {result.Failure?.Stage.ToString() ?? "None"}\n" +
               $"Failure detail: {result.Failure?.Message ?? "None"}\n" +
               $"Provider exit code: {result.ExitCode?.ToString() ?? "None"}\n" +
               $"Warnings: {warnings}";
    }

    /// <inheritdoc />
    public void Dispose()
    {
        Deactivate();
    }
}
