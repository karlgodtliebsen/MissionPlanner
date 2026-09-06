using System.Diagnostics;
using AsyncAwaitBestPractices;
using Avalonia.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using MissionPlanner.App.Presentation;
using MissionPlanner.App.Utilities.Dialogs;
using MissionPlanner.App.Utilities.Dispatching;
using MissionPlanner.Core.Vehicles.Abstractions;
using MissionPlanner.Firmware;
using MissionPlanner.Firmware.Catalog;
using MissionPlanner.Firmware.Compatibility;
using MissionPlanner.Firmware.Connected;
using MissionPlanner.Firmware.Devices;
using MissionPlanner.Firmware.Dfu;
using MissionPlanner.Firmware.Discovery;
using MissionPlanner.Firmware.Entry;
using MissionPlanner.Firmware.Installation;
using MissionPlanner.Firmware.Model;
using MissionPlanner.Firmware.Preparation;
using MissionPlanner.Firmware.Presentation;
using MissionPlanner.Library.EventHub.Abstractions;
using MissionPlanner.Library.Factory.Domain.Abstractions;
using DialogOptions = MissionPlanner.App.Utilities.Dialogs.DialogOptions;

namespace MissionPlanner.App.Views.InitSetup.InstallFirmware;

/// <summary>Drives connected and disconnected firmware installation experiences.</summary>
public sealed partial class InstallFirmwareViewModel : ViewModelBase
{
    /// <summary>Gets the catalogue panel.</summary>
    public FirmwareCatalogViewModel Catalogue
    {
        get;
    }
    /// <summary>Gets the custom panel.</summary>
    public CustomFirmwareViewModel Custom
    {
        get;
    }
    /// <summary>Gets the dfu panel.</summary>
    public STM32BootloaderViewModel Dfu
    {
        get;
    }
    /// <summary>Gets the help panel.</summary>
    public FirmwareHelpViewModel Help
    {
        get;
    }
    /// <summary>Gets the shared devices panel.</summary>
    public DetectedDeviceViewModel Devices => Catalogue.Devices;
    /// <summary>Gets the shared validated panel.</summary>
    public ValidatedPackageViewModel Validated => Catalogue.Validated;

    /// <summary>Gets the shared selected panel.</summary>
    public SelectedFirmwareViewModel Selected => Catalogue.Selected;

    private readonly IFirmwareCatalogService catalogService;
    private readonly IFirmwareInstallationService installationService;
    private readonly IFirmwarePreparationService preparationService;
    private readonly IDfuInstallationService dfuInstallationService;
    private readonly IDfuDeviceCatalog dfuDeviceCatalog;
    private readonly IDfuToolLocator dfuToolLocator;
    private readonly IEmbeddedBootloaderUpdateService bootloaderUpdateService;
    private readonly IFirmwareSerialDeviceCatalog deviceCatalog;
    private readonly IFirmwarePageModeResolver modeResolver;
    private readonly IActiveVehicleContext activeVehicle;
    private readonly IUserConfirmationService confirmation;
    private readonly IDialogService dialogService;
    private readonly IDomainFactory domainFactory;
    private readonly FirmwareDialogCoordinator firmwareDialogs;
    private readonly object refreshSync = new();
    private CancellationTokenSource? lifetime;
    private CancellationTokenSource? refreshCancellation;
    private CancellationTokenSource? operationCancellation;
    private IDisposable? progressDialog;
    private long refreshVersion;
    private int operationRunning;

    private bool disposed;
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
    /// <param name="activeVehicle"></param>
    /// <param name="confirmation"></param>
    /// <param name="dialogService">Displays the cancellable firmware-operation progress dialog.</param>
    /// <param name="domainFactory"></param>
    /// <param name="logger"></param>
    /// <param name="firmwareDialogs">Sequences operator confirmations and firmware progress windows.</param>
    /// <param name="catalogue">Owns catalogue choices and filters.</param>
    /// <param name="custom">Owns custom application packages.</param>
    /// <param name="dfu">Owns DFU devices and local HEX selection.</param>
    /// <param name="help">Owns firmware help and support links.</param>
    /// <param name="dispatcher">Marshals observable state to the UI thread.</param>
    /// <param name="eventHub">Provides base ViewModel event services.</param>
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
        IActiveVehicleContext activeVehicle,
        IUserConfirmationService confirmation,
        IDialogService dialogService,
        IDomainFactory domainFactory,
        ILogger<InstallFirmwareViewModel> logger, FirmwareDialogCoordinator firmwareDialogs,
        FirmwareCatalogViewModel catalogue, CustomFirmwareViewModel custom, STM32BootloaderViewModel dfu, FirmwareHelpViewModel help,
        IUiDispatcher dispatcher, IDomainEventHub eventHub) : base(logger, dispatcher, eventHub)
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
        this.activeVehicle = activeVehicle;
        this.confirmation = confirmation;
        this.dialogService = dialogService;
        this.domainFactory = domainFactory;
        this.firmwareDialogs = firmwareDialogs;
        Catalogue = catalogue;
        Custom = custom;
        Dfu = dfu;
        Help = help;
    }

    /// <summary>Gets the message displayed by the active firmware progress dialog.</summary>
    [ObservableProperty]
    public partial string ProgressMessage { get; private set; } = string.Empty;

    /// <summary>Gets whether disconnecting power could interrupt a flash write or verification.</summary>
    public bool IsPowerCritical => CurrentOperationState is FirmwareOperationState.Erasing or FirmwareOperationState.Programming or FirmwareOperationState.Verifying;

    /// <summary>Gets whether the current non-terminal work accepts a cancellation request.</summary>
    public bool CanRequestCancellation => IsCatalogRefreshRunning || IsOperationInProgress;

    /// <summary>Gets whether Shell navigation may safely leave this page.</summary>
    public bool CanNavigateAway => !IsOperationInProgress;

    public event Action<SelectionChangedEventArgs>? SelectionChanged;

    public void InvokeSelectionChanged(SelectionChangedEventArgs e)
    {
        SelectionChanged?.Invoke(e);
    }
    [ObservableProperty]
    public partial bool IsConnectedMode
    {
        get;
        private set;
    }

    [ObservableProperty]
    public partial bool IsDisconnectedMode
    {
        get;
        private set;
    }

    [ObservableProperty]
    public partial bool IsUnsupportedMode
    {
        get;
        private set;
    }

    [ObservableProperty]
    public partial bool IsOperationInProgress
    {
        get;
        private set;
    }

    [ObservableProperty]
    public partial bool IsCatalogRefreshRunning
    {
        get;
        private set;
    }

    [ObservableProperty]
    public partial bool IsCancellationDeferred
    {
        get;
        private set;
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsPowerCritical))]
    public partial FirmwareOperationState? CurrentOperationState
    {
        get;
        private set;
    }

    [ObservableProperty]
    public partial FirmwareContextHelp ContextHelp
    {
        get;
        private set;
    } =
        FirmwareContextHelpResolver.Resolve(new FirmwareSupportContext(SerialDevicePresent: false));

    [ObservableProperty]
    public partial bool CanUpdateBootloader
    {
        get;
        private set;
    }

    [ObservableProperty]
    public partial bool CanInstall
    {
        get;
        private set;
    }



    /// <summary>
    /// Starts observing connection state and refreshes disconnected data.
    /// </summary>
    public override async Task ActivateAsync()
    {
        if (active)
        {
            return;
        }
        if (disposed)
        {
            return;
        }
        active = true;

        Validated.IsFirmwareValidated = false;
        lifetime?.Dispose();
        lifetime = new CancellationTokenSource();
        SetBusy();
        SubscribePanels();
        Selected.SelectedFirmware = Catalogue.SelectedFirmware;
        Catalogue.IsVehicleConnected = activeVehicle.IsOnline;
        Custom.HasDevice = Devices.HasDevice;
        Custom.HasDfuBootLoader = Dfu.HasDfuBootLoader;
        activeVehicle.Changed += OnActiveVehicleChanged;
        SetMessages("Ready");
        var visibleMode = ApplyMode();
        if (visibleMode == FirmwarePageMode.Disconnected)
        {
            Catalogue.IsVehicleConnected = false;
            await RefreshSafelyAsync(false, lifetime.Token);
        }
        SelectionChanged += InstallFirmwareViewModel_SelectionChanged;
    }

    private void InstallFirmwareViewModel_SelectionChanged(SelectionChangedEventArgs obj)
    {
        CancelRefresh();
        Validated.Reset();
        Devices.Reset();
        Dfu.Reset();
        Catalogue.Reset();

    }

    /// <inheritdoc />
    public override Task DeactivateAsync()
    {
        Deactivate();
        return Task.CompletedTask;
    }

    private void Deactivate()
    {
        if (disposed)
        {
            return;
        }
        if (!active)
        {
            return;
        }

        active = false;
        UnsubscribePanels();
        SelectionChanged -= InstallFirmwareViewModel_SelectionChanged;
        CancelRefresh();
        Validated.Reset();
        Devices.Reset();
        Dfu.Reset();
        Catalogue.Reset();
        activeVehicle.Changed -= OnActiveVehicleChanged;
        Validated.IsFirmwareValidated = false;
        Catalogue.IsVehicleConnected = false;
        var current = lifetime;
        lifetime = null;

        current?.Cancel();
        current?.Dispose();
    }

    /// <inheritdoc />
    public override void Dispose()
    {
        Deactivate();
        disposed = true;
    }

    private async Task RefreshSafelyAsync(bool forceRefresh, CancellationToken cancellationToken, bool allOptions = false)
    {
        SetBusy();
        try
        {
            await RefreshAsync(forceRefresh, cancellationToken, allOptions);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Refresh failed");
        }
        finally
        {
            ResetBusy();
        }
    }

    partial void OnIsCatalogRefreshRunningChanged(bool value)
    {
        OnPropertyChanged(nameof(CanRequestCancellation));
        CancelCommand.NotifyCanExecuteChanged();
    }

    partial void OnIsOperationInProgressChanged(bool value)
    {
        OnPropertyChanged(nameof(CanNavigateAway));
        OnPropertyChanged(nameof(CanRequestCancellation));
        InstallCommand.NotifyCanExecuteChanged();
        UpdatePanelCapabilities();
        InstallDfuFirmwareCommand.NotifyCanExecuteChanged();
        UpdateBootloaderCommand.NotifyCanExecuteChanged();
        CancelCommand.NotifyCanExecuteChanged();
    }

    partial void OnCanInstallChanged(bool value)
    {
        InstallCommand.NotifyCanExecuteChanged();
        UpdatePanelCapabilities();
    }

    partial void OnCanUpdateBootloaderChanged(bool value)
    {
        UpdateBootloaderCommand.NotifyCanExecuteChanged();
    }

    [RelayCommand(CanExecute = nameof(CanStartInstall), AllowConcurrentExecutions = false)]
    private async Task InstallAsync(CancellationToken cancellationToken)
    {
        if ((Catalogue.SelectedFirmware is null && Custom.CustomPackage is null) || Interlocked.CompareExchange(ref operationRunning, 1, 0) != 0)
        {
            return;
        }

        using var ownedCancellation = BeginOperationCancellation(cancellationToken);
        try
        {
            SetOperation(true, FirmwareOperationState.Downloading);
            await ShowOperationDialogAsync("Installing firmware", ownedCancellation, deferUntilConfirmed: true);
            var target = Catalogue.SelectedFirmware?.Entry.Target;
            var prepared = Validated.PreparedFirmware is not null && ReferenceEquals(Validated.PreparedFirmware.ManifestEntry, Catalogue.SelectedFirmware?.Entry) ? Validated.PreparedFirmware : null;
            var request = new FirmwareInstallationRequest(
                new BootloaderEntryContext(new BootloaderDiscoveryRequest(
                        Devices.SelectedDevice?.Descriptor,
                        target?.UsbIdentifiers,
                        target?.BootloaderNames),
                    Devices.SelectedDevice?.Descriptor),
                prepared is null ? Catalogue.SelectedFirmware?.Entry.Artifact : null,
                Custom.CustomPackage ?? prepared?.Package,
                Custom.CustomPackage is not null ? FirmwareInstallationSource.LocalCustom : FirmwareInstallationSource.OfficialCatalogue,
                Custom.CustomPackage is not null
                    ? new FirmwareCompatibilityPolicy(!Custom.RequireExactBoardIdMatch)
                    : FirmwareCompatibilityPolicy.Strict,
                Custom.CustomPackage is not null ? Custom.CustomFirmwareName : null);

            var progress = CreateProgress();
            var result = await installationService.InstallAsync(request, progress, ownedCancellation.Token);
            var diagnosticsReport = result.DiagnosticReport?.CreateReport();

            var succeeded = result.State == FirmwareOperationState.Completed;
            var message = result.State == FirmwareOperationState.Completed
                ? result.ApplicationDevice is null
                    ? "Firmware installation completed; reconnect was not detected. Reconnect the flight controller manually."
                    : $"Firmware installation completed. ArduPilot returned on {result.ApplicationDevice.PortName}; reconnect is available."
                : result.Failure?.TechnicalDetail is { Length: > 0 } detail
                    ? $"Firmware installation {result.State}: {detail}"
                    : $"Firmware installation {result.State}";

            SetMessages(message);
            NotificationManager!.Show(message);
            if (succeeded)
            {
                var options = dialogService.CreateOptions("Firmware installation completed.", "Ok", null);
                var viewModel = domainFactory.Create<DiagnosticsReportViewModel, string, string>(diagnosticsReport ?? "", message);
                dialogService.ShowOverlayDialog<DiagnosticsReportView, DiagnosticsReportViewModel>(viewModel, options);
            }
            else
            {
                var options = dialogService.CreateOptions("Firmware installation failed.", "Ok", null);
                var viewModel = domainFactory.Create<DiagnosticsReportViewModel, string, string>(diagnosticsReport ?? "", message);
                dialogService.ShowOverlayDialog<DiagnosticsReportView, DiagnosticsReportViewModel>(viewModel, options);
            }
        }
        catch (OperationCanceledException) when (ownedCancellation.IsCancellationRequested)
        {
            SetMessages("Firmware installation cancelled.");
            NotificationManager!.Show(StatusMessage ?? "");
        }
        catch (Exception exception)
        {
            var message = $"Firmware installation failed";
            Debug.Print(message);
            Logger.LogError(exception, message);
            SetMessages(exception);
            NotificationManager!.Show(ErrorMessage ?? message);
            var options = dialogService.CreateOptions(message, "Ok", null);
            var viewModel = domainFactory.Create<DiagnosticsReportViewModel, string, string>(message, exception.Message);
            dialogService.ShowOverlayDialog<DiagnosticsReportView, DiagnosticsReportViewModel>(viewModel, options);
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
        return Validated.IsFirmwareValidated != false && Devices.SelectedDevice is not null && CanInstall && (Catalogue.SelectedFirmware is not null || Custom.CustomPackage is not null) && !IsOperationInProgress;
    }

    private bool CanStartDfuInstall()
    {
        return
            (!string.IsNullOrWhiteSpace(Dfu.LocalDfuFirmwarePath) ? !string.IsNullOrWhiteSpace(Dfu.LocalDfuPlatform) : Catalogue.SelectedFirmware is not null)
            &&
            Dfu.SelectedDfuDevice?.Descriptor.DriverState == DfuDriverState.PresentReady && !IsOperationInProgress;
    }

    [RelayCommand(CanExecute = nameof(CanStartDfuInstall), AllowConcurrentExecutions = false)]
    private async Task InstallDfuFirmwareAsync(CancellationToken cancellationToken)
    {
        var hasLocalHex = !string.IsNullOrWhiteSpace(Dfu.LocalDfuFirmwarePath);
        if ((!hasLocalHex && Catalogue.SelectedFirmware is null) || Dfu.SelectedDfuDevice is null ||
            Interlocked.CompareExchange(ref operationRunning, 1, 0) != 0)
        {
            return;
        }

        // An explicitly loaded local image must always take precedence over a catalogue
        // row that may have been restored or automatically selected during refresh.
        var selectedFirmware = hasLocalHex ? null : Catalogue.SelectedFirmware;
        var selectedDfuDevice = Dfu.SelectedDfuDevice;
        var platform = hasLocalHex ? Dfu.LocalDfuPlatform?.Trim() : selectedFirmware?.Platform;
        var boardId = selectedFirmware?.BoardId;
        var localHexPath = hasLocalHex ? Dfu.LocalDfuFirmwarePath : null;
        if (string.IsNullOrWhiteSpace(platform))
        {
            SetMessages("Enter the exact ArduPilot platform for the selected local HEX file.");
            Interlocked.Exchange(ref operationRunning, 0);
            return;
        }

        var requiredPhrase = $"FLASH {platform}";
        var options = dialogService.CreateOptions("Confirm initial ArduPilot installation", "Continue", null);
        var message = $"This replaces Betaflight and installs ArduPilot plus its bootloader for {platform}{(boardId is int id ? $"(board ID {id})" : string.Empty)}. Type exactly: {requiredPhrase}";
        var phrase = await dialogService.PromptAsync(options, message, string.Empty, cancellationToken);
        if (!string.Equals(phrase?.Trim(), requiredPhrase, StringComparison.Ordinal))
        {
            SetMessages(phrase is null ? "Initial DFU installation cancelled." : $"Confirmation did not match {requiredPhrase}.");
            Interlocked.Exchange(ref operationRunning, 0);
            return;
        }

        using var ownedCancellation = BeginOperationCancellation(cancellationToken);
        try
        {
            SetOperation(true, FirmwareOperationState.Downloading);
            await ShowOperationDialogAsync("Installing ArduPilot through STM32 DFU", ownedCancellation, deferUntilConfirmed: true);
            var progress = new Progress<DfuProgress>(value => Dispatcher.Dispatch(() =>
            {
                ProgressMessage = DfuStageText(value);
                SetMessages(ProgressMessage);
            }));
            var result = await dfuInstallationService.InstallAsync(
                new DfuInstallationRequest(platform, boardId, selectedDfuDevice.Descriptor, ConfirmationPhrase: requiredPhrase,
                    ManifestEntry: selectedFirmware?.Entry, LocalHexPath: localHexPath), progress, ownedCancellation.Token);

            await RefreshDfuDevicesAsync(CancellationToken.None);

            var diagnosticReport = BuildDfuDiagnosticReport(result, platform, boardId, selectedDfuDevice.Descriptor);
            SetMessages(result.State == DfuOperationState.Completed
                ? result.ApplicationRediscovered
                    ? "Initial ArduPilot installation completed and the application device was detected."
                    : "Programming and verification completed. Reconnect or reset the controller if ArduPilot does not appear."
                : result.Failure?.Message ?? $"STM32 DFU installation {result.State}.");

            options = dialogService.CreateOptions("Firmware installation completed.", "Ok", null);
            var viewModel = domainFactory.Create<DiagnosticsReportViewModel, string>(diagnosticReport ?? "");
            dialogService.ShowOverlayDialog<DiagnosticsReportView, DiagnosticsReportViewModel>(viewModel, options);

        }
        catch (OperationCanceledException) when (ownedCancellation.IsCancellationRequested)
        {
            SetMessages("Initial DFU installation cancelled.");
        }
        catch (Exception exception)
        {

            message = $"Initial STM32 DFU installation failed: {exception.Message}";
            Logger.LogError(exception, "Initial STM32 DFU installation failed.");
            SetMessages(exception);
            options = dialogService.CreateOptions("Initial STM32 DFU installation failed.", "Ok", null);
            var viewModel = domainFactory.Create<DiagnosticsReportViewModel, string>(message ?? "");
            dialogService.ShowOverlayDialog<DiagnosticsReportView, DiagnosticsReportViewModel>(viewModel, options);

        }
        finally
        {
            CloseOperationDialog();
            EndOperationCancellation(ownedCancellation);
            Interlocked.Exchange(ref operationRunning, 0);
            SetOperation(false, null);
        }
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
            SetMessages(result.Code + (result.RebootRequired ? " — reboot the flight controller to use the new bootloader." : string.Empty));


        }
        catch (Exception exception)
        {
            Logger.LogError(exception, "Embedded bootloader update failed.");
            SetMessages(null, exception.Message);
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
        // Do not use IsDisconnectedMode here. ApplyMode updates that UI property through
        // the dispatcher, so it may still contain the previous value during activation.
        if (!OperatingSystem.IsWindows() || activeVehicle.IsOnline || IsOperationInProgress)
        {
            return;
        }
        if (Interlocked.CompareExchange(ref operationRunning, 1, 0) != 0)
        {
            return;
        }
        Debug.Print("InstallFirmware RefreshAsync");
        var (version, refreshCancellationToken) = BeginRefresh(cancellationToken);
        using var ownedCancellation = BeginOperationCancellation(refreshCancellationToken);
        var refreshToken = ownedCancellation.Token;

        try
        {
            await DispatchAsync(() =>
            {
                Validated.IsFirmwareValidated = false;
                IsCatalogRefreshRunning = true;
                SetMessages("Loading firmware catalogue…");
                NotificationManager?.Show(StatusMessage!);
            });

            SetOperation(true, FirmwareOperationState.LoadingCatalog);
            await ShowOperationDialogAsync("Loading firmware catalogue", ownedCancellation);

            var channel = Catalogue.SelectedChannel;
            await Task.Yield();
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
            Debug.Print("InstallFirmware RefreshAsync Completed task 1 & 2");
            await Task.Yield();

            var entries = catalog.Entries.Where(entry =>
                entry.Target.VehicleType != FirmwareVehicleType.Unknown
                &&
                entry.Artifact.Format is FirmwareImageFormat.Apj or FirmwareImageFormat.Px4).ToArray();

            var deviceItems = await Task.Run(() => DetectedDeviceViewModel.CreateItems(entries, devices), refreshToken).ConfigureAwait(false);

            Debug.Print($"InstallFirmware RefreshAsync Completed task 3 with entries count: {entries.Length}");

            await Task.Yield();

            refreshToken.ThrowIfCancellationRequested();
            if (!IsLatestRefresh(version))
            {
                return;
            }

            await DispatchAsync(() =>
            {
                Catalogue.SetCatalogue(entries, devices, allOptions);
                Custom.CustomPackage = null;

                Devices.DetectedDevices = deviceItems;
                Dfu.DfuDevices = dfuDevices.Select(device => new DfuDeviceItemViewModel(device)).ToArray();
                Dfu.SelectedDfuDevice = Dfu.DfuDevices.Count == 1 ? Dfu.DfuDevices[0] : null;
                Dfu.DfuStatus = Dfu.DfuDevices.Count == 0
                    ? "No STM32 DFU device detected. Hold BOOT/DFU while connecting USB, or use the board's documented BOOT and RESET sequence, then refresh."
                    : dfuTool.Availability != DfuToolAvailability.Available
                        ? dfuTool.Diagnostic ?? "Install STM32CubeProgrammer and its bundled DFU driver before continuing."
                        : Dfu.SelectedDfuDevice?.Descriptor.DriverState == DfuDriverState.PresentReady
                            ? "STM32 DFU device and STM32CubeProgrammer are ready."
                            : "Select a DFU device and resolve any indicated driver problem.";

                Devices.SelectRecommendedDevice();
                SetMessages(catalog.IsStale ? "Showing cached firmware catalogue" : $"{Catalogue.FirmwareChoices.Count} vehicle firmware choices available");
                NotificationManager?.Show(StatusMessage!);
                UpdateContextHelp();
            });
        }
        catch (OperationCanceledException) when (refreshToken.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            Debug.Print("InstallFirmware Firmware catalogue refresh failed.\n" + exception.Message);

            Logger.LogError(exception, "InstallFirmware Firmware catalogue refresh failed.");
            await DispatchAsync(() =>
            {
                if (IsLatestRefresh(version))
                {
                    SetMessages(exception);
                    NotificationManager?.Show(ErrorMessage!);

                }
            });
        }
        finally
        {
            await DispatchAsync(() =>
            {
                // CancelRefresh invalidates the version, but this operation still owns
                // the refresh flag and dialog until its finally block completes.
                IsCatalogRefreshRunning = false;
                CloseOperationDialog();
                EndOperationCancellation(ownedCancellation);
                SetOperation(false, null);
                Interlocked.Exchange(ref operationRunning, 0);
            });
        }
    }

    private (long Version, CancellationToken Token) BeginRefresh(CancellationToken cancellationToken)
    {
        Debug.Print("InstallFirmware BeginRefresh");

        (long Version, CancellationToken Token) result;
        lock (refreshSync)
        {
            refreshCancellation?.Cancel();
            refreshCancellation?.Dispose();
            refreshCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            result = (++refreshVersion, refreshCancellation.Token);
        }
        Debug.Print("InstallFirmware BeginRefresh Exit");
        return result;
    }

    private void CancelRefresh()
    {
        Debug.Print("InstallFirmware CancelRefresh");

        lock (refreshSync)
        {
            refreshVersion++;
            refreshCancellation?.Cancel();
            refreshCancellation?.Dispose();
            refreshCancellation = null;
        }
        Debug.Print("InstallFirmware CancelRefresh Exit");
    }

    private bool IsLatestRefresh(long version)
    {
        lock (refreshSync)
        {
            return version == refreshVersion;
        }
    }

    private async Task DispatchAsync(Action action)
    {
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        await Dispatcher.DispatchAsync(() =>
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
        });

    }

    [RelayCommand]
    private async Task DownloadAndValidateAsync(CancellationToken cancellationToken)
    {
        Validated.IsFirmwareValidated = false;
        if (Catalogue.SelectedFirmware is null || IsOperationInProgress)
        {
            return;
        }

        using var ownedCancellation = BeginOperationCancellation(cancellationToken);
        try
        {
            SetOperation(true, FirmwareOperationState.Downloading);
            await ShowOperationDialogAsync("Downloading firmware", ownedCancellation);
            Validated.PreparedFirmware = await preparationService.PrepareAsync(new FirmwarePreparationRequest(Catalogue.SelectedFirmware.Entry), CreateProgress(), ownedCancellation.Token);
            SetMessages(Validated.PreparedFirmware.WasCacheHit ? "Validated cached firmware package." : "Firmware downloaded and validated.");
            Validated.IsFirmwareValidated = true;
        }
        catch (OperationCanceledException) when (ownedCancellation.IsCancellationRequested)
        {
            SetMessages("Firmware download and validation cancelled.", null);
        }
        catch (Exception exception)
        {
            Logger.LogWarning(exception, "Firmware preparation failed.");
            SetMessages(null, exception.Message);
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
            SetMessages("Firmware catalogue refresh cancelled.");
        }

        var cancellation = operationCancellation;
        if (cancellation is null || cancellation.IsCancellationRequested)
        {
            return;
        }

        IsCancellationDeferred = CurrentOperationState is FirmwareOperationState.Erasing or FirmwareOperationState.Programming or FirmwareOperationState.Verifying or FirmwareOperationState.Rebooting;
        SetMessages(IsCancellationDeferred
            ? "Cancellation requested. The flash will continue through verify and reboot before stopping at a safe boundary. Do not disconnect power."
            : "Cancelling firmware operation…");

        cancellation.Cancel();
    }

    private void OnActiveVehicleChanged(Core.Vehicles.ActiveVehicleChangedEventArgs e)
    {
        Dispatcher.Dispatch(() =>
        {
            if (!active)
            {
                return;
            }

            Catalogue.IsVehicleConnected = e.Current.IsOnline;
            var visibleMode = ApplyMode();
            // Disconnecting to enter the bootloader is part of the active flash
            // operation; do not start a competing device/catalogue refresh.
            if (IsOperationInProgress)
            {
                return;
            }
            if (visibleMode == FirmwarePageMode.Disconnected && lifetime is { } currentLifetime)
            {
                RefreshSafelyAsync(false, currentLifetime.Token).SafeFireAndForget();
                return;
            }

            ResetBusy();
        });
    }

    private void SetOperation(bool operationActive, FirmwareOperationState? stage)
    {
        Dispatcher.Dispatch(() =>
        {
            IsOperationInProgress = operationActive;
            CurrentOperationState = stage;
            if (!operationActive)
            {
                IsCancellationDeferred = false;
            }
        });
        ApplyMode(stage);
    }

    private FirmwarePageMode ApplyMode(FirmwareOperationState? stage = null)
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

        Dispatcher.Dispatch(() =>
        {
            IsConnectedMode = visibleMode == FirmwarePageMode.Connected;
            IsDisconnectedMode = visibleMode == FirmwarePageMode.Disconnected;
            IsUnsupportedMode = visibleMode == FirmwarePageMode.UnsupportedPlatform;
            CanInstall = state.CanInstallApplicationFirmware;
            CanUpdateBootloader = state.CanUpdateEmbeddedBootloader;
            UpdatePanelCapabilities();
        });
        Task.Yield();
        return visibleMode;
    }

    private void UpdateProgress(FirmwareProgress progress)
    {
        Dispatcher.Dispatch(() =>
            {
                CurrentOperationState = progress.State;
                SetMessages(StageText(progress));
            });
    }

    private IProgress<FirmwareProgress> CreateProgress()
    {
        return new Progress<FirmwareProgress>(progress => Dispatcher.Dispatch(() =>
        {
            UpdateProgress(progress);
            ProgressMessage = BuildProgressMessage(progress);
        }));
    }

    private async Task ShowOperationDialogAsync(string title, CancellationTokenSource cancellation, bool deferUntilConfirmed = false)
    {
        CloseOperationDialog();
        ProgressMessage = title + "…";

        progressDialog = await firmwareDialogs.BeginAsync(() => dialogService.DisplayProgressCancellableAsync(
            () => ProgressMessage,
            new DialogOptions()
            {
                Title = ProgressMessage
            },
            cancellationToken: cancellation.Token), deferUntilConfirmed, cancellation.Token);
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
    }

    private void UpdateContextHelp(bool packageBoardMismatch = false)
    {
        ContextHelp = FirmwareContextHelpResolver.Resolve(new FirmwareSupportContext(
            SerialDevicePresent: Devices.DetectedDevices.Count > 0,
            TargetAmbiguous: Catalogue.FirmwareChoices.Count > 0 && Catalogue.SelectedFirmware is null,
            PackageBoardMismatch: packageBoardMismatch,
            Channel: Catalogue.SelectedChannel,
            CustomPackageSelected: Custom.CustomPackage is not null));
    }

    private static string StageText(FirmwareProgress progress)
    {
        return progress.State switch
        {
            FirmwareOperationState.Downloading => "Downloading firmware",
            FirmwareOperationState.WaitingForDevice => "Waiting for flight controller",
            FirmwareOperationState.CheckingForBootloader => "Checking for an ArduPilot bootloader",
            FirmwareOperationState.RequestingBootloaderReboot => "Requesting ArduPilot reboot to bootloader",
            FirmwareOperationState.WaitingForBootloader => "Waiting for the ArduPilot bootloader",
            FirmwareOperationState.ManualBootloaderReconnectRequired => "Automatic bootloader entry failed; reset or reconnect the controller",
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
            Logger.LogWarning(exception, "Unable to refresh STM32 DFU devices after installation.");
            return;
        }

        var selectedId = Dfu.SelectedDfuDevice?.Descriptor.ProviderId;
        await DispatchAsync(() =>
        {
            Dfu.DfuDevices = devices.Select(device => new DfuDeviceItemViewModel(device)).ToArray();
            Dfu.SelectedDfuDevice = selectedId is null
                ? Dfu.DfuDevices.Count == 1 ? Dfu.DfuDevices[0] : null
                : Dfu.DfuDevices.FirstOrDefault(item => string.Equals(item.Descriptor.ProviderId, selectedId, StringComparison.OrdinalIgnoreCase));
            Dfu.DfuStatus = Dfu.DfuDevices.Count == 0
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

    private static string BuildDfuDiagnosticReport(DfuProgrammingResult result, string platform, int? boardId, DfuDeviceDescriptor device)
    {
        var warnings = result.Warnings is { Count: > 0 } ? string.Join(", ", result.Warnings) : "None";
        return $"Operation: {result.OperationId}\n" +
               $"State: {result.State}\n" +
               $"Platform: {platform}\n" +
               $"Firmware board ID: {boardId?.ToString() ?? "Not available for local HEX"}\n" +
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

    private void SubscribePanels()
    {
        Catalogue.SelectionChanged += OnCatalogueSelection;
        Catalogue.ChannelChanged += OnCatalogueChannel;
        Catalogue.FiltersChanged += OnCatalogueFilters;
        Devices.SelectionChanged += OnDeviceSelection;
        Custom.PackageChanged += OnCustomPackage;
        Custom.OperationRequested += OnPanelOperation;
        Dfu.SelectionChanged += OnDfuSelection;
        Dfu.LocalFirmwareChanged += OnDfuFirmware;
        Dfu.PlatformChanged += OnDfuPlatform;
        Catalogue.OperationRequested += OnPanelOperation;
        Devices.OperationRequested += OnPanelOperation;

        Dfu.OperationRequested += OnPanelOperation;
        Validated.OperationRequested += OnPanelOperation;
        Selected.OperationRequested += OnPanelOperation;
    }

    private void UnsubscribePanels()
    {
        Catalogue.SelectionChanged -= OnCatalogueSelection;
        Catalogue.ChannelChanged -= OnCatalogueChannel;
        Catalogue.FiltersChanged -= OnCatalogueFilters;
        Devices.SelectionChanged -= OnDeviceSelection;
        Custom.PackageChanged -= OnCustomPackage;
        Custom.OperationRequested -= OnPanelOperation;
        Dfu.SelectionChanged -= OnDfuSelection;
        Dfu.LocalFirmwareChanged -= OnDfuFirmware;
        Dfu.PlatformChanged -= OnDfuPlatform;
        Catalogue.OperationRequested -= OnPanelOperation;
        Devices.OperationRequested -= OnPanelOperation;
        Dfu.OperationRequested -= OnPanelOperation;
        Validated.OperationRequested -= OnPanelOperation;
        Selected.OperationRequested -= OnPanelOperation;
    }

    private void OnPanelOperation(FirmwarePanelRequest request)
    {
        request.Completion = request.Action switch
        {
            FirmwarePanelAction.Refresh => RefreshSafelyAsync(true, request.CancellationToken, request.AllOptions),
            FirmwarePanelAction.Download => DownloadAndValidateAsync(request.CancellationToken),
            FirmwarePanelAction.Install when CanStartInstall() => InstallAsync(request.CancellationToken),
            FirmwarePanelAction.InstallDfu when CanStartDfuInstall() => InstallDfuFirmwareAsync(request.CancellationToken),
            _ => Task.CompletedTask
        };
    }
    private void OnCatalogueSelection(FirmwareCatalogItemViewModel? value)
    {
        Selected.SelectedFirmware = value;
        Validated.PreparedFirmware = null;
        Validated.IsFirmwareValidated = false;
        if (value is not null)
        {
            Custom.CustomPackage = null;
        }
        UpdatePanelCapabilities();
        UpdateContextHelp();
    }
    private void OnCatalogueChannel(FirmwareReleaseChannel value)
    {
        UpdateContextHelp();
        if (lifetime is not null && IsDisconnectedMode)
        {
            RefreshSafelyAsync(false, lifetime.Token).SafeFireAndForget();
        }
    }
    private void OnCatalogueFilters(bool value)
    {
        Validated.IsFirmwareValidated = false;
        UpdatePanelCapabilities();
    }
    private void OnDeviceSelection(FirmwareDeviceItemViewModel? value)
    {
        Custom.HasDevice = value is not null;
        UpdatePanelCapabilities();
    }
    private void OnCustomPackage(ApjFirmwarePackage? value)
    {
        Validated.PreparedFirmware = null;
        if (value is not null)
        {
            Catalogue.ClearSelection();
            Dfu.LocalDfuFirmwarePath = null;
            Dfu.LocalDfuFirmwareName = null;
        }
        Validated.IsFirmwareValidated = value is not null;
        UpdatePanelCapabilities();
        UpdateContextHelp();
    }
    private void OnDfuSelection(DfuDeviceItemViewModel? value)
    {
        Custom.HasDfuBootLoader = value is not null;
        OnPropertyChanged(nameof(HasDfuBootLoader));
        UpdatePanelCapabilities();
    }
    private void OnDfuFirmware(string? value)
    {
        if (value is not null)
        {
            Custom.CustomPackage = null;
            Catalogue.ClearSelection();
        }
        UpdatePanelCapabilities();
    }
    private void OnDfuPlatform(string? value)
    {
        UpdatePanelCapabilities();
    }

    private void UpdatePanelCapabilities()
    {
        if (!active)
        {
            return;
        }
        Devices.CanInstall = CanStartInstall();
        Validated.CanInstall = CanStartInstall();
        Dfu.CanInstallDfu = CanStartDfuInstall();
        InstallCommand.NotifyCanExecuteChanged();
        InstallDfuFirmwareCommand.NotifyCanExecuteChanged();
    }
    /// <summary>Gets whether the DFU tab has a selected device.</summary>
    public bool HasDfuBootLoader => Dfu.HasDfuBootLoader;
}
