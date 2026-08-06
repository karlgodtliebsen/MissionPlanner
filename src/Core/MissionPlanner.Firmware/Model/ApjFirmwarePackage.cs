namespace MissionPlanner.Firmware.Model;

/// <summary>Represents a validated APJ firmware image.</summary>
public sealed record ApjFirmwarePackage
{
    /// <summary>Initializes an APJ package.</summary>
    public ApjFirmwarePackage(
        int boardId,
        ReadOnlyMemory<byte> image,
        int imageMaximumSize,
        ReadOnlyMemory<byte> externalImage = default,
        int boardRevision = 0,
        int? boardRevisionMaximum = null,
        int minimumBootloaderRevision = 0,
        bool? requiresSecureBoot = null,
        bool? isSigned = null,
        string? description = null,
        string? summary = null,
        string? version = null,
        string? gitIdentity = null,
        IReadOnlyDictionary<string, string>? rawMetadata = null)
    {
        if (boardId <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(boardId));
        }

        if (image.IsEmpty)
        {
            throw new ArgumentException("Firmware image cannot be empty.", nameof(image));
        }

        BoardId = boardId;
        Image = image;
        if (imageMaximumSize <= 0 || image.Length > imageMaximumSize)
        {
            throw new ArgumentOutOfRangeException(nameof(imageMaximumSize));
        }

        if (boardRevision < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(boardRevision));
        }

        if (boardRevisionMaximum < boardRevision)
        {
            throw new ArgumentOutOfRangeException(nameof(boardRevisionMaximum));
        }

        if (minimumBootloaderRevision < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(minimumBootloaderRevision));
        }

        ImageMaximumSize = imageMaximumSize;
        ExternalImage = externalImage;
        BoardRevision = boardRevision;
        BoardRevisionMaximum = boardRevisionMaximum;
        MinimumBootloaderRevision = minimumBootloaderRevision;
        RequiresSecureBoot = requiresSecureBoot;
        IsSigned = isSigned;
        Description = description;
        Summary = summary;
        Version = version;
        GitIdentity = gitIdentity;
        RawMetadata = rawMetadata ?? new Dictionary<string, string>();
    }

    /// <summary>Gets the target board ID.</summary>
    public int BoardId { get; }

    /// <summary>Gets the decoded application image.</summary>
    public ReadOnlyMemory<byte> Image { get; }

    /// <summary>Gets the declared internal flash limit.</summary>
    public int ImageMaximumSize { get; }

    /// <summary>Gets the optional decoded external-flash image.</summary>
    public ReadOnlyMemory<byte> ExternalImage { get; }

    /// <summary>Gets the minimum board revision, or zero when unspecified.</summary>
    public int BoardRevision { get; }

    /// <summary>Gets the optional maximum supported hardware revision.</summary>
    public int? BoardRevisionMaximum { get; }

    /// <summary>Gets the minimum bootloader revision when declared.</summary>
    public int MinimumBootloaderRevision { get; }

    /// <summary>Gets whether the package explicitly requires secure boot.</summary>
    public bool? RequiresSecureBoot { get; }

    /// <summary>Gets whether the package declares a cryptographic signature.</summary>
    public bool? IsSigned { get; }

    /// <summary>Gets the package description.</summary>
    public string? Description { get; }

    /// <summary>Gets the package platform summary.</summary>
    public string? Summary { get; }

    /// <summary>Gets the build version.</summary>
    public string? Version { get; }

    /// <summary>Gets the source identity.</summary>
    public string? GitIdentity { get; }

    /// <summary>Gets preserved package metadata.</summary>
    public IReadOnlyDictionary<string, string> RawMetadata { get; }
}
