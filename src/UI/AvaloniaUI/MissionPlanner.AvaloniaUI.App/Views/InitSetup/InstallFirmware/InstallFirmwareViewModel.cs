using System.Diagnostics;
using AsyncAwaitBestPractices;
using Avalonia.Layout;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Mapsui.Utilities;
using Microsoft.Extensions.Logging;
using MissionPlanner.AvaloniaUI.App.Presentation;
using MissionPlanner.AvaloniaUI.App.Utilities;
using MissionPlanner.AvaloniaUI.App.Utilities.Dialogs;
using MissionPlanner.Core.Vehicles.Abstractions;
using MissionPlanner.Firmware;
using MissionPlanner.Firmware.Catalog;
using MissionPlanner.Firmware.Compatibility;
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

namespace MissionPlanner.AvaloniaUI.App.Views.InitSetup.InstallFirmware;

/// <summary>Drives connected and disconnected firmware installation experiences.</summary>
public sealed partial class InstallFirmwareViewModel : ViewModelBase
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
    private readonly IUserConfirmationService confirmation;
    private readonly IDialogService dialogService;
    private readonly IExternalLinkLauncher externalLinkLauncher;
    private readonly IDeviceManagerLauncher deviceManagerLauncher;
    private readonly ITextClipboardService clipboard;
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
    /// <param name="packageReader"></param>
    /// <param name="filePicker"></param>
    /// <param name="activeVehicle"></param>
    /// <param name="confirmation"></param>
    /// <param name="supportLinkProvider"></param>
    /// <param name="externalLinkLauncher"></param>
    /// <param name="deviceManagerLauncher"></param>
    /// <param name="clipboard">Copies firmware URLs and diagnostic reports.</param>
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
        IDeviceManagerLauncher deviceManagerLauncher, ITextClipboardService clipboard, IDialogService dialogService,
        ILogger<InstallFirmwareViewModel> logger) : base(logger)
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
        this.clipboard = clipboard;
        this.dialogService = dialogService;
    }

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
    /// 
    /// </summary>
    [ObservableProperty]
    public partial HorizontalAlignment ContextWidth
    {
        get;
        private set;
    } = HorizontalAlignment.Center;

    partial void OnIsVehicleConnectedChanged(bool oldValue, bool newValue)
    {
        ContextWidth = HorizontalAlignment.Center;// newValue ? HorizontalAlignment.Center : HorizontalAlignment.Stretch;
    }

    [ObservableProperty]
    public partial bool IsVehicleConnected
    {
        get;
        set;
    }

    ///
    /// <summary>
    /// Gets the distinct firmware versions available in the catalogue.
    /// </summary>
    [ObservableProperty]
    public partial ObservableRangeCollection<string> Versions { get; private set; } = [];

    /// <summary>
    /// Gets or sets the selected firmware version for filtering the catalogue.
    /// </summary>
    [ObservableProperty]
    public partial string? SelectedVersion
    {
        get;
        set;
    }

    /// <summary>
    ///  Gets the distinct FrameTypes available in the catalogue.
    /// </summary>
    [ObservableProperty]
    public partial ObservableRangeCollection<string> FrameTypes
    {
        get;
        private set;
    } = [];

    /// <summary>
    /// Gets or sets the selected FrameType for filtering the catalogue.
    /// </summary>
    [ObservableProperty]
    public partial string? SelectedFrameType
    {
        get;
        set;
    }


    /// <summary>
    ///  Gets the distinct Manufacturer available in the catalogue.
    /// </summary>
    [ObservableProperty]
    public partial ObservableRangeCollection<string> Manufacturers
    {
        get;
        private set;
    } = [];

    /// <summary>
    ///  Gets or sets the selected Manufacturer for filtering the catalogue.
    /// </summary>
    [ObservableProperty]
    public partial string? SelectedManufacturer
    {
        get;
        set;
    }


    /// <summary>Gets discovered serial devices.</summary>
    [ObservableProperty]
    public partial IReadOnlyList<FirmwareDeviceItemViewModel> DetectedDevices
    {
        get;
        private set;
    } = [];

    /// <summary>Gets release channels.</summary>
    public IReadOnlyList<FirmwareReleaseChannel> Channels { get; } = [FirmwareReleaseChannel.Stable, FirmwareReleaseChannel.Beta, FirmwareReleaseChannel.Latest];

    /// <summary>Gets operation progress.</summary>
    public FirmwareProgressViewModel OperationProgress { get; } = new();

    /// <summary>Gets concise help that remains available offline.</summary>
    public IReadOnlyList<FirmwareSupportSection> SupportSections { get; } = FirmwareSupportContent.Sections;

    /// <summary>Gets curated official and fallback support destinations.</summary>
    public IReadOnlyList<FirmwareSupportLink> SupportLinks
    {
        get;
    }

    [ObservableProperty]
    public partial FirmwareReleaseChannel SelectedChannel { get; set; } = FirmwareReleaseChannel.Stable;

    [ObservableProperty]
    public partial FirmwareCatalogItemViewModel? SelectedFirmware
    {
        get;
        set;
    }

    [ObservableProperty]
    public partial FirmwareDeviceItemViewModel? SelectedDevice
    {
        get;
        set;
    }

    [ObservableProperty]
    public partial IReadOnlyList<DfuDeviceItemViewModel> DfuDevices
    {
        get;
        private set;
    } = [];

    [ObservableProperty]
    public partial DfuDeviceItemViewModel? SelectedDfuDevice
    {
        get;
        set;
    }

    [ObservableProperty]
    public partial string DfuStatus { get; private set; } = "Enter STM32 DFU mode, then refresh the catalogue.";

    [ObservableProperty]
    public partial FirmwarePreparationResult? PreparedFirmware
    {
        get; private set;
    }

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

    /// <summary>Gets whether a local combined application-and-bootloader HEX file is selected.</summary>
    public bool HasLocalDfuFirmware => !string.IsNullOrWhiteSpace(LocalDfuFirmwarePath);

    /// <summary>Gets whether a serial flight-controller device is selected.</summary>
    public bool HasDevice => SelectedDevice is not null;

    /// <summary>Gets whether an STM32 DFU device is selected.</summary>
    public bool HasDfuBootLoader => SelectedDfuDevice is not null;

    /// <summary>Gets whether a firmware release from the catalogue is selected.</summary>
    public bool HasSelectedFirmware => SelectedFirmware is not null;

    [ObservableProperty]
    public partial ApjFirmwarePackage? CustomPackage
    {
        get;
        private set;
    }

    [ObservableProperty]
    public partial string? CustomFirmwareName
    {
        get;
        private set;
    }

    [ObservableProperty]
    public partial string? CustomFirmwareDescription
    {
        get;
        private set;
    }

    [ObservableProperty]
    public partial string? CustomFirmwarePlatform
    {
        get;
        private set;
    }

    [ObservableProperty]
    public partial string? CustomFirmwareBuild
    {
        get;
        private set;
    }

    [ObservableProperty]
    public partial int CustomFirmwareBoardId
    {
        get;
        private set;
    }

    [ObservableProperty]
    public partial long CustomFirmwareImageSize
    {
        get;
        private set;
    }

    [ObservableProperty]
    public partial bool RequireExactBoardIdMatch
    {
        get;
        set;
    } = true;

    [ObservableProperty]
    public partial string? LocalDfuFirmwarePath
    {
        get;
        private set;
    }

    [ObservableProperty]
    public partial string? LocalDfuFirmwareName
    {
        get;
        private set;
    }

    [ObservableProperty]
    public partial string? LocalDfuPlatform
    {
        get;
        set;
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


    [ObservableProperty]
    public partial string DeviceStatus
    {
        get;
        private set;
    } = "No flight controller detected";

    [ObservableProperty]
    public partial string? LastDiagnosticReport
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
        lifetime?.Dispose();
        lifetime = new CancellationTokenSource();
        SetBusy();
        activeVehicle.Changed += OnActiveVehicleChanged;
        SetMessages("Ready");
        OperationProgress.Stage = "Ready";
        OperationProgress.Progress = 0;
        OperationProgress.HasStage = false;
        OperationProgress.IsPowerCritical = false;
        OperationProgress.TechnicalDetail = null;
        LastDiagnosticReport = null;
        var visibleMode = ApplyMode();
        if (visibleMode == FirmwarePageMode.Disconnected)
        {
            IsVehicleConnected = false;
            await RefreshSafelyAsync(false, lifetime.Token);
        }
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
        activeVehicle.Changed -= OnActiveVehicleChanged;
        CancelRefresh();
        IsVehicleConnected = false;
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
        finally { }

        {
            ResetBusy();
        }
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

    partial void OnPreparedFirmwareChanged(FirmwarePreparationResult? value)
    {
        OnPropertyChanged(nameof(HasPreparedFirmware));
    }

    partial void OnCustomPackageChanged(ApjFirmwarePackage? value)
    {
        if (value is null)
        {
            RequireExactBoardIdMatch = true;
        }

        OnPropertyChanged(nameof(HasCustomFirmware));
        InstallCommand.NotifyCanExecuteChanged();
    }

    partial void OnLastDiagnosticReportChanged(string? value)
    {
        OnPropertyChanged(nameof(HasDiagnosticReport));
    }

    partial void OnLocalDfuFirmwarePathChanged(string? value)
    {
        OnPropertyChanged(nameof(HasLocalDfuFirmware));
        InstallDfuFirmwareCommand.NotifyCanExecuteChanged();
    }

    partial void OnIsOperationInProgressChanged(bool value)
    {
        OnPropertyChanged(nameof(CanNavigateAway));
        OnPropertyChanged(nameof(CanRequestCancellation));
        InstallCommand.NotifyCanExecuteChanged();
        InstallDfuFirmwareCommand.NotifyCanExecuteChanged();
        UpdateBootloaderCommand.NotifyCanExecuteChanged();
        CancelCommand.NotifyCanExecuteChanged();
    }

    partial void OnCanInstallChanged(bool value)
    {
        InstallCommand.NotifyCanExecuteChanged();
    }

    partial void OnCanUpdateBootloaderChanged(bool value)
    {
        UpdateBootloaderCommand.NotifyCanExecuteChanged();
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
        CustomPackage = null;
    }


    [RelayCommand(CanExecute = nameof(HasDevice))]
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
            if (!extension.Equals(".apj", StringComparison.OrdinalIgnoreCase) && !extension.Equals(".px4", StringComparison.OrdinalIgnoreCase))
            {
                throw new NotSupportedException("Only .apj and .px4 application packages are supported here. Use the separate DFU/legacy workflow for *_with_bl.hex.");
            }

            await using var stream = await file.OpenReadAsync(cancellationToken);
            var package = await packageReader.ReadAsync(stream, cancellationToken);
            RequireExactBoardIdMatch = true;
            CustomPackage = package;
            LocalDfuFirmwarePath = null;
            LocalDfuFirmwareName = null;
            CustomFirmwareName = file.FileName;
            CustomFirmwareDescription = package.Description ?? "Custom ArduPilot firmware";
            CustomFirmwarePlatform = package.Summary ?? "Platform declared by board ID";
            CustomFirmwareBuild = package.Version ?? package.GitIdentity ?? "Unknown build";
            CustomFirmwareBoardId = package.BoardId;
            CustomFirmwareImageSize = package.Image.Length;
            SelectedFirmware = null;
            selectedFirmwareTarget = null;
            SetMessages("Local firmware parsed and validated. Verify its board ID, then install it using the custom firmware panel.");
            UpdateContextHelp();
        }
        catch (Exception exception)
        {
            Logger.LogWarning(exception, "Custom firmware selection failed.");
            CustomPackage = null;
            SetMessages(exception);
        }
    }

    [RelayCommand(CanExecute = nameof(HasDfuBootLoader))]
    private async Task LoadCustomBlWithFirmwareAsync(CancellationToken cancellationToken)
    {
        try
        {
            var file = await filePicker.PickAsync(cancellationToken);
            if (file is null)
            {
                return;
            }

            var extension = Path.GetExtension(file.FileName);

            if (!extension.Equals(".hex", StringComparison.OrdinalIgnoreCase))
            {
                throw new NotSupportedException("Only .hex firmware packages are supported by the modern bootloader workflow.");
            }

            if (!file.FileName.EndsWith("_with_bl.hex", StringComparison.OrdinalIgnoreCase))
            {
                throw new NotSupportedException("For STM32 DFU installation, select a combined application-and-bootloader file named *_with_bl.hex.");
            }

            if (string.IsNullOrWhiteSpace(file.LocalPath) || !File.Exists(file.LocalPath))
            {
                throw new NotSupportedException("The selected HEX file does not expose a local path required by STM32CubeProgrammer.");
            }

            LocalDfuFirmwarePath = Path.GetFullPath(file.LocalPath);
            LocalDfuFirmwareName = file.FileName;
            CustomPackage = null;
            SelectedFirmware = null;
            selectedFirmwareTarget = null;
            SetMessages("Local *_with_bl.hex selected. Enter its exact ArduPilot platform and select the detected STM32 DFU device.");
        }
        catch (Exception exception)
        {
            Logger.LogWarning(exception, "Custom firmware selection failed.");
            CustomPackage = null;
            SetMessages(exception);
        }
    }

    [RelayCommand]
    private void ClearCustomFirmware()
    {
        CustomPackage = null;
        CustomFirmwareName = null;
        CustomFirmwareDescription = null;
        CustomFirmwarePlatform = null;
        CustomFirmwareBuild = null;
        CustomFirmwareBoardId = 0;
        CustomFirmwareImageSize = 0;
        RequireExactBoardIdMatch = true;
        SetMessages("Local firmware selection cleared.");
        UpdateContextHelp();
    }

    [RelayCommand]
    private void ClearLocalDfuFirmware()
    {
        LocalDfuFirmwarePath = null;
        LocalDfuFirmwareName = null;
        LocalDfuPlatform = null;
        SetMessages("Local STM32 DFU firmware selection cleared.");
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
                CustomPackage ?? prepared?.Package,
                CustomPackage is not null ? FirmwareInstallationSource.LocalCustom : FirmwareInstallationSource.OfficialCatalogue,
                CustomPackage is not null
                    ? new FirmwareCompatibilityPolicy(!RequireExactBoardIdMatch)
                    : FirmwareCompatibilityPolicy.Strict,
                CustomPackage is not null ? CustomFirmwareName : null);

            var progress = CreateProgress();
            var result = await installationService.InstallAsync(request, progress, ownedCancellation.Token);
            LastDiagnosticReport = result.DiagnosticReport?.CreateReport();


            SetMessages(result.State == FirmwareOperationState.Completed
                ? result.ApplicationDevice is null
                    ? "Firmware installation completed; reconnect was not detected. Reconnect the flight controller manually."
                    : $"Firmware installation completed. ArduPilot returned on {result.ApplicationDevice.PortName}; reconnect is available."
                : result.Failure?.TechnicalDetail is { Length: > 0 } detail
                    ? $"Firmware installation {result.State}: {detail}"
                    : $"Firmware installation {result.State}");
        }
        catch (OperationCanceledException) when (ownedCancellation.IsCancellationRequested)
        {
            SetMessages("Firmware installation cancelled.");
        }
        catch (Exception exception)
        {
            Debug.Print("Firmware installation failed.\n{0}", exception.ToString());
            Logger.LogError(exception, "Firmware installation failed.");
            SetMessages(exception);
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
        return SelectedDevice is not null && CanInstall && (SelectedFirmware is not null || CustomPackage is not null) && !IsOperationInProgress;
    }

    private bool CanStartDfuInstall()
    {
        return
            (!string.IsNullOrWhiteSpace(LocalDfuFirmwarePath) ? !string.IsNullOrWhiteSpace(LocalDfuPlatform) : SelectedFirmware is not null)
            &&
            SelectedDfuDevice?.Descriptor.DriverState == DfuDriverState.PresentReady && !IsOperationInProgress;
    }


    [RelayCommand(CanExecute = nameof(CanStartDfuInstall), AllowConcurrentExecutions = false)]
    private async Task InstallDfuFirmwareAsync(CancellationToken cancellationToken)
    {
        var hasLocalHex = !string.IsNullOrWhiteSpace(LocalDfuFirmwarePath);
        if ((!hasLocalHex && SelectedFirmware is null) || SelectedDfuDevice is null ||
            Interlocked.CompareExchange(ref operationRunning, 1, 0) != 0)
        {
            return;
        }

        // An explicitly loaded local image must always take precedence over a catalogue
        // row that may have been restored or automatically selected during refresh.
        var selectedFirmware = hasLocalHex ? null : SelectedFirmware;
        var selectedDfuDevice = SelectedDfuDevice;
        var platform = hasLocalHex ? LocalDfuPlatform?.Trim() : selectedFirmware?.Platform;
        var boardId = selectedFirmware?.BoardId;
        var localHexPath = hasLocalHex ? LocalDfuFirmwarePath : null;
        if (string.IsNullOrWhiteSpace(platform))
        {
            SetMessages("Enter the exact ArduPilot platform for the selected local HEX file.");
            Interlocked.Exchange(ref operationRunning, 0);
            return;
        }

        var requiredPhrase = $"FLASH {platform}";
        var options = AvaloniaDialogService.CreateDialogOptions("Confirm initial ArduPilot installation", "Continue", null);
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
            await ShowOperationDialogAsync("Installing ArduPilot through STM32 DFU", ownedCancellation);
            var progress = new Progress<DfuProgress>(value => Dispatcher.Dispatch(() =>
            {
                ProgressMessage = DfuStageText(value);
                SetMessages(ProgressMessage);
            }));
            var result = await dfuInstallationService.InstallAsync(
                new DfuInstallationRequest(platform, boardId, selectedDfuDevice.Descriptor, ConfirmationPhrase: requiredPhrase,
                    ManifestEntry: selectedFirmware?.Entry, LocalHexPath: localHexPath), progress, ownedCancellation.Token);

            await RefreshDfuDevicesAsync(CancellationToken.None);

            LastDiagnosticReport = BuildDfuDiagnosticReport(result, platform, boardId, selectedDfuDevice.Descriptor);
            SetMessages(result.State == DfuOperationState.Completed
                ? result.ApplicationRediscovered
                    ? "Initial ArduPilot installation completed and the application device was detected."
                    : "Programming and verification completed. Reconnect or reset the controller if ArduPilot does not appear."
                : result.Failure?.Message ?? $"STM32 DFU installation {result.State}.");
        }
        catch (OperationCanceledException) when (ownedCancellation.IsCancellationRequested)
        {
            SetMessages("Initial DFU installation cancelled.");
        }
        catch (Exception exception)
        {
            Logger.LogError(exception, "Initial STM32 DFU installation failed.");
            SetMessages(exception);
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
            : clipboard.SetTextAsync(LastDiagnosticReport);
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

        Debug.Print("InstallFirmware RefreshAsync");

        var (version, refreshToken) = BeginRefresh(cancellationToken);
        try
        {
            await DispatchAsync(() =>
            {
                IsCatalogRefreshRunning = true;
                SetMessages("Loading firmware catalogue…");
                NotificationManager?.Show(StatusMessage!);
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
            Debug.Print("InstallFirmware RefreshAsync Completed task 1 & 2");


            var entries = catalog.Entries.Where(entry =>
                entry.Target.VehicleType != FirmwareVehicleType.Unknown
                &&
                entry.Artifact.Format is FirmwareImageFormat.Apj or FirmwareImageFormat.Px4).ToArray();

            var deviceItems = await Task.Run(() => CreateDeviceItems(entries, devices), refreshToken).ConfigureAwait(false);

            Debug.Print($"InstallFirmware RefreshAsync Completed task 3 with entries count: {entries.Length}");


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
                SetMessages(catalog.IsStale ? "Showing cached firmware catalogue" : $"{FirmwareChoices.Count} vehicle firmware choices available");
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

        // Replace the bound collection rather than issuing a range/reset notification.
        // Avalonia's DataGrid did not reliably refresh its rows when the existing
        // ObservableRangeCollection instance was cleared and repopulated.
        FilteredFirmwareChoices = new ObservableRangeCollection<FirmwareCatalogItemViewModel>(choices);
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

        FirmwareChoices.ReplaceRange(choices);

        var versions = choices
            .Select(x => x.FirmwareVersion)
            .Distinct()
            .OrderByDescending(v => v.SemanticVersion ?? new System.Version(0, 0))
            .ThenByDescending(v => v.Value, StringComparer.OrdinalIgnoreCase)
            .Select(x => x.ToString())
            .ToList();

        Versions.ReplaceRange(versions);

        //FirmwareManifestEntry -> FirmwareBoardTarget Target  -> FirmwareVehicleType VehicleType
        var frameTypes = choices
            .Select(x => x.VehicleType)
            .Distinct()
            .Order()
            .ToList();

        FrameTypes.ReplaceRange(frameTypes);

        var manufacturers = choices
            .Select(x => x.Manufacturer)
            .Distinct()
            .Order()
            .ToList();

        Manufacturers.Clear();
        Manufacturers.AddRange(manufacturers);

        // Keep the initial catalogue population on the same path as subsequent
        // filter changes. FilterData also updates HasFirmwareChoices, which controls
        // whether the Avalonia DataGrid is present in the visual tree.
        FilterData(SelectedVersion, SelectedFrameType, SelectedManufacturer);

        Debug.Print($"InstallFirmware ApplyTargetQuery with FirmwareChoices count: {FirmwareChoices.Count}");

        var retained = previousEntry is null ? null : FirmwareChoices.FirstOrDefault(item => SameEntry(item.Entry, previousEntry));
        var automatic = FirmwareTargetSelector.UnambiguousHighConfidence(recommendations);
        SelectedFirmware = retained ?? (automatic is null ? null : FirmwareChoices.Single(item => ReferenceEquals(item.Entry, automatic.Entry)));
    }

    private static IReadOnlyList<FirmwareDeviceItemViewModel> CreateDeviceItems(IReadOnlyList<FirmwareManifestEntry> entries, IReadOnlyList<SerialDeviceDescriptor> devices)
    {
        Debug.Print("InstallFirmware CreateDeviceItems");

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

        Debug.Print($"InstallFirmware CreateDeviceItems found {deviceItems.Length} items");
        return deviceItems;
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

        //{
        //    completion.SetException(new InvalidOperationException("Unable to dispatch firmware catalogue update."));
        //}
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
            SetMessages(PreparedFirmware.WasCacheHit ? "Validated cached firmware package." : "Firmware downloaded and validated.");
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

    [RelayCommand]
    private Task CopyDownloadUrlAsync()
    {
        return SelectedFirmware is null
            ? Task.CompletedTask
            : clipboard.SetTextAsync(SelectedFirmware.Entry.Artifact.DownloadUri.AbsoluteUri);
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

    private void OnActiveVehicleChanged(Core.Vehicles.ActiveVehicleChangedEventArgs e)
    {
        IsVehicleConnected = !e.Current.IsOnline;
        if (e.Current.IsOnline)
        {
            // The disconnected catalogue/device scan is no longer relevant once a vehicle
            // becomes active. Cancel it before queuing UI work for the connected mode.
            CancelRefresh();
        }
        if (!active)
        {
            return;
        }
        var visibleMode = ApplyMode();
        if (visibleMode == FirmwarePageMode.Disconnected && lifetime is { } currentLifetime)
        {
            RefreshSafelyAsync(false, currentLifetime.Token).SafeFireAndForget();
            return;
        }

        ResetBusy();
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
        });

        return visibleMode;
    }

    private void UpdateProgress(FirmwareProgress progress)
    {
        Dispatcher.Dispatch(() =>
            {
                CurrentOperationState = progress.State;
                OperationProgress.Stage = StageText(progress);
                OperationProgress.Progress = (progress.Percentage ?? 0) / 100d;
                OperationProgress.HasStage = progress.Percentage.HasValue;
                OperationProgress.IsPowerCritical = progress.State is FirmwareOperationState.Erasing or FirmwareOperationState.Programming or FirmwareOperationState.Verifying;
                OperationProgress.TechnicalDetail = progress.TechnicalDetail;
                SetMessages(OperationProgress.Stage);
            });
    }

    partial void OnSelectedFirmwareChanged(FirmwareCatalogItemViewModel? value)
    {
        if (value is not null)
        {
            selectedFirmwareTarget = value.Entry;
        }

        OnPropertyChanged(nameof(HasSelectedFirmware));
        InstallCommand.NotifyCanExecuteChanged();
        InstallDfuFirmwareCommand.NotifyCanExecuteChanged();
    }

    partial void OnSelectedDeviceChanged(FirmwareDeviceItemViewModel? value)
    {
        OnPropertyChanged(nameof(HasDevice));
        LoadCustomFirmwareCommand.NotifyCanExecuteChanged();
        InstallCommand.NotifyCanExecuteChanged();
    }


    partial void OnSelectedDfuDeviceChanged(DfuDeviceItemViewModel? value)
    {
        OnPropertyChanged(nameof(HasDfuBootLoader));
        LoadCustomBlWithFirmwareCommand.NotifyCanExecuteChanged();
        InstallDfuFirmwareCommand.NotifyCanExecuteChanged();
    }

    partial void OnLocalDfuPlatformChanged(string? value)
    {
        InstallDfuFirmwareCommand.NotifyCanExecuteChanged();
    }

    private IProgress<FirmwareProgress> CreateProgress()
    {
        return new Progress<FirmwareProgress>(progress => Dispatcher.Dispatch(() =>
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
            () => ProgressMessage,
            new DialogOptions()
            {
                Title = ProgressMessage
            },
            cancellationToken: cancellation.Token);
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
            Logger.LogWarning(exception, "Unable to refresh STM32 DFU devices after installation.");
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
}

