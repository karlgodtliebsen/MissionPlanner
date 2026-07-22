namespace MissionPlanner.Core.Firmware;

/// <summary>Represents a downloaded and verified firmware package.</summary>
/// <param name="Release">The source manifest release.</param>
/// <param name="LocalPath">The platform-local cached file path.</param>
/// <param name="Sha256">The computed SHA-256 digest.</param>
/// <param name="IsVerified">Whether the digest exactly matched the manifest.</param>
public sealed record FirmwarePackage(
    FirmwareManifestEntry Release,
    string LocalPath,
    string Sha256,
    bool IsVerified);
