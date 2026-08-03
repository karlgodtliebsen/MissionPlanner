using System.Runtime.CompilerServices;
using MissionPlanner.Firmware.Model;

namespace MissionPlanner.Firmware.Devices;

/// <summary>Provides cancellable, deduplicated device monitoring through bounded polling.</summary>
public sealed class PollingFirmwareDeviceMonitor(
    IFirmwareSerialDeviceCatalog catalog,
    TimeProvider timeProvider,
    TimeSpan? interval = null) : IFirmwareDeviceMonitor
{
    private readonly TimeSpan pollInterval = interval ?? TimeSpan.FromMilliseconds(250);

    /// <inheritdoc />
    public async IAsyncEnumerable<FirmwareDeviceChange> WatchAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var previous = Index(await catalog.GetDevicesAsync(cancellationToken).ConfigureAwait(false));
        while (true)
        {
            await Task.Delay(pollInterval, timeProvider, cancellationToken).ConfigureAwait(false);
            var current = Index(await catalog.GetDevicesAsync(cancellationToken).ConfigureAwait(false));
            var now = timeProvider.GetUtcNow();
            foreach (var pair in previous.Where(pair => !current.ContainsKey(pair.Key)).OrderBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase))
                yield return new FirmwareDeviceChange(FirmwareDeviceChangeKind.Removed, pair.Value, now);
            foreach (var pair in current.Where(pair => !previous.ContainsKey(pair.Key)).OrderBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase))
                yield return new FirmwareDeviceChange(FirmwareDeviceChangeKind.Arrived, pair.Value, now);
            previous = current;
        }
    }

    private static Dictionary<string, SerialDeviceDescriptor> Index(IEnumerable<SerialDeviceDescriptor> devices) =>
        devices.GroupBy(DeviceKey, StringComparer.OrdinalIgnoreCase).ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);

    private static string DeviceKey(SerialDeviceDescriptor device) => device.StableIdentity ?? $"transient:{device.PortName}";
}
