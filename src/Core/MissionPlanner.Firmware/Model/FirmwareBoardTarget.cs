namespace MissionPlanner.Firmware.Model;

/// <summary>Describes the hardware target declared by a firmware release.</summary>
public sealed record FirmwareBoardTarget
{
    /// <summary>Initializes a board target.</summary>
    public FirmwareBoardTarget(
        int boardId,
        string platform,
        FirmwareVehicleType vehicleType,
        IEnumerable<UsbIdentifier>? usbIdentifiers = null,
        IEnumerable<string>? bootloaderNames = null)
    {
        if (boardId <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(boardId), "Board ID must be positive.");
        }

        BoardId = boardId;
        Platform = string.IsNullOrWhiteSpace(platform)
            ? throw new ArgumentException("A platform is required.", nameof(platform))
            : platform.Trim();
        VehicleType = vehicleType;
        UsbIdentifiers = (usbIdentifiers ?? []).Distinct().ToArray();
        BootloaderNames = (bootloaderNames ?? [])
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    /// <summary>Gets the bootloader board ID.</summary>
    public int BoardId { get; }

    /// <summary>Gets the normalized platform name.</summary>
    public string Platform { get; }

    /// <summary>Gets the vehicle family.</summary>
    public FirmwareVehicleType VehicleType { get; }

    /// <summary>Gets known USB identifiers.</summary>
    public IReadOnlyList<UsbIdentifier> UsbIdentifiers { get; }

    /// <summary>Gets known bootloader names.</summary>
    public IReadOnlyList<string> BootloaderNames { get; }
}
