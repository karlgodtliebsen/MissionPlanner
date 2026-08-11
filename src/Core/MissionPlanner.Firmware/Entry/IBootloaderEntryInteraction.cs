namespace MissionPlanner.Firmware.Entry;

/// <summary>Publishes a host-presented manual bootloader interaction.</summary>
public interface IBootloaderEntryInteraction
{
    /// <summary>Requests unplug/replug or hardware reset without embedding UI text.</summary>
    /// <returns><see langword="true"/> when the operator accepts the request; otherwise, <see langword="false"/>.</returns>
    Task<bool> RequestAsync(string interactionCode, CancellationToken cancellationToken = default);
}
