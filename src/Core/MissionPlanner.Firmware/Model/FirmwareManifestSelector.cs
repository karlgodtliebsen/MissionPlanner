namespace MissionPlanner.Firmware.Model;

/// <summary>Matches firmware using family and exact vendor/product/board identifiers.</summary>
public sealed class FirmwareManifestSelector : IFirmwareManifestSelector
{
    /// <inheritdoc />
    public IReadOnlyList<FirmwareManifestEntryRecord> Select(IReadOnlyList<FirmwareManifestEntryRecord> releases, VehicleFirmwareIdentity identity, FirmwareReleaseChannel channel)
    {
        return identity.VendorId == 0 || identity.ProductId == 0
            ? []
            : (IReadOnlyList<FirmwareManifestEntryRecord>)releases
                .Where(release =>
                    release.Family == identity.Family &&
                    release.Channel == channel &&
                    release.VendorId == identity.VendorId &&
                    release.ProductId == identity.ProductId &&
                    (release.BoardVersion == 0 || release.BoardVersion == identity.BoardVersion) &&
                    !string.IsNullOrWhiteSpace(release.BoardTarget))
                .OrderByDescending(release => release.PublishedAt)
                .ToArray();
    }
}
