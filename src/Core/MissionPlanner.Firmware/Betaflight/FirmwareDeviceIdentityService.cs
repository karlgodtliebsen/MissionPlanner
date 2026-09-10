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
        using (var deadline = new CancellationTokenSource(options?.Value.DiscoveryTimeout ?? TimeSpan.FromSeconds(8), clock))
        using (var linked = CancellationTokenSource.CreateLinkedTokenSource(deadline.Token, cancellationToken))
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
                    if (deadline.IsCancellationRequested)
                    {
                        break;
                    }
                    var device = result[index];
                    if (connection.OwnsSerialPort(device.PortName))
                    {
                        result[index] = device with { BetaflightProbeOutcome = BetaflightProbeOutcome.PortBusy };
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
                    var observed = await probe.ProbeAsync(device.PortName, linked.Token).ConfigureAwait(false);
                    var enriched = device with { BetaflightProbeOutcome = observed.Outcome, BetaflightIdentity = observed.Identity };
                    if (observed.Outcome == BetaflightProbeOutcome.Success)
                    {
                        enriched = enriched with { RuntimeProbe = new(FirmwareRuntimeKind.Betaflight, FirmwareBootEnvironment.None, "runtime.betaflight") };
                    }
                    else if (observed.Outcome is BetaflightProbeOutcome.NotMsp or BetaflightProbeOutcome.Timeout
                        && !connection.OwnsSerialPort(device.PortName))
                    {
                        if (runtimeVerifier is not null && await runtimeVerifier.VerifyAsync(device, linked.Token).ConfigureAwait(false) is { } runtime)
                        {
                            enriched = enriched with { RuntimeProbe = new(FirmwareRuntimeKind.ArduPilot, FirmwareBootEnvironment.None, "runtime.ardupilot", runtime.IsArmed) };
                        }
                        else if (bootloaderDiscovery is not null && !connection.OwnsSerialPort(device.PortName))
                        {
                            try
                            {
                                await using var found = await bootloaderDiscovery.FindAsync(new(device, Timeout: TimeSpan.FromMilliseconds(600)),
                                    cancellationToken: linked.Token).ConfigureAwait(false);
                                enriched = enriched with { BootloaderIdentity = found.Identity,
                                    RuntimeProbe = new(FirmwareRuntimeKind.None, FirmwareBootEnvironment.ArduPilotBootloader, "runtime.ap-bootloader") };
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
                lease.Transition(new(FirmwareOperationState.Completed, null, "identity.discovery-completed"));
                return result;
            }
            catch (OperationCanceledException) when (deadline.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
            {
                lease.Transition(new(FirmwareOperationState.Completed, null, "identity.discovery-deadline"));
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
