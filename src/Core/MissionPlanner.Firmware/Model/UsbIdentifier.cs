namespace MissionPlanner.Firmware.Model;

/// <summary>Identifies a USB device by vendor and product ID.</summary>
public readonly record struct UsbIdentifier
{
    /// <summary>Initializes a USB identifier.</summary>
    public UsbIdentifier(int vendorId, int productId)
    {
        VendorId = Validate(vendorId, nameof(vendorId));
        ProductId = Validate(productId, nameof(productId));
    }

    /// <summary>Gets the USB vendor ID.</summary>
    public ushort VendorId { get; }

    /// <summary>Gets the USB product ID.</summary>
    public ushort ProductId { get; }

    private static ushort Validate(int value, string parameterName) => value is > 0 and <= ushort.MaxValue
        ? (ushort)value
        : throw new ArgumentOutOfRangeException(parameterName, "USB identifiers must be between 1 and 65535.");
}
