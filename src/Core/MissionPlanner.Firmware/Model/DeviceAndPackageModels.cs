namespace MissionPlanner.Firmware.Model;

/// <summary>Describes a serial device visible to firmware discovery.</summary>
public sealed record SerialDeviceDescriptor(
    string PortName,
    string DisplayName,
    UsbIdentifier? UsbIdentifier = null,
    string? HardwareId = null,
    bool IsBootloader = false);

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
        string? description = null,
        string? summary = null,
        string? version = null,
        string? gitIdentity = null,
        IReadOnlyDictionary<string, string>? rawMetadata = null)
    {
        if (boardId <= 0) throw new ArgumentOutOfRangeException(nameof(boardId));
        if (image.IsEmpty) throw new ArgumentException("Firmware image cannot be empty.", nameof(image));
        BoardId = boardId;
        Image = image;
        if (imageMaximumSize <= 0 || image.Length > imageMaximumSize) throw new ArgumentOutOfRangeException(nameof(imageMaximumSize));
        if (boardRevision < 0) throw new ArgumentOutOfRangeException(nameof(boardRevision));
        ImageMaximumSize = imageMaximumSize;
        ExternalImage = externalImage;
        BoardRevision = boardRevision;
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

/// <summary>Describes a bootloader queried from a device.</summary>
public sealed record BootloaderIdentity
{
    /// <summary>Initializes a bootloader identity.</summary>
    public BootloaderIdentity(int boardId, int revision, long flashSize)
    {
        if (boardId <= 0) throw new ArgumentOutOfRangeException(nameof(boardId));
        if (revision < 0) throw new ArgumentOutOfRangeException(nameof(revision));
        if (flashSize <= 0) throw new ArgumentOutOfRangeException(nameof(flashSize));
        BoardId = boardId;
        Revision = revision;
        FlashSize = flashSize;
    }

    /// <summary>Gets the bootloader board ID.</summary>
    public int BoardId { get; }

    /// <summary>Gets the protocol revision.</summary>
    public int Revision { get; }

    /// <summary>Gets writable flash capacity in bytes.</summary>
    public long FlashSize { get; }
}
