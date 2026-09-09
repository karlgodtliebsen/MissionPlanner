using MissionPlanner.Firmware.Dfu;
using MissionPlanner.Firmware.Model;

namespace MissionPlanner.App.Views.InitSetup.InstallFirmware;

public sealed partial class InstallFirmwareViewModel
{
    private async Task PrepareDfuArtifactAsync(CancellationToken cancellationToken)
    {
        if (!CanStartDfuInstall())
        {
            return;
        }
        var local = UsesLocalDfuHex;
        var entry = local ? null : Catalogue.SelectedFirmware!.Entry;
        var request = new DfuInstallationRequest(local ? Dfu.LocalDfuPlatform!.Trim() : entry!.Target.Platform,
            entry?.Target.BoardId, Dfu.SelectedDfuDevice!.Descriptor,
            ManifestEntry: entry, LocalHexPath: local ? Dfu.LocalDfuFirmwarePath : null);
        using var owned = BeginOperationCancellation(cancellationToken);
        try
        {
            SetOperation(true, FirmwareOperationState.Downloading);
            Dfu.PreparedArtifact = null;
            using var lease = firmwareOperations.Begin(FirmwareOperationKind.PrepareDfuArtifact);
            try
            {
                await ShowOperationDialogAsync("Preparing combined HEX", owned);
                var artifact = await dfuArtifactResolver.ResolveAsync(request, owned.Token);
                owned.Token.ThrowIfCancellationRequested();
                Dfu.PreparedArtifact = artifact;
                Dfu.StatusMessage = "Combined HEX inspected. Review its source and the exact controller target before installation.";
                Dfu.ErrorMessage = null;
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
            Dfu.StatusMessage = "Combined HEX preparation cancelled.";
        }
        catch (Exception exception)
        {
            Dfu.ErrorMessage = exception.Message;
        }
        finally
        {
            CloseOperationDialog();
            EndOperationCancellation(owned);
            SetOperation(false, null);
        }
    }
}
