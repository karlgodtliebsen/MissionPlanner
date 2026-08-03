using MissionPlanner.Firmware.Model;

namespace MissionPlanner.Firmware.Entry;

/// <summary>Attempts one method of entering or locating a bootloader.</summary>
public interface IBootloaderEntryStrategy
{
    /// <summary>Gets ascending execution priority.</summary>
    int Priority { get; }
    /// <summary>Attempts the strategy without retaining temporary serial ownership.</summary>
    Task<BootloaderEntryResult> TryEnterAsync(BootloaderEntryContext context, CancellationToken cancellationToken = default);
}

/// <summary>Creates and disposes an isolated temporary MAVLink channel for one reboot request.</summary>
public interface ITemporaryMavLinkBootloaderGateway
{
    /// <summary>Detects a heartbeat, requests bootloader reboot, observes acknowledgement when available, and disposes the channel.</summary>
    Task<bool> RebootToBootloaderAsync(SerialDeviceDescriptor applicationDevice, CancellationToken cancellationToken = default);
}

/// <summary>Publishes a host-presented manual bootloader interaction.</summary>
public interface IBootloaderEntryInteraction
{
    /// <summary>Requests unplug/replug or hardware reset without embedding UI text.</summary>
    Task RequestAsync(string interactionCode, CancellationToken cancellationToken = default);
}

/// <summary>Runs bootloader-entry strategies in deterministic priority order.</summary>
public interface IBootloaderEntryService
{
    /// <summary>Runs applicable strategies until discovery may proceed or a bootloader is identified.</summary>
    Task<BootloaderEntryResult> EnterAsync(BootloaderEntryContext context, CancellationToken cancellationToken = default);
}
