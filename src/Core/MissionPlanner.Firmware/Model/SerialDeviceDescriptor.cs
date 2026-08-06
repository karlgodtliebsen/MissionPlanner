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

    private static string? Normalize(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}
