using MissionPlanner.Firmware.Installation;
using MissionPlanner.Firmware.Exceptions;
using MissionPlanner.Firmware.Model;
using MissionPlanner.Firmware.Operations;

namespace MissionPlanner.Firmware.Betaflight;

/// <summary>Enriches snapshots with expiring, presence-bound identity evidence.</summary>
public sealed class FirmwareDeviceIdentityService(IBetaflightDeviceProbe probe, IFirmwareConnectionGateway connection,
    IFirmwareOperationCoordinator operations, TimeProvider clock,
    Microsoft.Extensions.Options.IOptions<BetaflightOptions>? options = null) : IFirmwareDeviceIdentityService
{
    private readonly object sync = new();
    private readonly Dictionary<string, (DateTimeOffset At, BetaflightDeviceInfo? Identity, BetaflightProbeOutcome? Outcome)> cache = [];
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
        if (connection.IsVehicleConnected || !OperatingSystem.IsWindows())
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
            try
            {
                var result = devices.Select(device => device with { BetaflightIdentity = null, BetaflightProbeOutcome = null }).ToArray();
                for (var index = 0; index < result.Length; index++)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (deadline.IsCancellationRequested || connection.IsVehicleConnected)
                    {
                        break;
                    }
                    var device = result[index];
                    var key = Key(device);
                    lock (sync)
                    {
                        if (key is not null && cache.TryGetValue(key, out var known) && clock.GetUtcNow() - known.At < (options?.Value.CacheDuration ?? TimeSpan.FromSeconds(30)))
                        {
                            result[index] = device with { BetaflightIdentity = known.Identity is null ? null : known.Identity with { PortName = device.PortName }, BetaflightProbeOutcome = known.Outcome };
                            continue;
                        }
                    }
                    var observed = await probe.ProbeAsync(device.PortName, linked.Token).ConfigureAwait(false);
                    cancellationToken.ThrowIfCancellationRequested();
                    lock (sync)
                    {
                        if (version != generation || connection.IsVehicleConnected)
                        {
                            break;
                        }
                        result[index] = device with { BetaflightProbeOutcome = observed.Outcome };
                        if (observed.Outcome == BetaflightProbeOutcome.Success)
                        {
                            result[index] = result[index] with { BetaflightIdentity = observed.Identity };
                        }
                        if (key is not null && observed.Outcome != BetaflightProbeOutcome.Cancelled)
                        {
                            cache[key] = (clock.GetUtcNow(), result[index].BetaflightIdentity, observed.Outcome);
                        }
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
