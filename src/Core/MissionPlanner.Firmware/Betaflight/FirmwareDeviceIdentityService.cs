using MissionPlanner.Firmware.Installation;
using MissionPlanner.Firmware.Exceptions;
using MissionPlanner.Firmware.Model;
using MissionPlanner.Firmware.Operations;
using MissionPlanner.Firmware.Workflow;
using MissionPlanner.Firmware.Discovery;

namespace MissionPlanner.Firmware.Betaflight;

/// <summary>Enriches snapshots with expiring, presence-bound identity evidence.</summary>
public sealed class FirmwareDeviceIdentityService(IBetaflightDeviceProbe probe, IFirmwareConnectionGateway connection,
    IFirmwareOperationCoordinator operations, TimeProvider clock,
    Microsoft.Extensions.Options.IOptions<BetaflightOptions>? options = null,
    IArduPilotRuntimeVerifier? runtimeVerifier = null, IBootloaderDiscoveryService? bootloaderDiscovery = null) : IFirmwareDeviceIdentityService
{
    private readonly object sync = new();
    private readonly Dictionary<string, (DateTimeOffset At, SerialDeviceDescriptor Device)> cache = [];
    private long generation;

    /// <inheritdoc />
    public void Invalidate()
    {
        lock (sync)
        {
            generation++;
            cache.Clear();
        }
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<SerialDeviceDescriptor>> EnrichAsync(IReadOnlyList<SerialDeviceDescriptor> devices,
        bool forceRefresh = false, CancellationToken cancellationToken = default)
    {
        if (forceRefresh)
        {
            Invalidate();
        }
        long version;
        lock (sync)
        {
            version = generation;
            var present = devices.Select(Key).Where(key => key is not null).ToHashSet();
            foreach (var key in cache.Keys.Where(key => !present.Contains(key)).ToArray())
            {
                cache.Remove(key);
            }
        }
        if (!OperatingSystem.IsWindows())
        {
            return devices;
        }
        IFirmwareOperationSession lease;
        try
        {
            lease = operations.Begin(FirmwareOperationKind.ProbeFirmwareIdentity);
        }
        catch (FirmwareBusyException)
        {
            return devices;
        }
        using (lease)
        {
            var result = devices.Select(device => device with
            {
                BetaflightIdentity = null,
                BetaflightProbeOutcome = null,
                BootloaderIdentity = null,
                RuntimeProbe = new(FirmwareRuntimeKind.Unknown, FirmwareBootEnvironment.None, "runtime.not-probed")
            }).ToArray();
            try
            {
                for (var index = 0; index < result.Length; index++)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    using var deadline = new CancellationTokenSource(options?.Value.DiscoveryTimeout ?? TimeSpan.FromSeconds(12), clock);
                    using var linked = CancellationTokenSource.CreateLinkedTokenSource(deadline.Token, cancellationToken);
                    var device = result[index];
                    try
                    {
                        if (connection.OwnsSerialPort(device.PortName))
                        {
                            // Step 1 — an existing Mission Planner vehicle session owns this exact port.
                            // Reuse its authoritative autopilot identity instead of reopening or stealing
                            // the port. A generic/other autopilot leaves the runtime unresolved.
                            var owned = connection.IdentifyOwnedSerialRuntime(device.PortName);
                            result[index] = owned == FirmwareRuntimeKind.ArduPilot
                                ? device with
                                {
                                    BetaflightProbeOutcome = BetaflightProbeOutcome.PortBusy,
                                    RuntimeProbe = new(FirmwareRuntimeKind.ArduPilot, FirmwareBootEnvironment.None, "runtime.ardupilot.existing-session")
                                    {
                                        Evidence = FirmwareRuntimeEvidence.ExistingVehicleSession,
                                        Verification = FirmwareRuntimeVerification.Verified,
                                        Outcome = FirmwareRuntimeProbeOutcome.Success
                                    }
                                }
                                : device with
                                {
                                    BetaflightProbeOutcome = BetaflightProbeOutcome.PortBusy,
                                    RuntimeProbe = new(FirmwareRuntimeKind.Unknown, FirmwareBootEnvironment.None, "runtime.port-owned")
                                    { Outcome = FirmwareRuntimeProbeOutcome.PortBusy }
                                };
                            continue;
                        }
                        var key = Key(device);
                        lock (sync)
                        {
                            if (key is not null && cache.TryGetValue(key, out var known) && clock.GetUtcNow() - known.At < (options?.Value.CacheDuration ?? TimeSpan.FromSeconds(30)))
                            {
                                result[index] = device with
                                {
                                    BetaflightIdentity = known.Device.BetaflightIdentity is { } identity ? identity with { PortName = device.PortName } : null,
                                    BetaflightProbeOutcome = known.Device.BetaflightProbeOutcome,
                                    BootloaderIdentity = known.Device.BootloaderIdentity, RuntimeProbe = known.Device.RuntimeProbe
                                };
                                continue;
                            }
                        }
                        // MAVLink must run before MSP: a failed MSP exchange must not hide an
                        // ArduPilot application, and no two protocols may own the port together.
                        var runtimeResult = runtimeVerifier is null ? null
                            : await runtimeVerifier.ProbeAsync(device, linked.Token).ConfigureAwait(false);
                        if (runtimeResult is { Runtime: FirmwareRuntimeKind.ArduPilot }
                            || runtimeResult is { Outcome: FirmwareRuntimeProbeOutcome.PortBusy })
                        {
                            cancellationToken.ThrowIfCancellationRequested();
                            lock (sync)
                            {
                                if (version != generation || connection.OwnsSerialPort(device.PortName))
                                {
                                    break;
                                }
                                result[index] = device with { RuntimeProbe = runtimeResult };
                                if (key is not null)
                                {
                                    cache[key] = (clock.GetUtcNow(), result[index]);
                                }
                            }
                            continue;
                        }
                        if (connection.OwnsSerialPort(device.PortName))
                        {
                            continue;
                        }
                        var observed = await probe.ProbeAsync(device.PortName, linked.Token).ConfigureAwait(false);
                        var enriched = device with
                        {
                            BetaflightProbeOutcome = observed.Outcome,
                            BetaflightIdentity = observed.Identity,
                            RuntimeProbe = (runtimeResult ?? device.RuntimeProbe!) with
                            {
                                Code = $"{runtimeResult?.Code ?? "runtime.unavailable"}; msp.{observed.Outcome}",
                                Outcome = observed.Outcome == BetaflightProbeOutcome.PortBusy
                                    ? FirmwareRuntimeProbeOutcome.PortBusy : runtimeResult?.Outcome ?? FirmwareRuntimeProbeOutcome.NotBetaflight
                            }
                        };
                        if (observed.Outcome == BetaflightProbeOutcome.Success)
                        {
                            enriched = enriched with { RuntimeProbe = new(FirmwareRuntimeKind.Betaflight, FirmwareBootEnvironment.None, "runtime.betaflight")
                            {
                                Evidence = FirmwareRuntimeEvidence.MspProbe,
                                Verification = FirmwareRuntimeVerification.Verified,
                                Outcome = FirmwareRuntimeProbeOutcome.Success,
                                ProtocolDetail = observed.Identity?.Board?.TargetName
                            } };
                        }
                        else if (observed.Outcome is BetaflightProbeOutcome.NotMsp or BetaflightProbeOutcome.Timeout
                            && !connection.OwnsSerialPort(device.PortName))
                        {
                            if (bootloaderDiscovery is not null && !connection.OwnsSerialPort(device.PortName))
                            {
                                try
                                {
                                    await using var found = await bootloaderDiscovery.FindAsync(new(device, Timeout: TimeSpan.FromMilliseconds(600)),
                                        cancellationToken: linked.Token).ConfigureAwait(false);
                                    enriched = enriched with { BootloaderIdentity = found.Identity,
                                        RuntimeProbe = new(FirmwareRuntimeKind.None, FirmwareBootEnvironment.ArduPilotBootloader, "runtime.ap-bootloader")
                                        {
                                            Evidence = FirmwareRuntimeEvidence.ArduPilotBootloader,
                                            Verification = FirmwareRuntimeVerification.Verified,
                                            Outcome = FirmwareRuntimeProbeOutcome.Success
                                        } };
                                }
                                catch (FirmwareDeviceNotFoundException)
                                {
                                    // An expected negative protocol probe leaves runtime Unknown.
                                }
                            }
                        }
                        cancellationToken.ThrowIfCancellationRequested();
                        lock (sync)
                        {
                            if (version != generation || connection.OwnsSerialPort(device.PortName))
                            {
                                break;
                            }
                            result[index] = enriched;
                            if (key is not null && observed.Outcome != BetaflightProbeOutcome.Cancelled)
                            {
                                cache[key] = (clock.GetUtcNow(), enriched);
                            }
                        }
                    }
                    catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
                    {
                        result[index] = device with
                        {
                            RuntimeProbe = new(FirmwareRuntimeKind.Unknown, FirmwareBootEnvironment.None, "runtime.discovery-timeout")
                            { Outcome = FirmwareRuntimeProbeOutcome.Timeout }
                        };
                    }
                }
                lease.Transition(new(FirmwareOperationState.Completed, null, "identity.discovery-completed"));
                return result;
            }
            finally
            {
                if (lease.State != FirmwareOperationState.Completed)
                {
                    lease.RequestCancellation("identity.discovery-stopped");
                }
            }
        }
    }

    private static string? Key(SerialDeviceDescriptor device) => device.StableIdentity is null ? null
        : $"{device.StableIdentity}|{device.UsbIdentifier}|{device.UsbSerialNumber}|{device.ProductName}|{device.ArrivedAt.UtcTicks}";
}
