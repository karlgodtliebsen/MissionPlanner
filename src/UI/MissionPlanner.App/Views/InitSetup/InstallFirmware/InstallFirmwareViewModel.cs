using System.Diagnostics;
using AsyncAwaitBestPractices;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using MissionPlanner.App.Presentation;
using MissionPlanner.App.Utilities.Dialogs;
using MissionPlanner.App.Utilities.Dispatching;
using MissionPlanner.App.Views.InitSetup.InstallFirmware.SubViews;
using MissionPlanner.Core.Vehicles.Abstractions;
using MissionPlanner.Firmware;
using MissionPlanner.Firmware.Compatibility;
using MissionPlanner.Firmware.Connected;
using MissionPlanner.Firmware.Dfu;
using MissionPlanner.Firmware.Discovery;
using MissionPlanner.Firmware.Entry;
using MissionPlanner.Firmware.Installation;
using MissionPlanner.Firmware.Model;
using MissionPlanner.Firmware.Preparation;
using MissionPlanner.Firmware.Presentation;
using MissionPlanner.Library.EventHub.Abstractions;
using MissionPlanner.Library.Factory.Domain.Abstractions;

namespace MissionPlanner.App.Views.InitSetup.InstallFirmware;

/// <summary>Drives connected and disconnected firmware installation experiences.</summary>
public sealed partial class InstallFirmwareViewModel : ViewModelBase
{


    private bool UsesLocalDfuHex => SelectedDfuTabIndex == (int)Stm32DfuSection.Custom;
    private readonly IFirmwarePreparationService preparationService;
    private readonly FirmwareLandingViewModel landing;
    private readonly Firmware.Betaflight.IBetaflightArduPilotCompatibilityProvider betaflightCompatibility;
    private readonly Firmware.Betaflight.IFirmwareDeviceIdentityService deviceIdentity;
    private readonly Firmware.Betaflight.IBetaflightDfuHandoff dfuHandoff;
    private readonly IDfuInstallationService dfuInstallationService;
    private readonly IDfuArtifactResolver dfuArtifactResolver;
    private readonly Firmware.Operations.IFirmwareOperationCoordinator firmwareOperations;
    private readonly IEmbeddedBootloaderUpdateService bootloaderUpdateService;
    private readonly IFirmwarePageModeResolver modeResolver;
    private readonly IActiveVehicleContext activeVehicle;
    private readonly IUserConfirmationService confirmation;
    private readonly IDialogService dialogService;
    private readonly IDomainFactory domainFactory;
    private readonly IFirmwareInstallationService installationService;
    private readonly FirmwareDialogCoordinator firmwareDialogs;
    private CancellationTokenSource? lifetime;
    private CancellationTokenSource? operationCancellation;
    private IDisposable? progressDialog;
    private int operationRunning;
    private bool disposed;
    private bool active;



    /// <summary>Gets the catalogue panel.</summary>
    public FirmwareCatalogueViewModel OnlineFirmwareModel
    {
        get;
        init;
    }
    /// <summary>Gets the custom panel.</summary>
    public CustomFirmwareViewModel LocalFirmwareModel
    {
        get;
        init;
    }
    /// <summary>Gets the dfu panel.</summary>
    public STM32BootloaderViewModel DfuModel
    {
        get;
    }
    /// <summary>Gets the help panel.</summary>
    public FirmwareHelpViewModel HelpModel
    {
        get;
    }

    /// <summary>
    /// Gets the shared validated panel.
    /// </summary>
    public ValidatedPackageViewModel ValidatedModel
    {
        get;
    }
    /// <summary>Gets the selected release details.</summary>
    public SelectedFirmwareViewModel SelectedFirmwareModel
    {
        get;
    }

    /// <summary>
    /// Gets the shared devices panel.
    /// </summary>
    public DetectedDeviceViewModel DevicesModel
    {
        get;
    }


    ///// <summary>Gets the shared devices panel.</summary>
    //public DetectedDeviceViewModel DevicesModel => OnlineFirmwareModel.DevicesModel;
    ///// <summary>Gets the shared validated panel.</summary>
    //public ValidatedPackageViewModel ValidatedPackageModel => OnlineFirmwareModel.ValidatedPackageModel;

    /// <summary>Gets the shared selected panel.</summary>
   // public SelectedFirmwareViewModel SelectedFirmwareModel => OnlineFirmwareModel.SelectedFirmwareModel;

    //public CustomFirmwareViewModel CustomFirmware => LocalFirmwareModel;



    /// <summary>
    /// Gets or sets the selected top-level installation context.
    /// </summary>
    [ObservableProperty]
    public partial int SelectedSectionIndex
    {
        get; set;
    }

    partial void OnSelectedSectionIndexChanged(int value)
    {
        OnlineFirmwareModel.IsDfuContext = value == (int)FirmwareSection.Stm32Dfu;
    }
    /// <summary>Gets or sets the selected STM32 workflow: device, catalogue, or custom HEX.</summary>
    [ObservableProperty]
    public partial int SelectedDfuTabIndex
    {
        get; set;
    }

    partial void OnSelectedDfuTabIndexChanged(int value)
    {
        DfuModel.PreparedArtifact = null;
        UpdatePanelCapabilities();
    }

    /// <summary>
    /// Initializes the firmware page.
    /// </summary>
    /// <param name="installationService"></param>
    /// <param name="preparationService"></param>
    /// <param name="dfuInstallationService"></param>
    /// <param name="dfuArtifactResolver">Resolves and inspects combined HEX previews.</param>
    /// <param name="firmwareOperations">Prevents concurrent firmware resource ownership.</param>
    /// <param name="bootloaderUpdateService"></param>
    /// <param name="modeResolver"></param>
    /// <param name="activeVehicle"></param>
    /// <param name="confirmation"></param>
    /// <param name="dialogService">Displays the cancellable firmware-operation progress dialog.</param>
    /// <param name="domainFactory"></param>
    /// <param name="logger"></param>
    /// <param name="firmwareDialogs">Sequences operator confirmations and firmware progress windows.</param>
    /// <param name="devices">Owns the detected devices panel.</param>
    /// <param name="validated">Owns the validated firmware panel.</param>
    /// <param name="selected">Owns the selected firmware panel.</param>
    /// <param name="onlineFirmwareModel">Owns catalogue choices and filters.</param>
    /// <param name="landing">Owns device status and boot-mode entry requests.</param>
    /// <param name="betaflightCompatibility">Provides reviewed exact-board mappings, when available.</param>
    /// <param name="deviceIdentity">Verifies the selected serial device before requesting DfuModel.</param>
    /// <param name="dfuHandoff">Reboots and correlates the selected physical controller.</param>
    /// <param name="localFirmwareModel">Owns custom application packages.</param>
    /// <param name="dfu">Owns DFU devices and local HEX selection.</param>
    /// <param name="help">Owns firmware help and support links.</param>
    /// <param name="dispatcher">Marshals observable state to the UI thread.</param>
    /// <param name="eventHub">Provides base ViewModel event services.</param>
    public InstallFirmwareViewModel(
        IFirmwareInstallationService installationService,
        IFirmwarePreparationService preparationService,
        IDfuInstallationService dfuInstallationService,
        IDfuArtifactResolver dfuArtifactResolver,
        Firmware.Operations.IFirmwareOperationCoordinator firmwareOperations,
        IEmbeddedBootloaderUpdateService bootloaderUpdateService,
        IFirmwarePageModeResolver modeResolver,
        IActiveVehicleContext activeVehicle,
        IUserConfirmationService confirmation,
        IDialogService dialogService,
        IDomainFactory domainFactory,
        FirmwareDialogCoordinator firmwareDialogs,

        DetectedDeviceViewModel devices,
        ValidatedPackageViewModel validated,
        SelectedFirmwareViewModel selected,
        FirmwareCatalogueViewModel onlineFirmwareModel,
        FirmwareLandingViewModel landing,
        CustomFirmwareViewModel localFirmwareModel,
        STM32BootloaderViewModel dfu,
        FirmwareHelpViewModel help,

        Firmware.Betaflight.IBetaflightArduPilotCompatibilityProvider betaflightCompatibility,
        Firmware.Betaflight.IFirmwareDeviceIdentityService deviceIdentity,
        Firmware.Betaflight.IBetaflightDfuHandoff dfuHandoff,
        IUiDispatcher dispatcher,
        IDomainEventHub eventHub,
        ILogger<InstallFirmwareViewModel> logger
        ) : base(logger, dispatcher, eventHub)
    {
        this.installationService = installationService;
        this.preparationService = preparationService;
        this.dfuInstallationService = dfuInstallationService;
        this.dfuArtifactResolver = dfuArtifactResolver;
        this.firmwareOperations = firmwareOperations;
        this.bootloaderUpdateService = bootloaderUpdateService;
        this.modeResolver = modeResolver;
        this.activeVehicle = activeVehicle;
        this.confirmation = confirmation;
        this.dialogService = dialogService;
        this.domainFactory = domainFactory;
        this.firmwareDialogs = firmwareDialogs;
        this.betaflightCompatibility = betaflightCompatibility;
        this.deviceIdentity = deviceIdentity;
        this.dfuHandoff = dfuHandoff;

        this.landing = landing;

        DevicesModel = devices;
        ValidatedModel = validated;
        SelectedFirmwareModel = selected;
        OnlineFirmwareModel = onlineFirmwareModel;
        LocalFirmwareModel = localFirmwareModel;
        DfuModel = dfu;
        HelpModel = help;
    }

    /// <summary>
    /// Gets whether a serial controller is selected.
    /// </summary>

    public bool HasDevice => LocalFirmwareModel.HasDevice;


    [RelayCommand(CanExecute = nameof(HasDevice))]
    public async Task LoadLocalFirmwareAsync(CancellationToken cancellationToken)
    {
        await LocalFirmwareModel.LoadCustomFirmwareAsync(cancellationToken);
    }


    [RelayCommand]
    private void ClearFirmwareSelection()
    {

        LocalFirmwareModel.Reset();
        OnlineFirmwareModel.Reset();
        ValidatedModel.Reset();
        DevicesModel.Reset();
        DfuModel.Reset();
        SetMessages("Firmware selection cleared.");
    }

    /// <summary>Gets whether the parent permits installation.</summary>
    [ObservableProperty, NotifyCanExecuteChangedFor(nameof(InstallCommand))]
    public partial bool CanInstallLocalFirmware
    {
        get; set;
    }


    [RelayCommand(CanExecute = nameof(CanInstallLocalFirmware))]
    public Task InstallLocalFirmwareAsync(CancellationToken cancellationToken)
    {
        return LocalFirmwareModel.InstallAsync(cancellationToken);
    }


    [RelayCommand]
    public async Task ShowOnlineFirmwareSelectorAsync(CancellationToken cancellationToken)
    {
        var options = dialogService.CreateOptions("Select Online Firmware", "Close", "Cancel");
        options.FullScreen = true;
        var result = await dialogService.ShowOverlayDialogAsync<FirmwareCatalogueView, FirmwareCatalogueViewModel>(
            OnlineFirmwareModel,
            options,
            cancellationToken: cancellationToken);
    }

    [RelayCommand]
    public async Task ShowLocalFirmwareSelectorAsync(CancellationToken cancellationToken)
    {
        await LocalFirmwareModel.LoadCustomFirmwareAsync(cancellationToken);
    }

    /// <summary>Gets the message displayed by the active firmware progress dialog.</summary>
    [ObservableProperty]
    public partial string ProgressMessage { get; private set; } = string.Empty;

    /// <summary>
    /// Gets whether disconnecting power could interrupt a flash write or verification.
    /// </summary>
    public bool IsPowerCritical => CurrentOperationState is FirmwareOperationState.Erasing or FirmwareOperationState.Programming or FirmwareOperationState.Verifying;

    /// <summary>
    /// Gets whether the current non-terminal work accepts a cancellation request.
    /// </summary>
    public bool CanRequestCancellation => IsCatalogRefreshRunning || IsOperationInProgress;

    /// <summary>
    /// Gets whether Shell navigation may safely leave this page.
    /// </summary>
    public bool CanNavigateAway => !IsOperationInProgress;

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

    /// <summary>Gets the catalogue panel's read state for page-wide cancellation controls.</summary>
    public bool IsCatalogRefreshRunning => OnlineFirmwareModel.IsRefreshing;

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
    /// Observes connection state and starts child-owned discovery before enabling workflow tabs.
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
        discoveryInitialized = false;
        lifetime?.Dispose();
        lifetime = new CancellationTokenSource();
        SubscribePanels();
        LocalFirmwareModel.HasDevice = DevicesModel.HasDevice;
        LocalFirmwareModel.HasDetectedDfuDevice = DfuModel.HasDetectedDfuDevice;
        activeVehicle.Changed += OnActiveVehicleChanged;
        //SetBusy();
        SetMessages("Ready");
        ApplyMode();
        DevicesModel.DiscoveryOwnedByPage = DfuModel.DiscoveryOwnedByPage = true;
        await Task.WhenAll(LocalFirmwareModel.ActivateAsync(), ValidatedModel.ActivateAsync(), DevicesModel.ActivateAsync(), DfuModel.ActivateAsync());
        discoveryInitialized = true;
        UpdatePanelCapabilities();
    }

    /// <inheritdoc />
    public override Task DeactivateAsync()
    {
        return DeactivatePanelsAsync();
    }

    private async Task DeactivatePanelsAsync()
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
        DevicesModel.DiscoveryOwnedByPage = DfuModel.DiscoveryOwnedByPage = false;
        CanUseSerialFirmware = CanUseDfuFirmware = false;
        UnsubscribePanels();
        activeVehicle.Changed -= OnActiveVehicleChanged;
        var cleanup = Task.WhenAll(OnlineFirmwareModel.DeactivateAsync(), LocalFirmwareModel.DeactivateAsync(), ValidatedModel.DeactivateAsync(), DevicesModel.DeactivateAsync(), DfuModel.DeactivateAsync());
        OnlineFirmwareModel.Reset();
        LocalFirmwareModel.Reset();
        ValidatedModel.Reset();
        DevicesModel.Reset();
        DfuModel.Reset();
        var current = lifetime;
        lifetime = null;
        if (current is not null)
        {
            await current.CancelAsync();
            current.Dispose();
        }
        await cleanup;
    }

    /// <inheritdoc />
    public override void Dispose()
    {
        DeactivatePanelsAsync().SafeFireAndForget();
        disposed = true;
        base.Dispose();
    }

    partial void OnIsOperationInProgressChanged(bool value)
    {
        OnlineFirmwareModel.InstallationRunning = DevicesModel.InstallationRunning = DfuModel.InstallationRunning = value;
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
        if ((OnlineFirmwareModel.SelectedFirmware is null && LocalFirmwareModel.CustomPackage is null) || Interlocked.CompareExchange(ref operationRunning, 1, 0) != 0)
        {
            return;
        }

        using var ownedCancellation = BeginOperationCancellation(cancellationToken);
        try
        {
            SetOperation(true, FirmwareOperationState.Downloading);
            await ShowOperationDialogAsync("Installing firmware", ownedCancellation, deferUntilConfirmed: true);
            var target = OnlineFirmwareModel.SelectedFirmware?.Entry.Target;
            var prepared = ValidatedModel.PreparedFirmware is not null && ReferenceEquals(ValidatedModel.PreparedFirmware.ManifestEntry, OnlineFirmwareModel.SelectedFirmware?.Entry) ? ValidatedModel.PreparedFirmware : null;
            var request = new FirmwareInstallationRequest(
                new BootloaderEntryContext(new BootloaderDiscoveryRequest(
                        DevicesModel.SelectedDevice?.Descriptor,
                        target?.UsbIdentifiers,
                        target?.BootloaderNames),
                    DevicesModel.SelectedDevice?.Descriptor),
                prepared is null ? OnlineFirmwareModel.SelectedFirmware?.Entry.Artifact : null,
                LocalFirmwareModel.CustomPackage ?? prepared?.Package,
                LocalFirmwareModel.CustomPackage is not null ? FirmwareInstallationSource.LocalCustom : FirmwareInstallationSource.OfficialCatalogue,
                LocalFirmwareModel.CustomPackage is not null
                    ? new FirmwareCompatibilityPolicy(!LocalFirmwareModel.RequireExactBoardIdMatch)
                    : FirmwareCompatibilityPolicy.Strict,
                LocalFirmwareModel.CustomPackage is not null ? LocalFirmwareModel.CustomFirmwareName : null);

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
                var viewModel = domainFactory.Create<SubViews.DiagnosticsReportViewModel, string, string>(diagnosticsReport ?? "", message);
                dialogService.ShowOverlayDialog<SubViews.DiagnosticsReportView, SubViews.DiagnosticsReportViewModel>(viewModel, options);
            }
            else
            {
                var options = dialogService.CreateOptions("Firmware installation failed.", "Ok", null);
                var viewModel = domainFactory.Create<SubViews.DiagnosticsReportViewModel, string, string>(diagnosticsReport ?? "", message);
                dialogService.ShowOverlayDialog<SubViews.DiagnosticsReportView, SubViews.DiagnosticsReportViewModel>(viewModel, options);
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
            var viewModel = domainFactory.Create<SubViews.DiagnosticsReportViewModel, string, string>(message, exception.Message);
            dialogService.ShowOverlayDialog<SubViews.DiagnosticsReportView, SubViews.DiagnosticsReportViewModel>(viewModel, options);
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
        return ValidatedModel.IsFirmwareValidated != false && DevicesModel.SelectedDevice is not null && CanInstall && (OnlineFirmwareModel.SelectedFirmware is not null || LocalFirmwareModel.CustomPackage is not null) && !IsOperationInProgress && !ArePanelsRefreshing;
    }

    private bool CanStartDfuInstall()
    {
        return
            (UsesLocalDfuHex ? !string.IsNullOrWhiteSpace(DfuModel.LocalDfuFirmwarePath) && !string.IsNullOrWhiteSpace(DfuModel.LocalDfuPlatform) : OnlineFirmwareModel.SelectedFirmware is not null)
            &&
            OperatingSystem.IsWindows() && !activeVehicle.IsOnline
            && DfuModel.ToolStatus?.Availability == DfuToolAvailability.Available
            && DfuModel.SelectedDfuDevice?.Descriptor.DriverState == DfuDriverState.PresentReady && !IsOperationInProgress && !ArePanelsRefreshing;
    }

    [RelayCommand(CanExecute = nameof(CanStartDfuInstall), AllowConcurrentExecutions = false)]
    private async Task InstallDfuFirmwareAsync(CancellationToken cancellationToken)
    {
        var hasLocalHex = UsesLocalDfuHex;
        if (!CanStartDfuInstall() || (!hasLocalHex && OnlineFirmwareModel.SelectedFirmware is null) || DfuModel.SelectedDfuDevice is null ||
            Interlocked.CompareExchange(ref operationRunning, 1, 0) != 0)
        {
            return;
        }

        // The active source context determines the request; hidden local-file state must
        // never override a catalogue entry (or vice versa).
        var selectedFirmware = hasLocalHex ? null : OnlineFirmwareModel.SelectedFirmware;
        var selectedDfuDevice = DfuModel.SelectedDfuDevice;
        var platform = hasLocalHex ? DfuModel.LocalDfuPlatform?.Trim() : selectedFirmware?.Platform;
        var boardId = selectedFirmware?.BoardId;
        var localHexPath = hasLocalHex ? DfuModel.LocalDfuFirmwarePath : null;
        if (string.IsNullOrWhiteSpace(platform))
        {
            SetMessages("Enter the exact ArduPilot platform for the selected local HEX file.");
            Interlocked.Exchange(ref operationRunning, 0);
            return;
        }

        var requiredPhrase = $"FLASH {platform}";
        var options = dialogService.CreateOptions("Confirm initial ArduPilot installation", "Continue", null);
        var message = $"This replaces the current firmware and installs ArduPilot plus its bootloader for {platform}{(boardId is int id ? $" (board ID {id})" : string.Empty)}. Type exactly: {requiredPhrase}";
        string? phrase;
        try
        {
            phrase = await dialogService.PromptAsync(options, message, string.Empty, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            Interlocked.Exchange(ref operationRunning, 0);
            SetMessages("Initial DFU installation cancelled.");
            return;
        }
        catch (Exception exception)
        {
            Interlocked.Exchange(ref operationRunning, 0);
            SetMessages(exception);
            return;
        }
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
                    ManifestEntry: selectedFirmware?.Entry, LocalHexPath: localHexPath,
                    PreviousApplicationDevice: DfuModel.HasCorrelatedSource ? DfuModel.CorrelatedHandoff?.Source : null), progress, ownedCancellation.Token);

            await DfuModel.RefreshAfterInstallationAsync(CancellationToken.None);

            var diagnosticReport = BuildDfuDiagnosticReport(result, platform, boardId, selectedDfuDevice.Descriptor);
            SetMessages(result.State == DfuOperationState.Completed
                ? result.ApplicationRediscovered
                    ? "Initial ArduPilot installation completed and the application device was detected."
                    : "Programming and verification completed. Reconnect or reset the controller if ArduPilot does not appear."
                : result.Failure?.Message ?? $"STM32 DFU installation {result.State}.");

            options = dialogService.CreateOptions("Firmware installation completed.", "Ok", null);
            var viewModel = domainFactory.Create<SubViews.DiagnosticsReportViewModel, string, string>(diagnosticReport ?? "", "");
            dialogService.ShowOverlayDialog<SubViews.DiagnosticsReportView, SubViews.DiagnosticsReportViewModel>(viewModel, options);

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
            var viewModel = domainFactory.Create<SubViews.DiagnosticsReportViewModel, string, string>(message ?? "", exception.Message);
            dialogService.ShowOverlayDialog<DiagnosticsReportView, SubViews.DiagnosticsReportViewModel>(viewModel, options);

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
        return CanUpdateBootloader && !IsOperationInProgress && !ArePanelsRefreshing;
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
        ValidatedModel.IsFirmwareValidated = false;
        if (OnlineFirmwareModel.SelectedFirmware is null || IsOperationInProgress || ArePanelsRefreshing)
        {
            return;
        }

        using var ownedCancellation = BeginOperationCancellation(cancellationToken);
        try
        {
            SetOperation(true, FirmwareOperationState.Downloading);
            await ShowOperationDialogAsync("Downloading firmware", ownedCancellation);
            ValidatedModel.PreparedFirmware = await preparationService.PrepareAsync(new FirmwarePreparationRequest(OnlineFirmwareModel.SelectedFirmware.Entry), CreateProgress(), ownedCancellation.Token);
            SetMessages(ValidatedModel.PreparedFirmware.WasCacheHit ? "ValidatedPackageModel cached firmware package." : "Firmware downloaded and ValidatedModel.");
            ValidatedModel.IsFirmwareValidated = true;
            NotificationManager?.Show(StatusMessage ?? "");
        }
        catch (OperationCanceledException) when (ownedCancellation.IsCancellationRequested)
        {
            SetMessages("Firmware download and validation cancelled.", null);
        }
        catch (Exception exception)
        {
            Logger.LogWarning(exception, "Firmware preparation failed.");
            SetMessages(exception);
            NotificationManager?.Show(ErrorMessage ?? "");
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
            OnlineFirmwareModel.CancelRefresh();
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

        NotificationManager?.Show(StatusMessage ?? "");
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

            ApplyMode();
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
            SerialDevicePresent: DevicesModel.DetectedDevices.Count > 0,
            TargetAmbiguous: OnlineFirmwareModel.FirmwareChoices.Count > 0 && OnlineFirmwareModel.SelectedFirmware is null,
            PackageBoardMismatch: packageBoardMismatch,
            Channel: OnlineFirmwareModel.SelectedChannel,
            CustomPackageSelected: LocalFirmwareModel.CustomPackage is not null));
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
        OnlineFirmwareModel.SelectionChanged += OnCatalogueSelection;
        OnlineFirmwareModel.ChannelChanged += OnCatalogueChannel;
        OnlineFirmwareModel.FiltersChanged += OnCatalogueFilters;
        DevicesModel.SelectionChanged += OnDeviceSelection;
        LocalFirmwareModel.PackageChanged += OnCustomPackage;
        LocalFirmwareModel.OperationRequested += OnPanelOperation;
        DfuModel.SelectionChanged += OnDfuSelection;
        DfuModel.LocalFirmwareChanged += OnDfuFirmware;
        DfuModel.PlatformChanged += OnDfuPlatform;
        OnlineFirmwareModel.RefreshStateChanged += OnPanelRefreshState;
        DevicesModel.RefreshStateChanged += OnPanelRefreshState;
        DfuModel.RefreshStateChanged += OnPanelRefreshState;
        DevicesModel.OperationRequested += OnPanelOperation;

        DfuModel.OperationRequested += OnPanelOperation;
        ValidatedModel.OperationRequested += OnPanelOperation;
        landing.OperationRequested += OnPanelOperation;
        SelectedFirmwareModel.OperationRequested += OnPanelOperation;
    }

    private void UnsubscribePanels()
    {
        OnlineFirmwareModel.SelectionChanged -= OnCatalogueSelection;
        OnlineFirmwareModel.ChannelChanged -= OnCatalogueChannel;
        OnlineFirmwareModel.FiltersChanged -= OnCatalogueFilters;
        DevicesModel.SelectionChanged -= OnDeviceSelection;
        LocalFirmwareModel.PackageChanged -= OnCustomPackage;
        LocalFirmwareModel.OperationRequested -= OnPanelOperation;
        DfuModel.SelectionChanged -= OnDfuSelection;
        DfuModel.LocalFirmwareChanged -= OnDfuFirmware;
        DfuModel.PlatformChanged -= OnDfuPlatform;
        OnlineFirmwareModel.RefreshStateChanged -= OnPanelRefreshState;
        DevicesModel.RefreshStateChanged -= OnPanelRefreshState;
        DfuModel.RefreshStateChanged -= OnPanelRefreshState;
        DevicesModel.OperationRequested -= OnPanelOperation;
        DfuModel.OperationRequested -= OnPanelOperation;
        ValidatedModel.OperationRequested -= OnPanelOperation;
        landing.OperationRequested -= OnPanelOperation;
        SelectedFirmwareModel.OperationRequested -= OnPanelOperation;
    }

    private bool ArePanelsRefreshing => OnlineFirmwareModel.IsRefreshing || DevicesModel.IsRefreshing || DfuModel.IsRefreshing;

    private void OnPanelRefreshState(bool value)
    {
        OnPropertyChanged(nameof(IsCatalogRefreshRunning));
        OnPropertyChanged(nameof(CanRequestCancellation));
        CancelCommand.NotifyCanExecuteChanged();
        UpdatePanelCapabilities();
        UpdateContextHelp();
    }

    private void OnPanelOperation(FirmwarePanelRequest request)
    {
        request.Completion = request.Action switch
        {
            FirmwarePanelAction.Download => DownloadAndValidateAsync(request.CancellationToken),
            FirmwarePanelAction.PrepareDfu => PrepareDfuArtifactAsync(request.CancellationToken),
            FirmwarePanelAction.RebootToDfu => RebootToDfuAsync(request.CancellationToken),
            FirmwarePanelAction.Install when CanStartInstall() => InstallAsync(request.CancellationToken),
            FirmwarePanelAction.InstallDfu when CanStartDfuInstall() => InstallDfuFirmwareAsync(request.CancellationToken),
            _ => Task.CompletedTask
        };
    }
    private void OnCatalogueSelection(FirmwareCatalogItemViewModel? value)
    {
        DfuModel.PreparedArtifact = null;
        if (value is not null)
        {
            LocalFirmwareModel.CustomPackage = null;
        }
        UpdatePanelCapabilities();
        UpdateContextHelp();
    }
    private void OnCatalogueChannel(FirmwareReleaseChannel value)
    {
        UpdateContextHelp();
    }
    private void OnCatalogueFilters(bool value)
    {
        ValidatedModel.IsFirmwareValidated = false;
        UpdatePanelCapabilities();
    }
    private void OnDeviceSelection(FirmwareDeviceItemViewModel? value)
    {
        LocalFirmwareModel.HasDevice = value is not null;
        UpdatePanelCapabilities();
    }
    private void OnCustomPackage(ApjFirmwarePackage? value)
    {
        ValidatedModel.PreparedFirmware = null;
        if (value is not null)
        {
            OnlineFirmwareModel.ClearSelection();
            DfuModel.LocalDfuFirmwarePath = null;
            DfuModel.LocalDfuFirmwareName = null;
        }
        ValidatedModel.IsFirmwareValidated = value is not null;
        UpdatePanelCapabilities();
        UpdateContextHelp();
    }
    private void OnDfuSelection(DfuDeviceItemViewModel? value)
    {
        if (DfuModel.CorrelatedHandoff is not null && !DfuModel.HasCorrelatedSource)
        {
            OnlineFirmwareModel.SetReviewedDfuTarget(null);
        }
        LocalFirmwareModel.HasDetectedDfuDevice = value is not null;
        OnPropertyChanged(nameof(HasDetectedDfuDevice));
        UpdatePanelCapabilities();
    }
    private void OnDfuFirmware(string? value)
    {
        DfuModel.PreparedArtifact = null;
        if (value is not null)
        {
            LocalFirmwareModel.CustomPackage = null;
            OnlineFirmwareModel.ClearSelection();
        }
        UpdatePanelCapabilities();
    }
    private void OnDfuPlatform(string? value)
    {
        DfuModel.PreparedArtifact = null;
        UpdatePanelCapabilities();
    }

    private void UpdatePanelCapabilities()
    {
        if (!active)
        {
            return;
        }
        var available = discoveryInitialized && IsDisconnectedMode && !activeVehicle.IsOnline && !IsOperationInProgress;
        CanUseDfuFirmware = available && DfuModel.DfuDevices.Count > 0;
        CanUseSerialFirmware = available && DfuModel.DfuDevices.Count == 0
            && DevicesModel.Descriptors.Any(device => !string.IsNullOrWhiteSpace(device.PortName));
        DevicesModel.CanInstall = CanStartInstall();
        ValidatedModel.CanInstall = CanStartInstall();
        DfuModel.CanInstallDfu = CanStartDfuInstall();
        InstallCommand.NotifyCanExecuteChanged();
        InstallDfuFirmwareCommand.NotifyCanExecuteChanged();
    }
    /// <summary>Gets whether the DFU tab has a selected device.</summary>
    public bool HasDetectedDfuDevice => DfuModel.HasDetectedDfuDevice;

    private bool discoveryInitialized;

    /// <summary>Gets whether a serial controller is available for catalogue or custom firmware.</summary>
    [ObservableProperty]
    public partial bool CanUseSerialFirmware
    {
        get; private set;
    }

    /// <summary>Gets whether a detected DFU controller can use the STM32 workflow.</summary>
    [ObservableProperty]
    public partial bool CanUseDfuFirmware
    {
        get; private set;
    }

    /// <summary>Refreshes both device types even when their workflow tabs are disabled.</summary>
    [RelayCommand]
    private async Task RefreshDevicesAsync(CancellationToken cancellationToken)
    {
        await Task.WhenAll(DevicesModel.RefreshAsync(cancellationToken), DfuModel.RefreshAsync(cancellationToken));
        UpdatePanelCapabilities();
    }
}
