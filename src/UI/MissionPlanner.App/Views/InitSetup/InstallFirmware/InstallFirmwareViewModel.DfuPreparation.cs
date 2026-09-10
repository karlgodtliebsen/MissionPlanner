using MissionPlanner.Firmware.Dfu;
using MissionPlanner.Firmware.Model;

namespace MissionPlanner.App.Views.InitSetup.InstallFirmware;

public sealed partial class InstallFirmwareViewModel
{
    private async Task PrepareDfuArtifactAsync(CancellationToken cancellationToken)
    {
        if (IsOperationInProgress || ArePanelsRefreshing
            || (UsesLocalDfuHex ? string.IsNullOrWhiteSpace(DfuModel.LocalDfuPlatform) : OnlineFirmwareModel.SelectedFirmware is null))
        {
            return;
        }
        var local = UsesLocalDfuHex;
        var entry = local ? null : OnlineFirmwareModel.SelectedFirmware!.Entry;
        var request = new DfuInstallationRequest(local ? DfuModel.LocalDfuPlatform!.Trim() : entry!.Target.Platform,
            entry?.Target.BoardId, DfuModel.SelectedDfuDevice?.Descriptor,
            ManifestEntry: entry, LocalHexPath: local ? DfuModel.LocalDfuFirmwarePath : null);
        using var owned = BeginOperationCancellation(cancellationToken);
        try
        {
            SetOperation(true, FirmwareOperationState.Downloading);
            DfuModel.PreparedArtifact = null;
            using var lease = firmwareOperations.Begin(FirmwareOperationKind.PrepareDfuArtifact);
            try
            {
                await ShowOperationDialogAsync("Preparing combined HEX", owned);
                var artifact = await dfuArtifactResolver.ResolveAsync(request, owned.Token);
                owned.Token.ThrowIfCancellationRequested();
                if (UsesLocalDfuHex != local || (local
                    ? DfuModel.LocalDfuFirmwarePath != request.LocalHexPath || DfuModel.LocalDfuPlatform?.Trim() != request.SelectedPlatform
                    : !ReferenceEquals(OnlineFirmwareModel.SelectedFirmware?.Entry, entry)))
                {
                    DfuModel.StatusMessage = "Selection changed; prepare the newly selected combined HEX before reviewing it.";
                    return;
                }
                DfuModel.PreparedArtifact = artifact;
                DfuModel.StatusMessage = "Combined HEX inspected. Review its source and the exact controller target before installation.";
                DfuModel.ErrorMessage = null;
                lease.Transition(new(FirmwareOperationState.Completed, null, "dfu.preview-completed"));
            }
            finally
            {
                if (lease.State != FirmwareOperationState.Completed)
                {
                    lease.RequestCancellation();
                }
            }
        }
        catch (OperationCanceledException) when (owned.IsCancellationRequested)
        {
            DfuModel.StatusMessage = "Combined HEX preparation cancelled.";
        }
        catch (Exception exception)
        {
            DfuModel.ErrorMessage = exception.Message;
        }
        finally
        {
            CloseOperationDialog();
            EndOperationCancellation(owned);
            SetOperation(false, null);
        }
    }
}
