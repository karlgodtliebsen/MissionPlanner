using Microsoft.Extensions.Options;
using MissionPlanner.Firmware.Devices;
using MissionPlanner.Firmware.Dfu;
using MissionPlanner.Firmware.Entry;
using MissionPlanner.Firmware.Installation;
using MissionPlanner.Firmware.Model;
using MissionPlanner.Firmware.Operations;

namespace MissionPlanner.Firmware.Betaflight;

/// <summary>Reuses existing serial snapshots and USB DFU monitoring for the transition.</summary>
public sealed class BetaflightDfuHandoff(IBootloaderEntryService entry, IDfuDeviceCatalog dfu,
    IDfuDeviceMonitor monitor, IFirmwareSerialDeviceCatalog serial, IUsbTopologyProvider topology,
    IFirmwareOperationCoordinator operations, IFirmwareConnectionGateway connection, IOptions<DfuOptions> options,
    TimeProvider clock) : IBetaflightDfuHandoff
{
    /// <inheritdoc />
    public async Task<BetaflightDfuHandoffResult> RebootAsync(SerialDeviceDescriptor source,
        IProgress<FirmwareProgress>? progress = null, CancellationToken cancellationToken = default)
    {
        if (connection.IsVehicleConnected || source.BetaflightIdentity is null)
        {
            return new(false, "betaflight.disconnected-identity-required", source);
        }
        using var lease = operations.Begin(FirmwareOperationKind.ProbeFirmwareIdentity);
        try
        {
            var location = await topology.GetLocationAsync(source.OsDeviceId, cancellationToken).ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(location))
            {
                return new(false, "betaflight.physical-location-unavailable", source);
            }
            var before = await dfu.GetDevicesAsync(cancellationToken).ConfigureAwait(false);
            var existing = before.Select(device => device.ProviderId).ToHashSet(StringComparer.OrdinalIgnoreCase);
            progress?.Report(new(FirmwareOperationState.RequestingBootloaderReboot, null, "betaflight.reboot-requested"));
            var entered = await entry.EnterAsync(new(new(source), source)
            {
                Target = BootloaderEntryTarget.Stm32RomDfu
            }, cancellationToken).ConfigureAwait(false);
            if (entered.Outcome != BootloaderEntryOutcome.DfuRebootInitiated)
            {
                return new(false, entered.Code, source);
            }
            using var deadline = new CancellationTokenSource(options.Value.DfuDisappearanceTimeout, clock);
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, deadline.Token);
            progress?.Report(new(FirmwareOperationState.WaitingForBootloader, null, "betaflight.waiting-for-correlated-dfu"));
            try
            {
                await foreach (var snapshot in monitor.WatchAsync(linked.Token).ConfigureAwait(false))
                {
                    var candidates = snapshot.Where(device => device.VendorId == 0x0483 && device.ProductId == 0xdf11
                        && !existing.Contains(device.ProviderId)).ToArray();
                    var matches = new List<DfuDeviceDescriptor>();
                    foreach (var candidate in candidates)
                    {
                        var candidateLocation = await topology.GetLocationAsync(candidate.PnpInstanceId ?? candidate.ProviderId, linked.Token).ConfigureAwait(false);
                        if (string.Equals(candidateLocation, location, StringComparison.OrdinalIgnoreCase))
                        {
                            matches.Add(candidate);
                        }
                    }
                    if (matches.Count > 1)
                    {
                        return new(false, "betaflight.dfu-ambiguous", source, PhysicalLocation: location);
                    }
                    if (matches.Count != 1)
                    {
                        continue;
                    }
                    while ((await serial.GetDevicesAsync(linked.Token).ConfigureAwait(false))
                        .Any(device => device.StableIdentity == source.StableIdentity || device.PortName == source.PortName))
                    {
                        await Task.Delay(options.Value.DevicePollInterval, clock, linked.Token).ConfigureAwait(false);
                    }
                    var current = await dfu.GetDevicesAsync(linked.Token).ConfigureAwait(false);
                    var currentMatches = 0;
                    foreach (var device in current.Where(device => !existing.Contains(device.ProviderId)))
                    {
                        var currentLocation = await topology.GetLocationAsync(device.PnpInstanceId ?? device.ProviderId, linked.Token).ConfigureAwait(false);
                        if (string.Equals(currentLocation, location, StringComparison.OrdinalIgnoreCase))
                        {
                            currentMatches++;
                        }
                    }
                    if (currentMatches > 1)
                    {
                        return new(false, "betaflight.dfu-ambiguous", source, PhysicalLocation: location);
                    }
                    if (!current.Any(device => device.ProviderId == matches[0].ProviderId && device.ArrivedAt == matches[0].ArrivedAt))
                    {
                        return new(false, "betaflight.dfu-removed", source, PhysicalLocation: location);
                    }
                    lease.Transition(new(FirmwareOperationState.Completed, null, "betaflight.dfu-correlated"));
                    return new(true, "betaflight.dfu-correlated", source, matches[0], location);
                }
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                return new(false, "betaflight.dfu-correlation-timeout", source, PhysicalLocation: location);
            }
            return new(false, "betaflight.dfu-not-found", source, PhysicalLocation: location);
        }
        finally
        {
            if (lease.State != FirmwareOperationState.Completed)
            {
                lease.RequestCancellation("betaflight.handoff-stopped");
            }
        }
    }
}
