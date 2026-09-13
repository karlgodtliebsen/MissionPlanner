using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MissionPlanner.Firmware.Model;
using MissionPlanner.Firmware.Workflow;

namespace MissionPlanner.App.Views.InitSetup.InstallFirmware;

public sealed partial class InstallFirmwareViewModel
{
    private string? workflowSelection;
    private string? workflowResult;
    private bool workflowInstalled;
    private bool hexPreparationFailed;
    private readonly List<string> completedOperationSteps = [];

    private bool IsKnownLocalPlatform => OnlineFirmwareModel.KnownPlatforms.Contains(DfuModel.LocalDfuPlatform?.Trim(), StringComparer.Ordinal);

    /// <summary>Loads local target choices without opening the online selector.</summary>
    [RelayCommand]
    private async Task LoadLocalPlatformChoicesAsync(CancellationToken cancellationToken)
    {
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, lifetime?.Token ?? CancellationToken.None);
        await OnlineFirmwareModel.EnsureKnownPlatformsAsync(cancellation.Token);
        UpdatePanelCapabilities();
    }

    /// <summary>Gets whether an online release still needs validation for the selected transport.</summary>
    public bool ShowOnlineValidation => OnlineFirmwareModel.SelectedFirmware is not null
        && !ShowHexPreparation && !SelectedArtifact.ArtifactValid;

    /// <summary>Gets whether the controller still needs to be placed in STM32 ROM DFU.</summary>
    public bool RequiresDfuEntry => DfuModel.SelectedDfuDevice is null
        && (CurrentPlan.Context.Runtime == FirmwareRuntimeKind.Betaflight || UsesLocalDfuHex || DfuModel.PreparedArtifact is not null);

    /// <summary>Gets whether target evidence exists for a compatibility evaluation.</summary>
    public bool HasCompatibilityResult => SelectedArtifact.ArtifactValid && (DfuModel.SelectedDfuDevice is not null
        ? DfuModel.PreparedArtifact is not null && dfuSafety is not null
        : DevicesModel.SelectedDevice?.Descriptor.BootloaderIdentity is not null && compatibility is not null
            && SelectedArtifact.Format == FirmwareArtifactFormat.Apj);

    /// <summary>Gets whether an evaluated target has failed its compatibility or readiness checks.</summary>
    public bool HasCompatibilityFailure => HasCompatibilityResult && !SelectedArtifact.TargetCompatible;

    /// <summary>Gets a compatibility result without representing pending checks as a mismatch.</summary>
    public string TargetCompatibilityText => HasCompatibilityResult ? SelectedArtifact.TargetCompatible.ToString()
        : RequiresDfuEntry ? "Not checked — enter STM32 DFU first"
        : !SelectedArtifact.ArtifactValid ? "Not checked — validate firmware first"
        : "Not checked — identify the controller first";

    [RelayCommand]
    private Task ValidateSelectedFirmwareAsync(CancellationToken cancellationToken)
    {
        return ShowHexPreparation ? PrepareDfuArtifactAsync(cancellationToken) : DownloadAndValidateAsync(cancellationToken);
    }

    /// <summary>Gets whether the selected source requires combined HEX preparation.</summary>
    public bool ShowHexPreparation => IsFirmwareSelected && (UsesLocalDfuHex
        || OnlineFirmwareModel.SelectedFirmware is not null && CurrentPlan.RequiredArtifactFormat == FirmwareArtifactFormat.WithBootloaderHex);

    /// <summary>Gets whether an inspected DFU artifact can be confirmed inline.</summary>
    public bool ShowDfuConfirmation => ShowValidationAndCompatibility && DfuModel.PreparedArtifact is not null
        && DfuModel.SelectedDfuDevice is not null;

    /// <summary>Gets the exact phrase required to confirm the intended controller platform.</summary>
    public string DfuConfirmationPlaceholder => $"Type FLASH {SelectedArtifact.Platform} to confirm the controller target";

    /// <summary>Gets or sets the operator's inline target confirmation.</summary>
    [ObservableProperty]
    public partial string? DfuConfirmationText
    {
        get;
        set;
    }

    partial void OnDfuConfirmationTextChanged(string? value)
    {
        dfuTargetConfirmation = null;
        UpdatePanelCapabilities();
    }

    private bool CanReviewDfuTarget() => !IsOperationInProgress && !ArePanelsRefreshing && ShowDfuConfirmation
        && !string.IsNullOrWhiteSpace(SelectedArtifact.Platform)
        && string.Equals(DfuConfirmationText?.Trim(), $"FLASH {SelectedArtifact.Platform}", StringComparison.Ordinal);

    private void InvalidateDfuConfirmation()
    {
        dfuTargetConfirmation = null;
        DfuConfirmationText = null;
    }

    private void UpdateWorkflowGuidance()
    {
        var selection = $"{SelectedArtifact.LocalFile}|{SelectedArtifact.OnlineUrl}|{SelectedArtifact.Platform}|{SelectedArtifact.Sha256}";
        if (workflowSelection != selection)
        {
            workflowSelection = selection;
            workflowResult = null;
            workflowInstalled = false;
            hexPreparationFailed = false;
            completedOperationSteps.Clear();
        }
        var completed = new List<string>();
        if (OnlineFirmwareModel.KnownPlatforms.Count > 0)
        {
            completed.Add("Firmware catalogue loaded.");
        }
        if (HasPhysicalController)
        {
            completed.Add(DfuModel.SelectedDfuDevice is not null ? "STM32 DFU controller selected."
                : $"Controller selected: {DevicesModel.SelectedDevice?.Descriptor.PortName}.");
        }
        if (CurrentPlan.Context.RuntimeVerification == FirmwareRuntimeVerification.Verified)
        {
            completed.Add($"Controller runtime identified: {CurrentPlan.Context.Runtime}.");
        }
        if (CurrentPlan.Context.Bootloader is not null)
        {
            completed.Add("ArduPilot bootloader identity read.");
        }
        if (IsFirmwareSelected)
        {
            completed.Add($"Firmware selected: {SelectedArtifact.LocalFile ?? SelectedArtifact.OnlineUrl?.ToString() ?? SelectedArtifact.Source}.");
        }
        if (SelectedArtifact.ArtifactValid)
        {
            completed.Add(DfuModel.PreparedArtifact is not null ? "Combined HEX validated." : "APJ firmware validated.");
        }
        if (SelectedArtifact.TargetCompatible && DfuModel.PreparedArtifact is null)
        {
            completed.Add("Firmware board compatibility checked.");
        }
        if (CurrentPlan.Context.TargetSafetyConfirmed)
        {
            completed.Add("DFU controller target confirmed.");
        }
        completed.AddRange(completedOperationSteps);
        if (workflowResult is not null)
        {
            completed.Add(workflowResult);
        }
        WorkflowProgress = completed.Count == 0 ? "Ready to select a controller and firmware." : string.Join(Environment.NewLine, completed);

        WorkflowNextStep = IsOperationInProgress
            ? IsCancellationDeferred ? "Keep power connected while verification and reboot finish."
                : $"Wait: {StatusMessage ?? ProgressMessage ?? "firmware operation in progress"}"
            : ArePanelsRefreshing ? "Wait for device discovery and the firmware catalogue to finish loading."
            : workflowInstalled ? "Reconnect to ArduPilot and check the controller configuration."
            : hexPreparationFailed ? "Firmware validation failed. Review the error above and select a consistent release or corrected combined HEX before retrying Validate combined HEX."
            : RequiresDfuEntry ? "Enter STM32 DFU using the BOOT button: disconnect USB, hold BOOT while reconnecting USB, then release BOOT. Click Enter DFU using BOOT button to detect the controller. The selected firmware is retained; validate it and confirm the target after DFU is detected."
            : HasPhysicalController && CurrentPlan.RequiredArtifactFormat == FirmwareArtifactFormat.None
                ? "Identify the controller runtime. Click Probe runtime, or use the controller's documented bootloader entry procedure and refresh devices."
            : !IsFirmwareSelected ? "Select online firmware or a local firmware file."
            : ShowHexPreparation && DfuModel.PreparedArtifact is null
                ? UsesLocalDfuHex && !IsKnownLocalPlatform
                    ? OnlineFirmwareModel.KnownPlatforms.Count == 0
                        ? "Refresh the firmware catalogue to load known controller platforms, then select the intended platform and validate the combined HEX."
                        : "Select the intended controller platform from the catalogue choices, then click Validate combined HEX. If the platform is unknown, use an online release for your exact controller."
                    : "Click Validate combined HEX, then confirm the controller target below."
            : !SelectedArtifact.ArtifactValid ? "Download and validate the selected firmware."
            : !HasPhysicalController ? "Connect and select the physical controller."
            : CurrentPlan.Context.TargetPortOwned || CurrentPlan.Context.TargetArmed ? CurrentPlan.BlockReason
            : ShowDfuConfirmation && !CurrentPlan.Context.TargetSafetyConfirmed
                ? $"Check that {SelectedArtifact.Platform} is the exact controller platform. {DfuConfirmationPlaceholder}, then click Confirm DFU target."
            : CurrentPlan.CanExecute ? "Click Install firmware to program the selected controller."
            : CurrentPlan.BlockReason ?? "Review the controller and firmware selection.";
    }

    private void RecordWorkflowResult(string message, bool installed = false)
    {
        workflowResult = message;
        workflowInstalled = installed;
        UpdateWorkflowGuidance();
    }

    private void RecordDfuProgress(MissionPlanner.Firmware.Dfu.DfuOperationState state)
    {
        var completed = state switch
        {
            MissionPlanner.Firmware.Dfu.DfuOperationState.Verifying => "Firmware programming completed; verification started.",
            MissionPlanner.Firmware.Dfu.DfuOperationState.Detaching => "Firmware verification completed; reboot started.",
            MissionPlanner.Firmware.Dfu.DfuOperationState.WaitingForApplication => "Reboot requested; waiting for ArduPilot.",
            _ => null
        };
        if (completed is not null && !completedOperationSteps.Contains(completed))
        {
            completedOperationSteps.Add(completed);
        }
    }

    private void RecordOperationProgress(FirmwareOperationState state)
    {
        var completed = state switch
        {
            FirmwareOperationState.Verifying => "Firmware programming completed; verification started.",
            FirmwareOperationState.Rebooting => "Firmware verification completed; reboot started.",
            FirmwareOperationState.WaitingForApplication => "Reboot requested; waiting for ArduPilot.",
            _ => null
        };
        if (completed is not null && !completedOperationSteps.Contains(completed))
        {
            completedOperationSteps.Add(completed);
        }
    }
}
