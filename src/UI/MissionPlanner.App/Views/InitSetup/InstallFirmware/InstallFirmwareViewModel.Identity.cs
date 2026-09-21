using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MissionPlanner.Firmware.Compatibility;
using MissionPlanner.Firmware.Model;

namespace MissionPlanner.App.Views.InitSetup.InstallFirmware;

public sealed partial class InstallFirmwareViewModel
{
    private string? identitySelectionKey;

    /// <summary>Gets explicit operation intent, reset when controller or artifact changes.</summary>
    [ObservableProperty]
    public partial FirmwareInstallMode InstallMode { get; private set; }

    /// <summary>Gets whether this operation explicitly replaces a target.</summary>
    public bool IsRecovery => InstallMode == FirmwareInstallMode.Recovery;

    /// <summary>Gets whether normal compatibility offers recovery as the next action.</summary>
    [ObservableProperty]
    public partial bool CanOfferRecovery { get; private set; }

    /// <summary>Gets source-attributed policy evidence.</summary>
    [ObservableProperty]
    public partial FirmwareCompatibilityResult? IdentityDecision { get; private set; }

    /// <summary>Gets separate physical, bootloader, running and selected identities.</summary>
    [ObservableProperty]
    public partial string IdentityEvidenceText { get; private set; } = string.Empty;

    [RelayCommand(CanExecute = nameof(CanOfferRecovery))]
    private void BeginRecovery()
    {
        InstallMode = FirmwareInstallMode.Recovery;
        OnPropertyChanged(nameof(IsRecovery));
        RefreshRecoveryPlan();
    }

    [RelayCommand]
    private void EndRecovery()
    {
        InstallMode = FirmwareInstallMode.NormalUpgrade;
        OnPropertyChanged(nameof(IsRecovery));
        RefreshRecoveryPlan();
    }

    private void RefreshRecoveryPlan()
    {
        ResolveCurrentPlan();
        InstallCommand.NotifyCanExecuteChanged();
        ExecuteCurrentPlanCommand.NotifyCanExecuteChanged();
    }

    private void ResolveIdentityEvidence(SerialDeviceDescriptor? device, ApjFirmwarePackage? package)
    {
        var selected = LocalFirmwareModel.CustomPackage is { } local ? local.Identity
            : OnlineFirmwareModel.SelectedFirmware?.Entry is { } release ? SelectedFirmwareIdentity.FromRelease(release)
            : package?.Identity;
        var key = $"{device?.StableIdentity}|{device?.PortName}|{selected}|{LocalFirmwareModel.PreparedLocalFirmware?.ArtifactMetadata.Sha256 ?? ValidatedModel.PreparedFirmware?.Sha256}";
        if (identitySelectionKey != key)
        {
            identitySelectionKey = key;
            InstallMode = FirmwareInstallMode.NormalUpgrade;
            OnPropertyChanged(nameof(IsRecovery));
        }
        var snapshot = new FirmwareIdentitySnapshot(
            device is null ? null : new(null, device.UsbIdentifier, device.UsbSerialNumber),
            device?.BootloaderIdentity, device is null ? null : upgradeConnection?.ReadRunningIdentity(device) ?? device.RuntimeProbe?.RunningIdentity, selected);
        IdentityEvidenceText = FirmwareIdentityPresentation.Format(snapshot);
        var normal = selected is not null && device is not null
            ? FirmwareIdentityCompatibility.Evaluate(snapshot, FirmwareInstallMode.NormalUpgrade, package) : null;
        CanOfferRecovery = !IsOperationInProgress && !ArePanelsRefreshing && !IsRecovery
            && OperatingSystem.IsWindows() && normal?.CanOfferRecovery == true && activeVehicle.State?.IsArmed != true;
        IdentityDecision = IsRecovery ? FirmwareIdentityCompatibility.Evaluate(snapshot, InstallMode, package) : normal;
        BeginRecoveryCommand.NotifyCanExecuteChanged();
    }
}
