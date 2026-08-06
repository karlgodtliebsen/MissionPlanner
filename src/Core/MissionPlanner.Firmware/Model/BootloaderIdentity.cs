namespace MissionPlanner.Firmware.Model;

/// <summary>Describes a bootloader queried from a device.</summary>
public sealed record BootloaderIdentity
{
    /// <summary>Initializes a bootloader identity.</summary>
    public BootloaderIdentity(
        int boardId,
        int bootloaderRevision,
        long flashSize,
        int boardRevision = 0,
        long externalFlashSize = 0,
        string? chipDescription = null,
        bool? isSecure = null)
    {
        if (boardId <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(boardId));
        }

        if (bootloaderRevision < 2)
        {
            throw new ArgumentOutOfRangeException(nameof(bootloaderRevision));
        }

        if (flashSize <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(flashSize));
        }

        if (boardRevision < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(boardRevision));
        }

        if (externalFlashSize < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(externalFlashSize));
        }

        BoardId = boardId;
        BootloaderRevision = bootloaderRevision;
        FlashSize = flashSize;
        BoardRevision = boardRevision;
        ExternalFlashSize = externalFlashSize;
        ChipDescription = chipDescription;
        IsSecure = isSecure;
    }

    /// <summary>Gets the bootloader board ID.</summary>
    public int BoardId { get; }

    /// <summary>Gets the protocol revision.</summary>
    public int BootloaderRevision { get; }

    /// <summary>Gets writable flash capacity in bytes.</summary>
    public long FlashSize { get; }

    /// <summary>Gets the hardware board revision.</summary>
    public int BoardRevision { get; }

    /// <summary>Gets external flash capacity in bytes.</summary>
    public long ExternalFlashSize { get; }

    /// <summary>Gets the optional bootloader chip description.</summary>
    public string? ChipDescription { get; }

    /// <summary>Gets secure-boot state when the bootloader reports it.</summary>
    public bool? IsSecure { get; }
}
