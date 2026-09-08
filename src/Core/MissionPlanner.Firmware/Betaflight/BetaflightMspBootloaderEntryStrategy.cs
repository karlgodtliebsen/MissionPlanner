using System.Buffers.Binary;
using MissionPlanner.Firmware.Betaflight.Protocol;
using MissionPlanner.Firmware.Devices;
using MissionPlanner.Firmware.Entry;
using MissionPlanner.Firmware.Installation;

namespace MissionPlanner.Firmware.Betaflight;

/// <summary>Semantic reboot modes defined by Betaflight's MSP implementation.</summary>
public enum MspRebootMode : byte
{
    /// <summary>Restart the currently installed firmware.</summary>
    Firmware = 0,
    /// <summary>Enter the MCU factory ROM bootloader.</summary>
    RomBootloader = 1
}

/// <summary>Enters ROM DFU only after same-port live firmware, UID and armed-state validation.</summary>
public sealed class BetaflightMspBootloaderEntryStrategy(MspPortConnector connector, IBetaflightMspClient client,
    IFirmwareSerialDeviceCatalog devices, IFirmwareConnectionGateway connection, TimeProvider clock) : IBootloaderEntryStrategy
{
    /// <inheritdoc />
    public int Priority => 50;
    /// <inheritdoc />
    public BootloaderEntryTarget Target => BootloaderEntryTarget.Stm32RomDfu;

    /// <inheritdoc />
    public async Task<BootloaderEntryResult> TryEnterAsync(BootloaderEntryContext context, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var selected = context.ApplicationDevice ?? context.DiscoveryRequest.SelectedDevice;
        if (context.Target != Target || selected?.BetaflightIdentity is not { FirmwareVariant: "BTFL" } identity)
        {
            return new(BootloaderEntryOutcome.NotApplicable, "betaflight.not-proven");
        }
        if (context.HasActiveMissionPlannerSession || connection.IsVehicleConnected)
        {
            return Failed("port-busy");
        }
        if (identity.McuUniqueId is null || selected.StableIdentity is null ||
            !(identity.McuType?.StartsWith("STM32", StringComparison.OrdinalIgnoreCase) == true ||
              identity.Board?.TargetName?.StartsWith("STM32", StringComparison.OrdinalIgnoreCase) == true))
        {
            return Failed("identity-stale-or-stm32-unproven");
        }
        var snapshot = await devices.GetDevicesAsync(cancellationToken).ConfigureAwait(false);
        if (snapshot.Count(device => device.PortName == selected.PortName && device.StableIdentity == selected.StableIdentity
            && device.UsbSerialNumber == selected.UsbSerialNumber && device.ArrivedAt == selected.ArrivedAt) != 1)
        {
            return Failed("identity-stale");
        }
        MspResponse reboot;
        await using (var owned = await connector.OpenAsync(selected.PortName, cancellationToken).ConfigureAwait(false))
        {
            if (owned.Port is not { } port)
            {
                return Failed(owned.Failure.ToString());
            }
            async Task<MspResponse> Query(ushort command)
            {
                var response = await client.RequestAsync(port, command, ReadOnlyMemory<byte>.Empty,
                    TimeSpan.FromMilliseconds(500), cancellationToken).ConfigureAwait(false);
                cancellationToken.ThrowIfCancellationRequested();
                return response;
            }
            var variant = await Query(MspCommand.FcVariant).ConfigureAwait(false);
            var uid = await Query(MspCommand.Uid).ConfigureAwait(false);
            if (variant.Failure != MspFailure.None || !variant.Frame!.Payload.AsSpan().SequenceEqual("BTFL"u8) ||
                uid.Failure != MspFailure.None || uid.Frame!.Payload.Length != 12 || Convert.ToHexString(uid.Frame.Payload) != identity.McuUniqueId)
            {
                return Failed("identity-stale");
            }
            var boxes = await Query(MspCommand.BoxIds).ConfigureAwait(false);
            var status = await Query(MspCommand.Status).ConfigureAwait(false);
            var armIndex = boxes.Frame is null ? -1 : Array.IndexOf(boxes.Frame.Payload, (byte)0);
            if (boxes.Failure != MspFailure.None || status.Failure != MspFailure.None || armIndex is < 0 or >= 32 || status.Frame!.Payload.Length < 10)
            {
                return Failed("armed-state-unknown");
            }
            if ((BinaryPrimitives.ReadUInt32LittleEndian(status.Frame.Payload.AsSpan(6)) & (1u << armIndex)) != 0)
            {
                return Failed("armed");
            }
            cancellationToken.ThrowIfCancellationRequested();
            if (connection.IsVehicleConnected)
            {
                return Failed("port-busy");
            }
            reboot = await client.RequestAsync(port, MspCommand.Reboot, new byte[] { (byte)MspRebootMode.RomBootloader },
                TimeSpan.FromMilliseconds(500), cancellationToken).ConfigureAwait(false);
        }
        cancellationToken.ThrowIfCancellationRequested();
        if (reboot.Failure == MspFailure.None && reboot.Frame?.Payload is [1])
        {
            return new(BootloaderEntryOutcome.DfuRebootInitiated, "betaflight.reboot-accepted");
        }
        if (!reboot.RequestWritten || reboot.Failure is not (MspFailure.Disconnected or MspFailure.Timeout))
        {
            return Failed(reboot.RequestWritten ? "msp-rejected" : "timeout-before-write");
        }
        var started = clock.GetTimestamp();
        while (clock.GetElapsedTime(started) < TimeSpan.FromSeconds(3))
        {
            var current = await devices.GetDevicesAsync(cancellationToken).ConfigureAwait(false);
            if (!current.Any(device => device.StableIdentity == selected.StableIdentity))
            {
                return new(BootloaderEntryOutcome.DfuRebootInitiated, "betaflight.serial-disappeared-after-request");
            }
            await Task.Delay(TimeSpan.FromMilliseconds(200), clock, cancellationToken).ConfigureAwait(false);
        }
        return Failed("serial-did-not-disappear");
    }

    private static BootloaderEntryResult Failed(string code) => new(BootloaderEntryOutcome.Failed, "betaflight." + code);
}
