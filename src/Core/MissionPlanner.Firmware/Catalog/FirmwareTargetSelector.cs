using MissionPlanner.Firmware.Model;

namespace MissionPlanner.Firmware.Catalog;

/// <summary>Filters and ranks hardware targets without choosing ambiguous results.</summary>
public static class FirmwareTargetSelector
{
    /// <summary>Returns matching targets ordered by evidence and stable target identity.</summary>
    public static IReadOnlyList<FirmwareTargetRecommendation> Query(
        IEnumerable<FirmwareManifestEntry> entries,
        FirmwareTargetQuery query,
        IReadOnlyCollection<SerialDeviceDescriptor>? devices = null,
        int? previouslySelectedBoardId = null)
    {
        ArgumentNullException.ThrowIfNull(entries);
        ArgumentNullException.ThrowIfNull(query);
        devices ??= [];
        return entries.Where(entry => Matches(entry, query))
            .Select(entry => Recommend(entry, devices, previouslySelectedBoardId))
            .OrderByDescending(item => item.Confidence)
            .ThenByDescending(item => item.Reason)
            .ThenBy(item => item.Entry.Target.VehicleType)
            .ThenBy(item => item.Entry.Target.Platform, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    /// <summary>Returns the sole high-confidence recommendation, or <see langword="null"/> when evidence is ambiguous.</summary>
    public static FirmwareTargetRecommendation? UnambiguousHighConfidence(IEnumerable<FirmwareTargetRecommendation> recommendations)
    {
        var high = recommendations.Where(item => item.Confidence == FirmwareTargetConfidence.High).ToArray();
        return high.Length == 1 ? high[0] : null;
    }

    private static FirmwareTargetRecommendation Recommend(FirmwareManifestEntry entry, IReadOnlyCollection<SerialDeviceDescriptor> devices, int? previousBoardId)
    {
        if (devices.Any(device => device.UsbIdentifier is { } usb && entry.Target.UsbIdentifiers.Contains(usb)))
        {
            return new FirmwareTargetRecommendation(entry, FirmwareTargetMatchReason.ExactUsbMatch, FirmwareTargetConfidence.High);
        }

        if (devices.Any(device => entry.Target.BootloaderNames.Any(alias =>
                (!string.IsNullOrWhiteSpace(device.ProductName) && device.ProductName.Contains(alias, StringComparison.OrdinalIgnoreCase)) ||
                device.BoardHints.Any(hint => hint.Contains(alias, StringComparison.OrdinalIgnoreCase)))))
        {
            return new FirmwareTargetRecommendation(entry, FirmwareTargetMatchReason.ExactBootloaderAliasMatch, FirmwareTargetConfidence.High);
        }

        return previousBoardId == entry.Target.BoardId
            ? new FirmwareTargetRecommendation(entry, FirmwareTargetMatchReason.PreviouslySelectedTarget, FirmwareTargetConfidence.Medium)
            : new FirmwareTargetRecommendation(entry, FirmwareTargetMatchReason.ManualSelection, FirmwareTargetConfidence.Low);
    }

    private static bool Matches(FirmwareManifestEntry entry, FirmwareTargetQuery query)
    {
        var manufacturer = Metadata(entry, "manufacturer") ?? Metadata(entry, "brand") ?? string.Empty;
        var search = query.SearchText?.Trim();
        return (query.VehicleFamily is null || entry.Target.VehicleType == query.VehicleFamily) &&
               (query.ReleaseChannel is null || entry.Channel == query.ReleaseChannel) &&
               Contains(entry.Target.Platform, query.Platform) && Contains(manufacturer, query.Manufacturer) &&
               (query.BoardId is null || entry.Target.BoardId == query.BoardId) &&
               (string.IsNullOrWhiteSpace(query.Bootloader) || entry.Target.BootloaderNames.Any(value => Contains(value, query.Bootloader))) &&
               (query.UsbIdentifier is null || entry.Target.UsbIdentifiers.Contains(query.UsbIdentifier.Value)) &&
               Contains(entry.Version.Value, query.Version) && Contains(entry.GitSha ?? string.Empty, query.GitSha) &&
               (string.IsNullOrWhiteSpace(search) || Contains(entry.Target.Platform, search) || Contains(manufacturer, search) ||
                entry.Target.BoardId.ToString(System.Globalization.CultureInfo.InvariantCulture).Contains(search, StringComparison.OrdinalIgnoreCase));
    }

    private static string? Metadata(FirmwareManifestEntry entry, string key)
    {
        return entry.RawMetadata.FirstOrDefault(pair => pair.Key.Equals(key, StringComparison.OrdinalIgnoreCase)).Value;
    }

    private static bool Contains(string value, string? filter)
    {
        return string.IsNullOrWhiteSpace(filter) || value.Contains(filter.Trim(), StringComparison.OrdinalIgnoreCase);
    }
}
