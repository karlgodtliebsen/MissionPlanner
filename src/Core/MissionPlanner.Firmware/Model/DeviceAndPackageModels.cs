namespace MissionPlanner.Firmware.Model;

/// <summary>Describes a serial device visible to firmware discovery.</summary>
public sealed record SerialDeviceDescriptor
{
    /// <summary>Initializes a serial device descriptor.</summary>
    public SerialDeviceDescriptor(
        string portName,
        string? osDeviceId = null,
        UsbIdentifier? usbIdentifier = null,
        string? usbSerialNumber = null,
        string? productName = null,
        string? manufacturer = null,
        IEnumerable<string>? boardHints = null,
        DateTimeOffset? arrivedAt = null)
    {
        PortName = string.IsNullOrWhiteSpace(portName) ? throw new ArgumentException("A transient port name is required.", nameof(portName)) : portName.Trim();
        OsDeviceId = Normalize(osDeviceId);
        UsbIdentifier = usbIdentifier;
        UsbSerialNumber = Normalize(usbSerialNumber);
        ProductName = Normalize(productName);
        Manufacturer = Normalize(manufacturer);
        BoardHints = (boardHints ?? []).Where(value => !string.IsNullOrWhiteSpace(value)).Select(value => value.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        ArrivedAt = arrivedAt ?? DateTimeOffset.UtcNow;
    }

    /// <summary>Gets the transient port name, such as COM7.</summary>
    public string PortName { get; }
    /// <summary>Gets the stable operating-system device identity when available.</summary>
    public string? OsDeviceId { get; }
    /// <summary>Gets the USB vendor and product identity when available.</summary>
    public UsbIdentifier? UsbIdentifier { get; }
    /// <summary>Gets the USB serial number when available.</summary>
    public string? UsbSerialNumber { get; }
    /// <summary>Gets the product name when available.</summary>
    public string? ProductName { get; }
    /// <summary>Gets the manufacturer when available.</summary>
    public string? Manufacturer { get; }
    /// <summary>Gets normalized board or bootloader hints.</summary>
    public IReadOnlyList<string> BoardHints { get; }
    /// <summary>Gets when this device arrival was first observed.</summary>
    public DateTimeOffset ArrivedAt { get; }
    /// <summary>Gets the best stable matching key, excluding the transient port name.</summary>
    public string? StableIdentity => OsDeviceId ?? (UsbIdentifier is not null && UsbSerialNumber is not null ? $"{UsbIdentifier}:{UsbSerialNumber}" : null);

    private static string? Normalize(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

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
