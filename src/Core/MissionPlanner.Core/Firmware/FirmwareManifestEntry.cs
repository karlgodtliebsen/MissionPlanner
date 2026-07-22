using MissionPlanner.Core.Vehicles.Models;

namespace MissionPlanner.Core.Firmware;

/// <summary>Describes one cryptographically identified firmware package.</summary>
/// <param name="Family">The compatible firmware family.</param>
/// <param name="BoardTarget">The manifest-provided technical board target.</param>
/// <param name="VendorId">The exact USB/hardware vendor identifier.</param>
/// <param name="ProductId">The exact USB/hardware product identifier.</param>
/// <param name="BoardVersion">The exact board version, or zero when the manifest applies to every revision of the vendor/product pair.</param>
/// <param name="Version">The release version label.</param>
/// <param name="Channel">The distribution channel.</param>
/// <param name="DownloadUri">The HTTPS package URI.</param>
/// <param name="Sha256">The expected lowercase or uppercase SHA-256 hex digest.</param>
/// <param name="ReleaseNotes">Release notes or a concise release summary.</param>
/// <param name="PublishedAt">The release publication time.</param>
public sealed record FirmwareManifestEntry(
    FirmwareFamily Family,
    string BoardTarget,
    ushort VendorId,
    ushort ProductId,
    uint BoardVersion,
    string Version,
    FirmwareReleaseChannel Channel,
    Uri DownloadUri,
    string Sha256,
    string? ReleaseNotes,
    DateTimeOffset PublishedAt);
