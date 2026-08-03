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
    public ApjFirmwarePackage(int boardId, ReadOnlyMemory<byte> image, uint imageCrc)
    {
        if (boardId <= 0) throw new ArgumentOutOfRangeException(nameof(boardId));
        if (image.IsEmpty) throw new ArgumentException("Firmware image cannot be empty.", nameof(image));
        BoardId = boardId;
        Image = image;
        ImageCrc = imageCrc;
    }

    /// <summary>Gets the target board ID.</summary>
    public int BoardId { get; }

    /// <summary>Gets the decoded application image.</summary>
    public ReadOnlyMemory<byte> Image { get; }

    /// <summary>Gets the package-declared image CRC.</summary>
    public uint ImageCrc { get; }
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
