using MissionPlanner.Firmware.Dfu;
using MissionPlanner.Firmware.Model;
using MissionPlanner.Firmware.Workflow;

namespace MissionPlanner.Firmware.Preparation;

/// <summary>Common artifact presentation with source-specific provenance and separate compatibility.</summary>
public sealed record FirmwareArtifactSummary
{
    /// <summary>Gets the source displayed to the operator.</summary>
    public string Source { get; init; } = "No firmware selected";
    /// <summary>Gets the installation artifact family.</summary>
    public FirmwareArtifactFormat Format { get; init; }
    /// <summary>Gets the declared or selected platform.</summary>
    public string? Platform { get; init; }
    /// <summary>Gets the package or selected board ID.</summary>
    public int? BoardId { get; init; }
    /// <summary>Gets the vehicle family when available.</summary>
    public string? VehicleFamily { get; init; }
    /// <summary>Gets the build/version metadata.</summary>
    public string? Version { get; init; }
    /// <summary>Gets decoded image size.</summary>
    public long ImageSize { get; init; }
    /// <summary>Gets inspected address ranges for a combined HEX artifact.</summary>
    public string? AddressRanges { get; init; }
    /// <summary>Gets the validated content hash.</summary>
    public string? Sha256 { get; init; }
    /// <summary>Gets durable cache identity.</summary>
    public string? CacheIdentity { get; init; }
    /// <summary>Gets whether this preparation reused cached content.</summary>
    public bool CacheHit { get; init; }
    /// <summary>Gets the official source URL, absent for local files.</summary>
    public Uri? OnlineUrl { get; init; }
    /// <summary>Gets the official release channel.</summary>
    public string? Channel { get; init; }
    /// <summary>Gets the firmware Git identity.</summary>
    public string? GitSha { get; init; }
    /// <summary>Gets original local file provenance.</summary>
    public string? LocalFile { get; init; }
    /// <summary>Gets when local content was imported.</summary>
    public DateTimeOffset? ImportedAt { get; init; }
    /// <summary>Gets whether structure and payload validation succeeded.</summary>
    public bool ArtifactValid { get; init; }
    /// <summary>Gets whether compatibility against the current target succeeded.</summary>
    public bool TargetCompatible { get; init; }
    /// <summary>Gets validation or safety warnings.</summary>
    public string? Warnings { get; init; }

    /// <summary>Projects an official APJ into the same representation used for local content.</summary>
    public static FirmwareArtifactSummary FromOnline(FirmwarePreparationResult prepared)
    {
        return FromPackage(prepared.Package) with
        {
            Source = "Official catalogue", OnlineUrl = prepared.ManifestEntry.Artifact.DownloadUri,
            Channel = prepared.ManifestEntry.Channel.ToString(), Platform = prepared.ManifestEntry.Target.Platform,
            VehicleFamily = prepared.ManifestEntry.Target.VehicleType.ToString(), Version = prepared.ManifestEntry.Version.Value,
            GitSha = prepared.ManifestEntry.GitSha, Sha256 = prepared.Sha256, CacheIdentity = prepared.CacheIdentity,
            CacheHit = prepared.WasCacheHit, Warnings = string.Join(Environment.NewLine, prepared.Warnings)
        };
    }

    /// <summary>Projects an imported local APJ without asserting hardware compatibility.</summary>
    public static FirmwareArtifactSummary FromLocal(LocalFirmwarePreparationResult prepared)
    {
        return FromPackage(prepared.Package) with
        {
            Source = "Local file", LocalFile = prepared.OriginalPath ?? prepared.FileName,
            ImportedAt = prepared.ArtifactMetadata.DownloadedAt, Sha256 = prepared.ArtifactMetadata.Sha256,
            CacheIdentity = prepared.ArtifactMetadata.CacheKey, CacheHit = prepared.WasCacheHit
        };
    }

    /// <summary>Projects inspected HEX without conflating it with an APJ application package.</summary>
    public static FirmwareArtifactSummary FromHex(DfuArtifact artifact)
    {
        return new()
        {
            Source = artifact.SourceUri is null ? "Local file" : "Official catalogue",
            Format = FirmwareArtifactFormat.WithBootloaderHex, Platform = artifact.Platform, BoardId = artifact.BoardId,
            OnlineUrl = artifact.SourceUri, LocalFile = artifact.SourceUri is null ? artifact.LocalPath : null,
            Sha256 = artifact.Metadata.Sha256, CacheIdentity = artifact.LocalPath,
            ImageSize = artifact.Metadata.DataBytes, ArtifactValid = true,
            ImportedAt = artifact.SourceUri is null ? artifact.Metadata.InspectedAt : null,
            AddressRanges = string.Join(", ", artifact.Metadata.Ranges.Select(range => $"0x{range.StartAddress:X8}–0x{range.EndAddress:X8}")),
            Warnings = string.Join(Environment.NewLine, artifact.Metadata.Warnings)
        };
    }

    private static FirmwareArtifactSummary FromPackage(ApjFirmwarePackage package)
    {
        return new()
        {
            Format = FirmwareArtifactFormat.Apj, Platform = package.Summary, BoardId = package.BoardId,
            Version = package.Version, GitSha = package.GitIdentity, ImageSize = package.Image.Length,
            ArtifactValid = true
        };
    }
}
