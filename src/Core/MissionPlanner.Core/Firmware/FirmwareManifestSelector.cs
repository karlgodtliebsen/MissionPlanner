using MissionPlanner.Core.Vehicles.Models;

namespace MissionPlanner.Core.Firmware;

/// <summary>Matches firmware using family and exact vendor/product/board identifiers.</summary>
public sealed class FirmwareManifestSelector : IFirmwareManifestSelector
{
    /// <inheritdoc />
    public IReadOnlyList<FirmwareManifestEntry> Select(
        IReadOnlyList<FirmwareManifestEntry> releases,
        VehicleFirmwareIdentity identity,
        FirmwareReleaseChannel channel)
    {
        if (identity.VendorId == 0 || identity.ProductId == 0)
        {
            return [];
        }

        return releases
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
