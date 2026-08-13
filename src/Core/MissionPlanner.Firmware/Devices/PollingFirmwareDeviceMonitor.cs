using System.Runtime.CompilerServices;
using MissionPlanner.Firmware.Model;

namespace MissionPlanner.Firmware.Devices;

/// <summary>Provides cancellable, deduplicated device monitoring through bounded polling.</summary>
public sealed class PollingFirmwareDeviceMonitor(IFirmwareSerialDeviceCatalog catalog, TimeProvider timeProvider, TimeSpan? interval = null) : IFirmwareDeviceMonitor
{
    private readonly TimeSpan pollInterval = interval ?? TimeSpan.FromMilliseconds(250);

    /// <inheritdoc />
    public async IAsyncEnumerable<FirmwareDeviceChange> WatchAsync([EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var initial = await TryGetDevicesAsync(cancellationToken).ConfigureAwait(false);
        if (initial is null)
        {
            yield break;
        }

        var previous = Index(initial);
        while (!cancellationToken.IsCancellationRequested)
        {
            if (!await WaitForNextPollAsync(cancellationToken).ConfigureAwait(false))
            {
                yield break;
            }

            var snapshot = await TryGetDevicesAsync(cancellationToken).ConfigureAwait(false);
            if (snapshot is null)
            {
                yield break;
            }

            var current = Index(snapshot);
            var now = timeProvider.GetUtcNow();
            foreach (var pair in previous.Where(pair => !current.TryGetValue(pair.Key, out var replacement) || !SameDeviceMode(pair.Value, replacement)).OrderBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase))
            {
                yield return new FirmwareDeviceChange(FirmwareDeviceChangeKind.Removed, pair.Value, now);
            }

            foreach (var pair in current.Where(pair => !previous.TryGetValue(pair.Key, out var replaced) || !SameDeviceMode(replaced, pair.Value)).OrderBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase))
            {
                yield return new FirmwareDeviceChange(FirmwareDeviceChangeKind.Arrived, pair.Value, now);
            }

            previous = current;
        }
    }

    private async Task<bool> WaitForNextPollAsync(CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(pollInterval, timeProvider, cancellationToken).ConfigureAwait(false);
            return true;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return false;
        }
    }

    private async Task<IReadOnlyList<SerialDeviceDescriptor>?> TryGetDevicesAsync(CancellationToken cancellationToken)
    {
        try
        {
            return await catalog.GetDevicesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return null;
        }
    }

    private static Dictionary<string, SerialDeviceDescriptor> Index(IEnumerable<SerialDeviceDescriptor> devices)
    {
        return devices.GroupBy(DeviceKey, StringComparer.OrdinalIgnoreCase).ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);
    }

    private static string DeviceKey(SerialDeviceDescriptor device)
    {
        return device.StableIdentity ?? $"transient:{device.PortName}";
    }

    private static bool SameDeviceMode(SerialDeviceDescriptor left, SerialDeviceDescriptor right)
    {
        return string.Equals(left.PortName, right.PortName, StringComparison.OrdinalIgnoreCase) &&
               left.UsbIdentifier == right.UsbIdentifier &&
               string.Equals(left.ProductName, right.ProductName, StringComparison.OrdinalIgnoreCase) &&
               string.Equals(left.Manufacturer, right.Manufacturer, StringComparison.OrdinalIgnoreCase) &&
               left.BoardHints.SequenceEqual(right.BoardHints, StringComparer.OrdinalIgnoreCase);
    }
}
