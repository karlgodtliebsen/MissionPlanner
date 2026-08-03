using MissionPlanner.Firmware.Compatibility;
using MissionPlanner.Firmware.Discovery;
using MissionPlanner.Firmware.Downloads;
using MissionPlanner.Firmware.Entry;
using MissionPlanner.Firmware.Exceptions;
using MissionPlanner.Firmware.Model;
using MissionPlanner.Firmware.Operations;
using MissionPlanner.Firmware.Recovery;

namespace MissionPlanner.Firmware.Installation;

/// <summary>Orchestrates the safety-ordered disconnected installation workflow.</summary>
public sealed class FirmwareInstallationService(
    IFirmwareOperationCoordinator operationCoordinator,
    IFirmwareConnectionGateway connectionGateway,
    IFirmwareArtifactDownloader downloader,
    IBootloaderEntryService entryService,
    IBootloaderDiscoveryService discovery,
    IFirmwareCompatibilityService compatibility,
    IFirmwareUserInteraction interaction,
    IFirmwareApplicationDiscoveryService applicationDiscovery) : IFirmwareInstallationService
{
    /// <inheritdoc />
    public async Task<FirmwareOperationResult> InstallAsync(
        FirmwareInstallationRequest request,
        IProgress<FirmwareProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var operation = operationCoordinator.Begin(FirmwareOperationKind.InstallApplicationFirmware);
        var stage = FirmwareOperationState.Idle;
        try
        {
            if (connectionGateway.IsVehicleConnected)
            {
                Transition(FirmwareOperationState.Failed, "installation.connection-conflict");
                throw new FirmwareConnectionConflictException($"Normal {connectionGateway.ActiveTransportKind} vehicle connection must be disconnected before firmware installation.");
            }

            var package = request.Package;
            string source;
            if (request.Artifact is not null)
            {
                Transition(FirmwareOperationState.Downloading, "installation.downloading");
                var downloaded = await downloader.DownloadAsync(request.Artifact, progress, cancellationToken).ConfigureAwait(false);
                package = downloaded.Package;
                source = downloaded.Metadata.SourceUri.AbsoluteUri;
            }
            else if (package is not null)
            {
                source = "custom";
            }
            else
            {
                throw new FirmwarePackageException("An artifact or validated package is required.");
            }

            Transition(FirmwareOperationState.ValidatingPackage, "installation.package-validated");
            Transition(FirmwareOperationState.WaitingForDevice, "installation.waiting-for-device");
            Transition(FirmwareOperationState.EnteringBootloader, "installation.entering-bootloader");
            var entry = await entryService.EnterAsync(request.EntryContext, cancellationToken).ConfigureAwait(false);
            DiscoveredBootloader found;
            if (entry is { Outcome: BootloaderEntryOutcome.BootloaderIdentified, Bootloader: not null }) found = entry.Bootloader;
            else
            {
                Transition(FirmwareOperationState.WaitingForDevice, "installation.waiting-for-bootloader");
                found = await discovery.FindAsync(request.EntryContext.DiscoveryRequest, progress, cancellationToken).ConfigureAwait(false);
            }

            var bootloaderDevice = found.Device;
            await using (found.ConfigureAwait(false))
            {
                Transition(FirmwareOperationState.IdentifyingBootloader, "installation.bootloader-identified");
                Transition(FirmwareOperationState.CheckingCompatibility, "installation.checking-compatibility");
                var decision = compatibility.Check(package, found.Identity);
                if (!decision.IsCompatible) throw new FirmwareCompatibilityException($"{decision.Code}: {decision.TechnicalDetail}");

                var confirmed = await interaction.ConfirmInstallationAsync(new FirmwareInstallationConfirmation(
                    package.BoardId, found.Identity.BoardId, found.Identity.BootloaderRevision, package.Image.Length, source), cancellationToken).ConfigureAwait(false);
                if (!confirmed)
                {
                    Transition(FirmwareOperationState.Cancelled, "installation.not-confirmed");
                    return new FirmwareOperationResult(operation.OperationId, operation.Kind, FirmwareOperationState.Cancelled);
                }

                Transition(FirmwareOperationState.Erasing, "installation.erasing");
                await found.Client.EraseAsync(cancellationToken).ConfigureAwait(false);
                Transition(FirmwareOperationState.Programming, "installation.programming");
                await found.Client.ProgramAsync(package, progress, cancellationToken).ConfigureAwait(false);
                Transition(FirmwareOperationState.Verifying, "installation.verifying");
                var verification = await found.Client.VerifyAsync(package, cancellationToken).ConfigureAwait(false);
                if (!verification.Succeeded)
                    throw new FirmwareVerificationException($"Expected checksum 0x{verification.ExpectedChecksum:X8}; received 0x{verification.ActualChecksum:X8}.");
                Transition(FirmwareOperationState.Rebooting, "installation.rebooting");
                await found.Client.RebootAsync(cancellationToken).ConfigureAwait(false);
            }

            Transition(FirmwareOperationState.WaitingForApplication, "installation.waiting-for-application");
            var applicationDevice = await applicationDiscovery.FindAsync(
                new FirmwareApplicationDiscoveryRequest(bootloaderDevice, request.EntryContext.ApplicationDevice),
                cancellationToken).ConfigureAwait(false);
            Transition(FirmwareOperationState.Completed, "installation.completed");
            return new FirmwareOperationResult(
                operation.OperationId,
                operation.Kind,
                FirmwareOperationState.Completed,
                ApplicationDevice: applicationDevice,
                ReconnectSuggested: applicationDevice is not null);
        }
        catch (FirmwareConnectionConflictException)
        {
            throw;
        }
        catch (OperationCanceledException exception)
        {
            var failureStage = stage;
            if (operation.State is not (FirmwareOperationState.Erasing or FirmwareOperationState.Programming or FirmwareOperationState.Verifying or FirmwareOperationState.Rebooting or FirmwareOperationState.WaitingForApplication))
                Transition(FirmwareOperationState.Cancelled, "installation.cancelled");
            else
                Transition(FirmwareOperationState.Failed, "installation.cancelled-after-destructive-stage");
            return new FirmwareOperationResult(operation.OperationId, operation.Kind, operation.State,
                new FirmwareOperationFailure("installation.cancelled", failureStage, exception.Message, exception.GetType().Name));
        }
        catch (Exception exception)
        {
            var failureStage = stage;
            if (operation.State is not (FirmwareOperationState.Completed or FirmwareOperationState.Cancelled or FirmwareOperationState.Failed))
                Transition(FirmwareOperationState.Failed, FailureCode(exception));
            return new FirmwareOperationResult(operation.OperationId, operation.Kind, operation.State,
                new FirmwareOperationFailure(FailureCode(exception), failureStage, exception.Message, exception.GetType().Name));
        }
        finally
        {
            operation.Dispose();
        }
        void Transition(FirmwareOperationState state, string code)
        {
            stage = state;
            var report = new FirmwareProgress(state, null, code);
            operation.Transition(report);
            progress?.Report(report);
        }
    }

    private static string FailureCode(Exception exception) => exception switch
    {
        FirmwareCompatibilityException => "installation.compatibility-failed",
        FirmwareVerificationException => "installation.verification-failed",
        FirmwareDeviceNotFoundException => "installation.device-not-found",
        FirmwareDownloadException => "installation.download-failed",
        FirmwarePackageException => "installation.package-invalid",
        FirmwareBootloaderException => "installation.bootloader-failed",
        _ => "installation.failed"
    };
}
