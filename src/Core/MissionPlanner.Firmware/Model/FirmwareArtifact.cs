namespace MissionPlanner.Firmware.Model;

/// <summary>Describes a downloadable firmware artifact.</summary>
public sealed record FirmwareArtifact
{
    /// <summary>Initializes a firmware artifact.</summary>
    public FirmwareArtifact(Uri downloadUri, FirmwareImageFormat format, long size, string? sha256 = null)
    {
        ArgumentNullException.ThrowIfNull(downloadUri);
        if (!downloadUri.IsAbsoluteUri || downloadUri.Scheme is not ("http" or "https"))
        {
            throw new ArgumentException("Firmware artifact URLs must be absolute HTTP or HTTPS URLs.", nameof(downloadUri));
        }

        if (size <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(size), "Firmware artifact size must be positive.");
        }

        if (sha256 is not null && (sha256.Length != 64 || !sha256.All(Uri.IsHexDigit)))
        {
            throw new ArgumentException("SHA-256 must contain exactly 64 hexadecimal characters.", nameof(sha256));
        }

        DownloadUri = downloadUri;
        Format = format;
        Size = size;
        Sha256 = sha256?.ToUpperInvariant();
    }

    /// <summary>Gets the artifact download URI.</summary>
    public Uri DownloadUri { get; }

    /// <summary>Gets the image format.</summary>
    public FirmwareImageFormat Format { get; }

    /// <summary>Gets the expected encoded artifact size.</summary>
    public long Size { get; }

    /// <summary>Gets the optional expected SHA-256 hash.</summary>
    public string? Sha256 { get; }
}
