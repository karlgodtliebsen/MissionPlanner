using MissionPlanner.Firmware.Compatibility;
using MissionPlanner.Firmware.Discovery;
using MissionPlanner.Firmware.Downloads;
using MissionPlanner.Firmware.Entry;
using MissionPlanner.Firmware.Exceptions;
using MissionPlanner.Firmware.Model;
using MissionPlanner.Firmware.Operations;
using MissionPlanner.Firmware.Recovery;
using MissionPlanner.Firmware.Diagnostics;
using Microsoft.Extensions.Logging;

namespace MissionPlanner.Firmware.Installation;

/// <summary>Orchestrates the safety-ordered disconnected installation workflow.</summary>
public sealed class FirmwareInstallationService(
    IFirmwareOperationCoordinator operationCoordinator,
    IFirmwareConnectionGateway connectionGateway,
    IFirmwareArtifactDownloader downloader,
    IBootloaderEntryService entryService,
    IFirmwareCompatibilityService compatibility,
    IFirmwareUserInteraction interaction,
    IFirmwareApplicationDiscoveryService applicationDiscovery,
    ILogger<FirmwareInstallationService> logger) : IFirmwareInstallationService
{
    /// <inheritdoc />
    public async Task<FirmwareOperationResult> InstallAsync(
        FirmwareInstallationRequest request,
        IProgress<FirmwareProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var operation = operationCoordinator.Begin(FirmwareOperationKind.InstallApplicationFirmware);
        var startedAt = DateTimeOffset.UtcNow;
        var stage = FirmwareOperationState.Idle;
        var isLocalCustom = request.Source == FirmwareInstallationSource.LocalCustom;
        var requestedPolicy = request.CompatibilityPolicy ?? FirmwareCompatibilityPolicy.Strict;
        var effectivePolicy = isLocalCustom
            ? requestedPolicy
            : FirmwareCompatibilityPolicy.Strict;
        var boardIdOverride = requestedPolicy.AllowBoardIdMismatch
            ? FirmwareBoardIdOverrideState.RequestedNotUsed
            : FirmwareBoardIdOverrideState.NotRequested;
        ApjFirmwarePackage? diagnosticPackage = request.Package;
        string? firmwareSource = request.Artifact?.DownloadUri.AbsoluteUri ?? (isLocalCustom
            ? request.LocalFileName ?? "local/custom"
            : request.Package is null ? null : "official/catalogue (prepared package)");
        BootloaderIdentity? diagnosticBootloader = null;
        SerialDeviceDescriptor? diagnosticBootloaderDevice = null;
        long? bytesProgrammed = null;
        string? verificationResult = null;
        using var logScope = logger.BeginScope(new Dictionary<string, object?> { ["FirmwareOperationId"] = operation.OperationId });
        try
        {
            if (connectionGateway.IsVehicleConnected)
            {
                Transition(FirmwareOperationState.Failed, "installation.connection-conflict");
                throw new FirmwareConnectionConflictException(
                    $"Normal {connectionGateway.ActiveTransportKind} vehicle connection must be disconnected before firmware installation.",
                    operation.OperationId,
                    FirmwareOperationState.Failed);
            }

            var package = request.Package;
            string source;
            if (request.Artifact is not null)
            {
                Transition(FirmwareOperationState.Downloading, "installation.downloading");
                var downloaded = await downloader.DownloadAsync(request.Artifact, progress, cancellationToken).ConfigureAwait(false);
                package = downloaded.Package;
                diagnosticPackage = package;
                source = downloaded.Metadata.SourceUri.AbsoluteUri;
            }
            else if (package is not null)
            {
                source = isLocalCustom
                    ? request.LocalFileName ?? "local/custom"
                    : "official/catalogue (prepared package)";
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
                // EntryService already performs discovery after every strategy that can cause a
                // device transition. A final blind discovery here merely repeats the full timeout
                // and loses the strategy that actually failed.
                throw new FirmwareDeviceNotFoundException(
                    $"Bootloader entry failed ({entry.Code}). {entry.TechnicalDetail ?? "No ArduPilot serial bootloader was protocol-confirmed."}");
            }

            var bootloaderDevice = found.Device;
            diagnosticBootloader = found.Identity;
            diagnosticBootloaderDevice = found.Device;
            await using (found.ConfigureAwait(false))
            {
                Transition(
                    FirmwareOperationState.IdentifyingBootloader,
                    "installation.bootloader-identified",
                    $"Device: {found.Device.PortName}; board ID: {found.Identity.BoardId}; bootloader revision: {found.Identity.BootloaderRevision}");
                Transition(FirmwareOperationState.CheckingCompatibility, "installation.checking-compatibility");
                var decision = compatibility.Check(package, found.Identity, effectivePolicy);
                if (!decision.IsCompatible) throw new FirmwareCompatibilityException($"{decision.Code}: {decision.TechnicalDetail}");

                var mismatchOverrideUsed = effectivePolicy.AllowBoardIdMismatch &&
                                           package.BoardId != found.Identity.BoardId &&
                                           !(found.Identity.BoardId == 33 && package.BoardId == 9);
                if (mismatchOverrideUsed) boardIdOverride = FirmwareBoardIdOverrideState.Used;
                var requiredPhrase = mismatchOverrideUsed ? $"FLASH {package.BoardId} ON {found.Identity.BoardId}" : null;

                var confirmed = await interaction.ConfirmInstallationAsync(new FirmwareInstallationConfirmation(
                    package.BoardId, found.Identity.BoardId, found.Identity.BootloaderRevision, package.Image.Length, source,
                    mismatchOverrideUsed, requiredPhrase), cancellationToken).ConfigureAwait(false);
                if (!confirmed)
                {
                    Transition(FirmwareOperationState.Cancelled, "installation.not-confirmed");
                    return new FirmwareOperationResult(operation.OperationId, operation.Kind, FirmwareOperationState.Cancelled);
                }

                // The caller may cancel freely until final confirmation. Once erase starts, an
                // ordinary UI/navigation cancellation must not interrupt the recovery-critical
                // erase/program/verify/reboot sequence and strand the controller.
                cancellationToken.ThrowIfCancellationRequested();
                var destructiveToken = CancellationToken.None;
                Transition(FirmwareOperationState.Erasing, "installation.erasing");
                using var deferredCancellation = cancellationToken.Register(() =>
                    operation.RequestCancellation("installation.cancellation-deferred"));
                await found.Client.EraseAsync(destructiveToken).ConfigureAwait(false);
                Transition(FirmwareOperationState.Programming, "installation.programming");
                await found.Client.ProgramAsync(package, effectivePolicy, progress, destructiveToken).ConfigureAwait(false);
                bytesProgrammed = package.Image.Length + package.ExternalImage.Length;
                Transition(FirmwareOperationState.Verifying, "installation.verifying");
                var verification = await found.Client.VerifyAsync(package, effectivePolicy, destructiveToken).ConfigureAwait(false);
                verificationResult = verification.Succeeded ? "Succeeded" : $"Failed (expected 0x{verification.ExpectedChecksum:X8}, actual 0x{verification.ActualChecksum:X8})";
                if (!verification.Succeeded)
                    throw new FirmwareVerificationException($"Expected checksum 0x{verification.ExpectedChecksum:X8}; received 0x{verification.ActualChecksum:X8}.");
                Transition(FirmwareOperationState.Rebooting, "installation.rebooting");
                await found.Client.RebootAsync(destructiveToken).ConfigureAwait(false);
            }

            Transition(FirmwareOperationState.WaitingForApplication, "installation.waiting-for-application");
            if (operation.CancellationRequested || cancellationToken.IsCancellationRequested)
            {
                if (operation.State != FirmwareOperationState.Cancelled)
                    operation.RequestCancellation("installation.cancelled-at-safe-boundary");
                return new FirmwareOperationResult(operation.OperationId, operation.Kind, FirmwareOperationState.Cancelled,
                    DiagnosticReport: CreateDiagnostic(FirmwareOperationState.Cancelled));
            }

            var applicationDevice = await applicationDiscovery.FindAsync(
                new FirmwareApplicationDiscoveryRequest(bootloaderDevice, request.EntryContext.ApplicationDevice),
                cancellationToken).ConfigureAwait(false);
            Transition(FirmwareOperationState.Completed, "installation.completed");
            return new FirmwareOperationResult(
                operation.OperationId,
                operation.Kind,
                FirmwareOperationState.Completed,
                ApplicationDevice: applicationDevice,
                ReconnectSuggested: applicationDevice is not null,
                DiagnosticReport: CreateDiagnostic(FirmwareOperationState.Completed, applicationDevice));
        }
        catch (FirmwareConnectionConflictException exception)
        {
            logger.LogWarning(exception,
                "Firmware operation {OperationId} was rejected in state {FailureStage} because the vehicle connection owns the transport.",
                operation.OperationId,
                stage);
            throw;
        }
        catch (OperationCanceledException exception)
        {
            var failureStage = stage;
            if (operation.State == FirmwareOperationState.Cancelled) { }
            else if (operation.State is not (FirmwareOperationState.Erasing or FirmwareOperationState.Programming or FirmwareOperationState.Verifying or FirmwareOperationState.Rebooting))
                Transition(FirmwareOperationState.Cancelled, "installation.cancelled");
            else
                Transition(FirmwareOperationState.Failed, "installation.cancelled-after-destructive-stage");
            return new FirmwareOperationResult(operation.OperationId, operation.Kind, operation.State,
                new FirmwareOperationFailure("installation.cancelled", failureStage, exception.Message, exception.GetType().Name),
                DiagnosticReport: CreateDiagnostic(operation.State, failureCode: "installation.cancelled", failureStage: failureStage, failureDetail: exception.Message));
        }
        catch (Exception exception)
        {
            var failureStage = stage;
            if (operation.State is not (FirmwareOperationState.Completed or FirmwareOperationState.Cancelled or FirmwareOperationState.Failed))
                Transition(FirmwareOperationState.Failed, FailureCode(exception));
            logger.LogError(exception, "Firmware operation {OperationId} failed in state {FailureStage} with {FailureCode}.", operation.OperationId, failureStage, FailureCode(exception));
            return new FirmwareOperationResult(operation.OperationId, operation.Kind, operation.State,
                new FirmwareOperationFailure(FailureCode(exception), failureStage, exception.Message, exception.GetType().Name),
                DiagnosticReport: CreateDiagnostic(operation.State, failureCode: FailureCode(exception), failureStage: failureStage, failureDetail: exception.Message));
        }
        finally
        {
            operation.Dispose();
        }
        void Transition(FirmwareOperationState state, string code, string? technicalDetail = null)
        {
            stage = state;
            var report = new FirmwareProgress(state, null, code, technicalDetail: technicalDetail);
            operation.Transition(report);
            logger.LogInformation("Firmware operation {OperationId} entered {State} ({MessageCode}).", operation.OperationId, state, code);
            progress?.Report(report);
        }

        FirmwareDiagnosticReport CreateDiagnostic(
            FirmwareOperationState resultState,
            SerialDeviceDescriptor? applicationDevice = null,
            string? failureCode = null,
            FirmwareOperationState? failureStage = null,
            string? failureDetail = null) => new(
                operation.OperationId,
                resultState,
                firmwareSource,
                diagnosticPackage?.BoardId,
                diagnosticBootloader?.BoardId,
                diagnosticBootloader?.BootloaderRevision,
                request.EntryContext.ApplicationDevice?.StableIdentity ?? request.EntryContext.ApplicationDevice?.PortName,
                diagnosticBootloaderDevice?.StableIdentity ?? diagnosticBootloaderDevice?.PortName,
                applicationDevice?.StableIdentity ?? applicationDevice?.PortName,
                bytesProgrammed,
                verificationResult,
                failureCode,
                DateTimeOffset.UtcNow - startedAt,
                failureStage,
                failureDetail,
                boardIdOverride);
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
