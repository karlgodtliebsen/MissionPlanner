using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MissionPlanner.Firmware.Installation;
using MissionPlanner.Firmware.Model;
using MissionPlanner.Firmware.Operations;
using MissionPlanner.Firmware.Recovery;

namespace MissionPlanner.Firmware.Dfu;

/// <summary>Orchestrates the safety-ordered DFU installation workflow.</summary>
public sealed class DfuInstallationService(
    IFirmwareOperationCoordinator operationCoordinator,
    IFirmwareConnectionGateway connectionGateway,
    IDfuToolLocator toolLocator,
    IDfuArtifactResolver artifactResolver,
    IDfuDeviceCatalog deviceCatalog,
    IDfuDeviceMonitor deviceMonitor,
    IDfuProgrammer programmer,
    IDfuTargetSafetyService targetSafety,
    IDfuUserInteraction interaction,
    IFirmwareApplicationDiscoveryService applicationDiscovery,
    IOptions<DfuOptions> options,
    ILogger<DfuInstallationService> logger) : IDfuInstallationService
{
    private readonly DfuOptions configured = options.Value;

    /// <inheritdoc />
    public async Task<DfuProgrammingResult> InstallAsync(
        DfuInstallationRequest request,
        IProgress<DfuProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        using var operation = operationCoordinator.Begin(FirmwareOperationKind.InstallApplicationAndBootloaderDfu);
        var stage = DfuOperationState.Idle;
        var programmingVerified = false;
        var warnings = new List<string>();
        DfuProgrammingResult? providerResult = null;

        try
        {
            if (connectionGateway.IsVehicleConnected)
                return Fail("dfu.connection-conflict", "The normal vehicle connection must be disconnected before DFU installation.");

            Transition(FirmwareOperationState.Downloading, DfuOperationState.LocatingTool, "dfu.locating-tool");
            var tool = await toolLocator.LocateAsync(cancellationToken).ConfigureAwait(false);
            if (tool.Availability != DfuToolAvailability.Available)
                return Fail("dfu.tool-unavailable", tool.Diagnostic ?? "STM32CubeProgrammer is unavailable.", DfuProgrammingOutcome.ToolNotFound);

            stage = DfuOperationState.ResolvingArtifact;
            progress?.Report(new DfuProgress(stage, "dfu.resolving-artifact"));
            var artifact = request.Artifact ?? await artifactResolver.ResolveAsync(request, cancellationToken).ConfigureAwait(false);
            Transition(FirmwareOperationState.ValidatingPackage, DfuOperationState.InspectingHex, "dfu.artifact-inspected");
            Transition(FirmwareOperationState.WaitingForDevice, DfuOperationState.WaitingForDevice, "dfu.waiting-for-device");
            var device = (await deviceCatalog.GetDevicesAsync(cancellationToken).ConfigureAwait(false)).FirstOrDefault(candidate => SameDevice(candidate, request.Device));
            if (device is null) return Fail("dfu.device-not-found", "The selected DFU device is no longer present.", DfuProgrammingOutcome.NoDfuDevice);
            if (device.DriverState != DfuDriverState.PresentReady)
                return Fail("dfu.driver-not-ready", $"The selected DFU device driver state is {device.DriverState}.", DfuProgrammingOutcome.ConnectionFailed);

            Transition(FirmwareOperationState.IdentifyingBootloader, DfuOperationState.InspectingDevice, "dfu.inspecting-device");
            var deviceInformation = await programmer.InspectAsync(device, cancellationToken).ConfigureAwait(false);
            Transition(FirmwareOperationState.CheckingCompatibility, DfuOperationState.AwaitingConfirmation, "dfu.checking-target-safety");
            var safety = targetSafety.Evaluate(new DfuTargetSafetyRequest(
                request.SelectedPlatform, request.SelectedBoardId, artifact, deviceInformation, request.ManifestEntry,
                request.PreviousApplicationDevice?.StableIdentity, request.RememberedAssociation, request.ConfirmationPhrase));
            if (safety.Decision == DfuTargetSafetyDecision.Blocked || safety.RequiredConfirmationPhrase is not null)
                return Fail("dfu.target-safety-blocked", string.Join(", ", safety.EvidenceCodes), DfuProgrammingOutcome.FileRejected);

            var confirmation = new DfuInstallationConfirmation(request.SelectedPlatform, request.SelectedBoardId, device, deviceInformation, artifact, safety);
            if (!await interaction.ConfirmAsync(confirmation, cancellationToken).ConfigureAwait(false))
                return Cancel("dfu.not-confirmed");

            cancellationToken.ThrowIfCancellationRequested();
            var capabilities = await programmer.GetCapabilitiesAsync(cancellationToken).ConfigureAwait(false);
            using var deferredCancellation = cancellationToken.Register(() => operation.RequestCancellation("dfu.cancellation-deferred"));
            var providerToken = capabilities.CanSafelyCancelProgramming ? cancellationToken : CancellationToken.None;
            Transition(FirmwareOperationState.Programming, DfuOperationState.Programming, "dfu.programming");
            providerResult = await programmer.ProgramAndVerifyAsync(
                new DfuProgrammingRequest(device, artifact), progress, providerToken).ConfigureAwait(false);
            if (!providerResult.ProgrammingSucceeded)
                return Fail(providerResult.Failure?.Code ?? "dfu.programming-failed", providerResult.Failure?.Message ?? "DFU programming failed.", providerResult.Outcome, providerResult);

            Transition(FirmwareOperationState.Verifying, DfuOperationState.Verifying, "dfu.verifying");
            if (!providerResult.VerificationSucceeded)
                return Fail(providerResult.Failure?.Code ?? "dfu.verification-failed", providerResult.Failure?.Message ?? "DFU verification failed.", DfuProgrammingOutcome.VerificationFailed, providerResult);
            programmingVerified = true;

            Transition(FirmwareOperationState.Rebooting, DfuOperationState.Detaching, "dfu.resetting-device");
            if (!capabilities.CanDetach && !await interaction.AcknowledgePowerCycleAsync(confirmation, CancellationToken.None).ConfigureAwait(false))
                warnings.Add("dfu.power-cycle-not-acknowledged");

            if (!await WaitForDisappearanceAsync(device).ConfigureAwait(false)) warnings.Add("dfu.device-still-present");
            Transition(FirmwareOperationState.WaitingForApplication, DfuOperationState.WaitingForApplication, "dfu.waiting-for-application");
            if (cancellationToken.IsCancellationRequested)
            {
                operation.RequestCancellation("dfu.cancelled-at-safe-boundary");
                return Result(DfuOperationState.Cancelled, false);
            }

            var bootloaderEvidence = new SerialDeviceDescriptor(
                "DFU", device.PnpInstanceId ?? device.ProviderId, new UsbIdentifier(device.VendorId, device.ProductId),
                device.SerialNumber, device.ProductName, device.Manufacturer, arrivedAt: device.ArrivedAt ?? device.ObservedAt);
            var application = await applicationDiscovery.FindAsync(new FirmwareApplicationDiscoveryRequest(
                bootloaderEvidence, request.PreviousApplicationDevice, configured.DfuApplicationRediscoveryTimeout), cancellationToken).ConfigureAwait(false);
            if (application is null) warnings.Add("dfu.application-not-rediscovered");
            Transition(FirmwareOperationState.Completed, DfuOperationState.Completed, "dfu.completed");
            return Result(DfuOperationState.Completed, application is not null);
        }
        catch (OperationCanceledException)
        {
            if (operation.State is not (FirmwareOperationState.Completed or FirmwareOperationState.Cancelled or FirmwareOperationState.Failed))
                operation.RequestCancellation("dfu.cancelled");
            return Result(operation.State == FirmwareOperationState.Cancelled ? DfuOperationState.Cancelled : DfuOperationState.Failed, false,
                new DfuFailure("dfu.cancelled", stage, "The DFU operation was cancelled."));
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "DFU operation {OperationId} failed during {Stage}.", operation.OperationId, stage);
            return Fail("dfu.failed", exception.Message);
        }

        DfuProgrammingResult Fail(string code, string message, DfuProgrammingOutcome outcome = DfuProgrammingOutcome.ProgrammingFailed, DfuProgrammingResult? source = null)
        {
            if (operation.State is not (FirmwareOperationState.Completed or FirmwareOperationState.Cancelled or FirmwareOperationState.Failed))
                Transition(FirmwareOperationState.Failed, DfuOperationState.Failed, code);
            return new DfuProgrammingResult(DfuOperationState.Failed, programmingVerified || source?.ProgrammingSucceeded == true,
                programmingVerified || source?.VerificationSucceeded == true, false, new DfuFailure(code, stage, message),
                source?.ProviderLog, source?.ExitCode, outcome, operation.OperationId, warnings);
        }

        DfuProgrammingResult Cancel(string code)
        {
            Transition(FirmwareOperationState.Cancelled, DfuOperationState.Cancelled, code);
            return Result(DfuOperationState.Cancelled, false);
        }

        DfuProgrammingResult Result(DfuOperationState state, bool applicationRediscovered, DfuFailure? failure = null) =>
            new(state, programmingVerified, programmingVerified, applicationRediscovered, failure, providerResult?.ProviderLog,
                providerResult?.ExitCode, programmingVerified ? DfuProgrammingOutcome.Succeeded : providerResult?.Outcome ?? DfuProgrammingOutcome.ProgrammingFailed,
                operation.OperationId, warnings);

        void Transition(FirmwareOperationState operationState, DfuOperationState dfuState, string code)
        {
            stage = dfuState;
            operation.Transition(new FirmwareProgress(operationState, null, code));
            progress?.Report(new DfuProgress(dfuState, code));
        }
    }

    private async Task<bool> WaitForDisappearanceAsync(DfuDeviceDescriptor device)
    {
        using var timeout = new CancellationTokenSource(configured.DfuDisappearanceTimeout);
        try
        {
            if (!(await deviceCatalog.GetDevicesAsync(timeout.Token).ConfigureAwait(false)).Any(candidate => SameDevice(candidate, device))) return true;
            await foreach (var snapshot in deviceMonitor.WatchAsync(timeout.Token).ConfigureAwait(false))
                if (!snapshot.Any(candidate => SameDevice(candidate, device))) return true;
        }
        catch (OperationCanceledException) when (timeout.IsCancellationRequested) { }
        return false;
    }

    private static bool SameDevice(DfuDeviceDescriptor left, DfuDeviceDescriptor right) =>
        string.Equals(left.ProviderId, right.ProviderId, StringComparison.OrdinalIgnoreCase) ||
        (!string.IsNullOrWhiteSpace(left.PnpInstanceId) && string.Equals(left.PnpInstanceId, right.PnpInstanceId, StringComparison.OrdinalIgnoreCase)) ||
        (!string.IsNullOrWhiteSpace(left.SerialNumber) && left.VendorId == right.VendorId && left.ProductId == right.ProductId &&
         string.Equals(left.SerialNumber, right.SerialNumber, StringComparison.OrdinalIgnoreCase));
}
