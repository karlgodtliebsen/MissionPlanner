using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MissionPlanner.App.Views.InitSetup.InstallFirmware.SubViews;
using MissionPlanner.Firmware.Dfu;
using MissionPlanner.Firmware.Entry;
using MissionPlanner.Firmware.Installation;
using MissionPlanner.Firmware.Model;
using MissionPlanner.Firmware.Preparation;
using MissionPlanner.Firmware.Workflow;

namespace MissionPlanner.App.Views.InitSetup.InstallFirmware;

public sealed partial class InstallFirmwareViewModel
{
    private string? dfuTargetConfirmation;

    /// <summary>Reviews exact platform intent separately from artifact validation.</summary>
    [RelayCommand]
    private async Task ReviewDfuTargetAsync(CancellationToken cancellationToken)
    {
        if (IsOperationInProgress || !SelectedArtifact.ArtifactValid
            || CurrentPlan.Transport != BootloaderEntryTarget.Stm32RomDfu || string.IsNullOrWhiteSpace(SelectedArtifact.Platform))
        {
            return;
        }
        var artifact = DfuModel.PreparedArtifact;
        var target = DfuModel.SelectedDfuDevice;
        var platform = SelectedArtifact.Platform;
        var expected = $"FLASH {platform}";
        var options = dialogService.CreateOptions("Confirm exact DFU target", "Confirm", "Cancel");
        var message1 = "STM32 ROM DFU does not identify the flight-controller board. ";
        var message2 = $"Verify that {platform} is the exact platform.";

        var viewModel = domainFactory.Create<ConfirmDfuTargetViewModel, string, string, string>(expected, message1, message2);
        var result = await dialogService.ShowOverlayDialogAsync<ConfirmDfuTargetView, ConfirmDfuTargetViewModel>(viewModel, options, cancellationToken: cancellationToken);

        var phrase = result.TargetMessage;


        if (ReferenceEquals(artifact, DfuModel.PreparedArtifact) && ReferenceEquals(target, DfuModel.SelectedDfuDevice)
            && string.Equals(phrase?.Trim(), expected, StringComparison.Ordinal))
        {
            dfuTargetConfirmation = expected;
        }
        UpdatePanelCapabilities();
    }

    /// <summary>Gets whether online provenance is available for the current artifact.</summary>
    public bool HasOnlineArtifact => SelectedArtifact.OnlineUrl is not null;

    /// <summary>Gets whether local provenance is available for the current artifact.</summary>
    public bool HasLocalArtifact => SelectedArtifact.LocalFile is not null;

    /// <summary>Gets whether online firmware, a local APJ, or a local HEX file is selected.</summary>
    [ObservableProperty]
    public partial bool IsFirmwareSelected
    {
        get; private set;
    }

    /// <summary>Gets whether a serial controller or STM32 DFU endpoint is selected.</summary>
    [ObservableProperty]
    public partial bool HasPhysicalController
    {
        get; private set;
    }

    /// <summary>Gets whether controller choices or a selected controller should be shown.</summary>
    [ObservableProperty]
    public partial bool ShowPhysicalController
    {
        get; private set;
    }

    /// <summary>Gets whether firmware and controller selections provide a validation context.</summary>
    [ObservableProperty]
    public partial bool ShowValidationAndCompatibility
    {
        get; private set;
    }

    /// <summary>Gets whether a prepared package and target evidence are available for checking; this does not mean they match.</summary>
    [ObservableProperty]
    public partial bool CanValidateCompatibility
    {
        get; private set;
    }


    [RelayCommand(CanExecute = nameof(HasOnlineArtifact))]
    private Task CopySelectedUrlAsync()
    {
        return HasOnlineArtifact && clipboard is not null
            ? clipboard.SetTextAsync(SelectedArtifact.OnlineUrl!.AbsoluteUri) : Task.CompletedTask;
    }

    private void DfuModel_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(DfuModel.LocalDfuPlatform))
        {
            if (LocalDfuPlatform != DfuModel.LocalDfuPlatform)
            {
                LocalDfuPlatform = DfuModel.LocalDfuPlatform;
            }
            PrepareSelectedHexCommand.NotifyCanExecuteChanged();
        }
    }
    private bool CanValidateHexFile()
    {
        return !IsOperationInProgress && !string.IsNullOrEmpty(DfuModel.LocalDfuPlatform);
    }

    [ObservableProperty]
    public partial string? LocalDfuPlatform
    {
        get;
        set;
    }

    partial void OnLocalDfuPlatformChanged(string? value)
    {
        if (value != DfuModel.LocalDfuPlatform)
        {
            DfuModel.LocalDfuPlatform = value;
        }
        PrepareSelectedHexCommand.NotifyCanExecuteChanged();
    }


    [RelayCommand(CanExecute = nameof(CanValidateHexFile))]
    private Task PrepareSelectedHexAsync(CancellationToken cancellationToken)
    {
        return PrepareDfuArtifactAsync(cancellationToken);
    }

    /// <summary>Gets the authoritative plan for the current target and artifact.</summary>
    [ObservableProperty]
    public partial FirmwareInstallationPlan CurrentPlan { get; private set; } = FirmwareInstallationPlanResolver.Resolve(new());

    /// <summary>Gets the common selected-artifact presentation for APJ and combined HEX.</summary>
    [ObservableProperty]
    public partial FirmwareArtifactSummary SelectedArtifact { get; private set; } = new();

    /// <summary>Gets compatibility details separately from file validity.</summary>
    [ObservableProperty]
    public partial string CompatibilityStatus { get; private set; } = "Target compatibility has not been checked.";

    [ObservableProperty]
    public partial string? InstallStatus
    {
        get; private set;
    }

    private void SetDeviceInformation()
    {
        DfuProviderId = string.Empty;
        DfuVendorId = string.Empty;
        DfuProductId = string.Empty;
        DfuCorrelatedSource = string.Empty;
        DfuMessage = string.Empty;
        UsbDevicePortName = string.Empty;
        UsbIdentifier = string.Empty;
        UsbRuntime = string.Empty;
        UsbOperationMode = string.Empty;
        UsbRuntimeEvidence = string.Empty;
        UsbIdentity = string.Empty;
        UsbBetaflightTarget = string.Empty;


        if (DfuModel.SelectedDfuDevice is { } dfu)
        {
            DfuProviderId = dfu.Descriptor.ProviderId;
            DfuVendorId = $"{dfu.Descriptor.VendorId:X4}";
            DfuProductId = $"{dfu.Descriptor.ProductId:X4}";

            DfuCorrelatedSource = DfuModel.HasCorrelatedSource && DfuModel.CorrelatedHandoff is not null && DfuModel.CorrelatedHandoff!.Source.BetaflightIdentity is not null
                ? DfuModel.CorrelatedHandoff!.Source.BetaflightIdentity.ToString()
                : "Historical Betaflight identity";
            DfuMessage = "ROM DFU identifies the MCU boot environment, not the flight-controller board.";

            //return $"STM32 ROM DFU: {dfu.Descriptor.ProviderId}\nUSB {dfu.Descriptor.VendorId:X4}:{dfu.Descriptor.ProductId:X4}\n"
            //    + (DfuModel.HasCorrelatedSource ? $"Historical Betaflight identity: {DfuModel.CorrelatedHandoff!.Source.BetaflightIdentity}\n" : string.Empty)
            //    + "ROM DFU identifies the MCU boot environment, not the flight-controller board.";


        }

        var device = DevicesModel.SelectedDevice?.Descriptor;
        //return device is null ? "No physical controller selected. Firmware preparation is available."
        //    : $"{device.PortName} — USB {device.UsbIdentifier}\nRuntime: {CurrentPlan.Context.Runtime}\n
        //          Operating mode: {device.RuntimeProbe?.OperatingMode ?? "Unknown"}\n"
        //        + $"Runtime evidence: {device.RuntimeProbe?.Evidence} — {device.RuntimeProbe?.Verification} ({device.RuntimeProbe?.Code})\n"
        //        + $"Identity: {CurrentPlan.Context.IdentityConfidence} — {CurrentPlan.Context.IdentityEvidence}\n"
        //        + (device.BetaflightIdentity is { } identity ? $"Betaflight target: {identity.Board?.TargetName}, version: {identity.FirmwareVersion}" : string.Empty);


        NoDfuDeviceWhenNull = "No physical controller selected. Firmware preparation is available.";

        if (device is not null)
        {
            UsbDevicePortName = device.PortName;
            if (device.UsbIdentifier is not null)
            {
                UsbIdentifier = device.UsbIdentifier!.ToString();
            }

            UsbRuntime = CurrentPlan.Context.Runtime.ToString();
            UsbOperationMode = device.RuntimeProbe?.OperatingMode ?? "Unknown";
            UsbRuntimeEvidence = $"{device.RuntimeProbe?.Evidence} — {device.RuntimeProbe?.Verification} ({device.RuntimeProbe?.Code})";
            UsbIdentity = $"{CurrentPlan.Context.IdentityConfidence} — {CurrentPlan.Context.IdentityEvidence}";


            var identity = device.BetaflightIdentity;
            UsbBetaflightTarget = identity is not null ? $"Betaflight target: {identity.Board?.TargetName}, version: {identity.FirmwareVersion}" : string.Empty;
        }


    }

    [ObservableProperty]
    public partial string? UsbIdentity
    {
        get; private set;
    }
    [ObservableProperty]
    public partial string? UsbBetaflightTarget
    {
        get; private set;
    }


    [ObservableProperty]
    public partial string? UsbRuntimeEvidence
    {
        get; private set;
    }
    [ObservableProperty]
    public partial string? UsbOperationMode
    {
        get; private set;
    }

    [ObservableProperty]
    public partial string? UsbRuntime
    {
        get; private set;
    }


    [ObservableProperty]
    public partial string? UsbIdentifier
    {
        get; private set;
    }

    [ObservableProperty]
    public partial string? UsbDevicePortName
    {
        get; private set;
    }


    [ObservableProperty]
    public partial string? NoDfuDeviceWhenNull
    {
        get; private set;
    }


    [ObservableProperty]
    public partial string? DfuMessage
    {
        get; private set;
    }
    [ObservableProperty]
    public partial string? DfuCorrelatedSource
    {
        get; private set;
    }

    /// <summary>
    /// Gets compatibility details separately from file validity.
    /// </summary>
    [ObservableProperty]
    public partial string? DfuProviderId
    {
        get; private set;
    }

    [ObservableProperty]
    public partial string? DfuVendorId
    {
        get; private set;
    }
    [ObservableProperty]
    public partial string? DfuProductId
    {
        get; private set;
    }


    private void ResolveCurrentPlan()
    {
        InstallStatus = string.Empty;
        var serial = DevicesModel.SelectedDevice?.Descriptor;
        var dfu = DfuModel.SelectedDfuDevice?.Descriptor;
        var package = LocalFirmwareModel.PreparedLocalFirmware?.Package ?? ValidatedModel.PreparedFirmware?.Package;
        var artifact = DfuModel.PreparedArtifact is { } hex ? FirmwareArtifactSummary.FromHex(hex)
            : LocalFirmwareModel.PreparedLocalFirmware is { } local ? FirmwareArtifactSummary.FromLocal(local)
            : ValidatedModel.PreparedFirmware is { } online ? FirmwareArtifactSummary.FromOnline(online)
            : new FirmwareArtifactSummary();
        if (DfuModel.PreparedArtifact is not null && !UsesLocalDfuHex && OnlineFirmwareModel.SelectedFirmware?.Entry is { } release)
        {
            artifact = artifact with
            {
                Channel = release.Channel.ToString(),
                Version = release.Version.Value,
                VehicleFamily = release.Target.VehicleType.ToString(),
                GitSha = release.GitSha
            };
        }
        var compatible = false;
        var safetyConfirmed = false;
        CompatibilityStatus = "Target compatibility has not been checked.";

        if (dfu is not null && DfuModel.PreparedArtifact is { } preparedHex)
        {
            var safety = dfuSafety?.Evaluate(new(UsesLocalDfuHex ? DfuModel.LocalDfuPlatform : OnlineFirmwareModel.SelectedFirmware?.Platform,
                UsesLocalDfuHex ? null : OnlineFirmwareModel.SelectedFirmware?.BoardId, preparedHex,
                new DfuDeviceInformation(dfu, null, null, null, [], []),
                UsesLocalDfuHex ? null : OnlineFirmwareModel.SelectedFirmware?.Entry, ConfirmationPhrase: dfuTargetConfirmation));

            safetyConfirmed = safety is not null && safety.RequiredConfirmationPhrase is null && safety.Decision != DfuTargetSafetyDecision.Blocked;

            compatible = safety is not null && safety.Decision != DfuTargetSafetyDecision.Blocked
                && DfuModel.ToolStatus?.Availability == DfuToolAvailability.Available
                && dfu.DriverState == DfuDriverState.PresentReady;

            CompatibilityStatus = safety is null
                ? "Target safety service unavailable."
                : $"{safety.Decision}: {string.Join(", ", safety.EvidenceCodes)}. ";


            InstallStatus = (safety is not null && safety.RequiredConfirmationPhrase is { } phrase) ? $"Installation requires typing {phrase}." : string.Empty;

        }
        else if (dfu is null && package is not null && serial?.BootloaderIdentity is { } bootloader)
        {
            var checkedPackage = compatibility?.Check(package, bootloader, MissionPlanner.Firmware.Compatibility.FirmwareCompatibilityPolicy.Strict);
            compatible = checkedPackage?.IsCompatible == true;
            CompatibilityStatus = checkedPackage is null ? "Compatibility service unavailable." : $"{checkedPackage.Code}: {checkedPackage.TechnicalDetail}";
        }
        var runtime = serial?.RuntimeProbe?.Runtime
            ?? (serial?.BetaflightIdentity is not null ? FirmwareRuntimeKind.Betaflight
                : serial is null ? FirmwareRuntimeKind.None : FirmwareRuntimeKind.Unknown);
        var boot = dfu is not null ? FirmwareBootEnvironment.Stm32RomDfu
            : serial?.BootloaderIdentity is not null ? FirmwareBootEnvironment.ArduPilotBootloader
            : serial?.RuntimeProbe?.BootEnvironment ?? FirmwareBootEnvironment.None;
        var reviewed = DfuModel.HasCorrelatedSource && DfuModel.CorrelatedHandoff?.Source.BetaflightIdentity is { } source
            ? betaflightCompatibility.Resolve(source) : null;
        var context = new FirmwareWorkflowContext
        {
            HardwareSupported = OperatingSystem.IsWindows(),
            PhysicalTarget = dfu is not null ? FirmwarePhysicalTarget.Stm32Dfu : serial is not null ? FirmwarePhysicalTarget.Serial : FirmwarePhysicalTarget.None,
            Endpoint = dfu?.ProviderId ?? serial?.PortName,
            Runtime = dfu is null ? runtime : FirmwareRuntimeKind.None,
            RuntimeVerification = dfu is null ? serial?.RuntimeProbe?.Verification ?? FirmwareRuntimeVerification.None : FirmwareRuntimeVerification.None,
            BootEnvironment = boot,
            Bootloader = dfu is null ? serial?.BootloaderIdentity : null,
            IdentityConfidence = (dfu is null && serial?.BootloaderIdentity is not null) || reviewed is not null
                ? FirmwareIdentityConfidence.Verified : serial is not null ? FirmwareIdentityConfidence.Hint : FirmwareIdentityConfidence.Unknown,
            IdentityEvidence = reviewed is not null ? "Reviewed Betaflight target mapping"
                : serial?.BootloaderIdentity is { } identity ? $"ArduPilot protocol board ID {identity.BoardId}" : "USB/product hints are not exact board proof",
            Platform = artifact.Platform,
            BoardId = artifact.BoardId,
            Release = OnlineFirmwareModel.SelectedFirmware?.Entry,
            ActiveConnection = connectionGateway?.ActiveTransportKind,
            TargetPortOwned = dfu is null && serial is not null && (connectionGateway?.OwnsSerialPort(serial.PortName) ?? activeVehicle.IsOnline),
            TargetArmed = dfu is null && serial?.RuntimeProbe?.IsArmed == true,
            OperationInProgress = IsOperationInProgress || ArePanelsRefreshing,
            ArtifactFormat = artifact.Format,
            ArtifactValid = artifact.ArtifactValid,
            TargetCompatible = compatible,
            TargetSafetyConfirmed = safetyConfirmed
        };
        SelectedArtifact = artifact with
        {
            TargetCompatible = compatible
        };
        OnPropertyChanged(nameof(HasOnlineArtifact));
        OnPropertyChanged(nameof(HasLocalArtifact));
        CopySelectedUrlCommand.NotifyCanExecuteChanged();
        CurrentPlan = FirmwareInstallationPlanResolver.Resolve(context);
        HasPhysicalController = serial is not null || dfu is not null;
        ShowPhysicalController = HasPhysicalController || DevicesModel.DetectedDevices.Count > 0 || DfuModel.DfuDevices.Count > 0;
        IsFirmwareSelected = OnlineFirmwareModel.SelectedFirmware is not null
            || LocalFirmwareModel.PreparedLocalFirmware is not null
            || DfuModel.HasLocalDfuFirmware
            || artifact.ArtifactValid;
        ShowValidationAndCompatibility = HasPhysicalController && IsFirmwareSelected;
        CanValidateCompatibility = !context.OperationInProgress && artifact.ArtifactValid
            && (dfu is not null
                ? DfuModel.PreparedArtifact is not null && !string.IsNullOrWhiteSpace(artifact.Platform) && dfuSafety is not null
                : package is not null && serial?.BootloaderIdentity is not null && compatibility is not null);
        SetDeviceInformation();
        OnPropertyChanged(nameof(CanRebootToDfu));
        RebootToDfuCommand.NotifyCanExecuteChanged();
        EnterArduPilotBootloaderCommand.NotifyCanExecuteChanged();
        ProbeRuntimeCommand.NotifyCanExecuteChanged();
        ExecuteCurrentPlanCommand.NotifyCanExecuteChanged();
    }


    private bool CanEnterArduPilotBootloader()
    {
        return CurrentPlan.Capabilities.CanEnterArduPilotBootloader && bootloaderEntry is not null;
    }

    private bool CanProbeRuntime()
    {
        return CurrentPlan.Capabilities.CanProbeRuntime;
    }

    private bool CanExecuteCurrentPlan()
    {
        return CurrentPlan.CanExecute;
    }

    /// <summary>Guides manual BOOT/RESET and watches for a physical DFU endpoint without sending a reboot command.</summary>
    [RelayCommand]
    private async Task EnterManualDfuAsync(CancellationToken cancellationToken)
    {
        if (!CurrentPlan.Capabilities.CanRefreshPhysicalDevices)
        {
            return;
        }
        using var owned = BeginOperationCancellation(cancellationToken);
        try
        {
            SetOperation(true, FirmwareOperationState.ManualBootloaderReconnectRequired);
            var accepted = await dialogService.ConfirmAsync(dialogService.CreateOptions("Enter STM32 ROM DFU", "Continue", "Cancel"),
                "Remove propellers. Hold BOOT/DFU while reconnecting USB, or follow the board's BOOT + RESET instructions. Release BOOT after enumeration.", owned.Token);
            if (!accepted)
            {
                SetMessages("STM32 DFU entry cancelled.");
                return;
            }
            await ShowOperationDialogAsync("Waiting for STM32 DFU", owned);
            using var deadline = CancellationTokenSource.CreateLinkedTokenSource(owned.Token);
            deadline.CancelAfter(TimeSpan.FromSeconds(30));
            try
            {
                do
                {
                    await DfuModel.RefreshAfterInstallationAsync(deadline.Token);
                    if (DfuModel.HasDetectedDfuDevice)
                    {
                        SetMessages("STM32 DFU detected. Select the exact controller platform and combined HEX firmware.");
                        return;
                    }
                    await Task.Delay(500, deadline.Token);
                } while (true);
            }
            catch (OperationCanceledException) when (!owned.IsCancellationRequested)
            {
                SetMessages("STM32 DFU was not detected before the discovery deadline (dfu.discovery-timeout).");
            }
        }
        catch (OperationCanceledException)
        {
            SetMessages("STM32 DFU entry cancelled.");
        }
        finally
        {
            CloseOperationDialog();
            EndOperationCancellation(owned);
            SetOperation(false, null);
        }
    }

    /// <summary>Probes the selected physical controller without changing boot mode.</summary>
    [RelayCommand(CanExecute = nameof(CanProbeRuntime))]
    private async Task ProbeRuntimeAsync(CancellationToken cancellationToken)
    {
        await DevicesModel.ReprobeCommand.ExecuteAsync(null);
        cancellationToken.ThrowIfCancellationRequested();
        UpdatePanelCapabilities();
    }

    /// <summary>Enters and identifies an AP bootloader without erasing or programming.</summary>
    [RelayCommand(CanExecute = nameof(CanEnterArduPilotBootloader))]
    private async Task EnterArduPilotBootloaderAsync(CancellationToken cancellationToken)
    {
        if (!CanEnterArduPilotBootloader() || DevicesModel.SelectedDevice?.Descriptor is not { } source)
        {
            return;
        }
        using var owned = BeginOperationCancellation(cancellationToken);
        using var lease = firmwareOperations.Begin(FirmwareOperationKind.ProbeFirmwareIdentity);
        try
        {
            SetOperation(true, FirmwareOperationState.CheckingForBootloader);
            await ShowOperationDialogAsync("Entering ArduPilot bootloader", owned);
            var progress = CreateProgress();
            var result = await bootloaderEntry!.EnterAsync(new(new(source), source)
            {
                Progress = progress.Report
            }, owned.Token);
            if (result.Bootloader is { } found)
            {
                await using (found)
                {
                    var identified = found.Device with
                    {
                        BootloaderIdentity = found.Identity,
                        RuntimeProbe = new(FirmwareRuntimeKind.None, FirmwareBootEnvironment.ArduPilotBootloader, "runtime.ap-bootloader")
                    };
                    DevicesModel.SelectedDevice = new(identified, true, $"Protocol board ID {found.Identity.BoardId}");
                }
                lease.Transition(new(FirmwareOperationState.Completed, null, "entry.bootloader-identified"));
                SetMessages("ArduPilot bootloader identified. Firmware compatibility can now be checked.");
            }
            else
            {
                SetMessages(result.TechnicalDetail ?? result.Code);
            }
        }
        catch (OperationCanceledException)
        {
            SetMessages("ArduPilot bootloader entry cancelled.");
        }
        catch (System.Exception exception)
        {
            SetMessages(exception);
        }
        finally
        {
            if (lease.State != FirmwareOperationState.Completed)
            {
                lease.RequestCancellation();
            }
            CloseOperationDialog();
            EndOperationCancellation(owned);
            SetOperation(false, null);
        }
    }

    /// <summary>Delegates the current executable plan to the existing transport-specific installer.</summary>
    [RelayCommand(CanExecute = nameof(CanExecuteCurrentPlan))]
    private Task ExecuteCurrentPlanAsync(CancellationToken cancellationToken)
    {
        return CurrentPlan.Transport == BootloaderEntryTarget.Stm32RomDfu
            ? InstallDfuFirmwareAsync(cancellationToken) : InstallAsync(cancellationToken);
    }
}
