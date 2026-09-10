using MissionPlanner.Firmware.Devices;
using MissionPlanner.Firmware.Dfu;
using MissionPlanner.Firmware.Installation;
using MissionPlanner.Firmware.Model;
using MissionPlanner.Firmware.Operations;

namespace MissionPlanner.Firmware.Betaflight;

/// <summary>Runs the conversion through existing installation services and requires runtime proof for success.</summary>
public sealed class BetaflightToArduPilotConversionService(IBetaflightDeviceProbe probe,
    IBetaflightArduPilotCompatibilityProvider compatibility, IBetaflightDfuHandoff handoff,
    IDfuArtifactResolver artifacts, IDfuInstallationService installer, IFirmwareSerialDeviceCatalog serial,
    IDfuDeviceCatalog dfu, IUsbTopologyProvider topology, IArduPilotRuntimeVerifier verifier,
    IFirmwareConnectionGateway connection, IFirmwareDeviceIdentityService identities,
    IFirmwareOperationCoordinator operations, TimeProvider clock) : IBetaflightToArduPilotConversionService
{
    /// <inheritdoc />
    public async Task<BetaflightConversionResult> ConvertAsync(BetaflightConversionRequest request,
        IProgress<DfuProgress>? progress = null, CancellationToken cancellationToken = default)
    {
        var result = new BetaflightConversionResult(false, "betaflight.identifying-source", request.Source, request.Firmware);
        try
        {
            if (!request.ConfigurationBackupConfirmed || !request.PropellersRemovedConfirmed || connection.OwnsSerialPort(request.Source.PortName))
            {
                return result with { Code = "betaflight.safety-confirmation-required" };
            }
            if (request.Source.BetaflightIdentity is not { } expected || expected.McuUniqueId is null)
            {
                return result with { Code = "betaflight.source-unproven" };
            }
            var mapping = compatibility.Resolve(expected);
            if (mapping is null || mapping.ArduPilotPlatform != request.Firmware.Target.Platform || mapping.ArduPilotBoardId != request.Firmware.Target.BoardId)
            {
                return result with { Code = "betaflight.exact-compatible-target-required" };
            }
            using (var lease = operations.Begin(FirmwareOperationKind.ProbeFirmwareIdentity))
            {
                try
                {
                    var fresh = await probe.ProbeAsync(request.Source.PortName, cancellationToken).ConfigureAwait(false);
                    if (fresh.Outcome != BetaflightProbeOutcome.Success || fresh.Identity?.McuUniqueId != expected.McuUniqueId
                        || compatibility.Resolve(fresh.Identity) != mapping)
                    {
                        return result with { Code = "betaflight.source-changed" };
                    }
                    lease.Transition(new(FirmwareOperationState.Completed, null, "betaflight.source-revalidated"));
                }
                finally
                {
                    if (lease.State != FirmwareOperationState.Completed)
                    {
                        lease.RequestCancellation();
                    }
                }
            }
            result = result with { Code = "betaflight.entering-dfu" };
            progress?.Report(new(DfuOperationState.WaitingForDevice, result.Code));
            var transition = await handoff.RebootAsync(request.Source, cancellationToken: cancellationToken).ConfigureAwait(false);
            result = result with { Handoff = transition };
            if (!transition.Succeeded || transition.Device is null || transition.PhysicalLocation is null)
            {
                return result with { Code = transition.Code };
            }
            var installRequest = new DfuInstallationRequest(mapping.ArduPilotPlatform, mapping.ArduPilotBoardId,
                transition.Device, ConfirmationPhrase: $"FLASH {mapping.ArduPilotPlatform}", ManifestEntry: request.Firmware,
                PreviousApplicationDevice: request.Source);
            result = result with { Code = "betaflight.resolving-artifact" };
            progress?.Report(new(DfuOperationState.ResolvingArtifact, result.Code));
            var artifact = await artifacts.ResolveAsync(installRequest, cancellationToken).ConfigureAwait(false);
            result = result with { Artifact = artifact };
            if (artifact.Platform != mapping.ArduPilotPlatform || artifact.BoardId != mapping.ArduPilotBoardId)
            {
                return result with { Code = "betaflight.artifact-target-mismatch" };
            }
            var currentDfu = await dfu.GetDevicesAsync(cancellationToken).ConfigureAwait(false);
            if (currentDfu.Count(device => device.ProviderId == transition.Device.ProviderId && device.ArrivedAt == transition.Device.ArrivedAt) != 1
                || !string.Equals(await topology.GetLocationAsync(transition.Device.PnpInstanceId ?? transition.Device.ProviderId, cancellationToken).ConfigureAwait(false),
                    transition.PhysicalLocation, StringComparison.OrdinalIgnoreCase))
            {
                return result with { Code = "betaflight.dfu-changed" };
            }
            cancellationToken.ThrowIfCancellationRequested();
            result = result with { Code = "betaflight.programming" };
            identities.Invalidate();
            var programmed = await installer.InstallAsync(installRequest with { Artifact = artifact }, progress, cancellationToken).ConfigureAwait(false);
            result = result with { Programming = programmed };
            if (!programmed.ProgrammingSucceeded || !programmed.VerificationSucceeded)
            {
                return result with { Code = programmed.Failure?.Code ?? "betaflight.programming-not-verified" };
            }
            result = result with { Code = "betaflight.waiting-for-ardupilot" };
            progress?.Report(new(DfuOperationState.WaitingForApplication, result.Code));
            using var verifyLease = operations.Begin(FirmwareOperationKind.ProbeFirmwareIdentity);
            try
            {
                using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(45), clock);
                using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, deadline.Token);
                while (!linked.IsCancellationRequested)
                {
                    var candidates = new List<SerialDeviceDescriptor>();
                    foreach (var device in await serial.GetDevicesAsync(linked.Token).ConfigureAwait(false))
                    {
                        if (string.Equals(await topology.GetLocationAsync(device.OsDeviceId, linked.Token).ConfigureAwait(false),
                            transition.PhysicalLocation, StringComparison.OrdinalIgnoreCase))
                        {
                            candidates.Add(device);
                        }
                    }
                    if (candidates.Count > 1)
                    {
                        return result with { Code = "betaflight.returned-serial-ambiguous" };
                    }
                    if (candidates.Count == 1)
                    {
                        if (connection.OwnsSerialPort(candidates[0].PortName))
                        {
                            return result with { Code = "betaflight.runtime-port-owned" };
                        }
                        var runtime = await verifier.VerifyAsync(candidates[0], linked.Token).ConfigureAwait(false);
                        if (runtime is not null)
                        {
                            verifyLease.Transition(new(FirmwareOperationState.Completed, null, "betaflight.ardupilot-verified"));
                            return result with { Succeeded = true, Code = "betaflight.ardupilot-verified", ReturnedDevice = candidates[0], Runtime = runtime };
                        }
                    }
                    await Task.Delay(TimeSpan.FromMilliseconds(500), clock, linked.Token).ConfigureAwait(false);
                }
                return result with { Code = "betaflight.flash-verified-runtime-unverified" };
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                return result with { Code = "betaflight.flash-verified-runtime-unverified" };
            }
            finally
            {
                if (verifyLease.State != FirmwareOperationState.Completed)
                {
                    verifyLease.RequestCancellation();
                }
            }
        }
        catch (OperationCanceledException)
        {
            return result with { Code = "betaflight.cancelled" };
        }
        catch (Exception exception)
        {
            return result with { Code = result.Code + ".failed:" + exception.GetType().Name };
        }
        finally
        {
            identities.Invalidate();
        }
    }
}
