using MissionPlanner.Firmware.Model;

namespace MissionPlanner.App.Views.InitSetup.InstallFirmware;

/// <summary>Preserves a typed firmware serial-device candidate for display and selection.</summary>
public sealed class FirmwareDeviceItemViewModel
{
    /// <summary>Initializes a device item and its recommendation evidence.</summary>
    public FirmwareDeviceItemViewModel(SerialDeviceDescriptor descriptor, bool isRecommended, string recommendation)
    {
        Descriptor = descriptor ?? throw new ArgumentNullException(nameof(descriptor));
        IsRecommended = isRecommended;
        Recommendation = recommendation;
    }

    /// <summary>Gets the complete descriptor passed to firmware orchestration.</summary>
    public SerialDeviceDescriptor Descriptor { get; }
    /// <summary>Gets the transient serial port name.</summary>
    public string PortName => Descriptor.PortName;
    /// <summary>Gets the stable operating-system identifier.</summary>
    public string? StableOsId => Descriptor.OsDeviceId;
    /// <summary>Gets the USB VID/PID display value.</summary>
    public string UsbId => Descriptor.UsbIdentifier?.ToString() ?? "Unknown USB identity";
    /// <summary>Gets the USB serial number.</summary>
    public string? UsbSerialNumber => Descriptor.UsbSerialNumber;
    /// <summary>Gets the manufacturer.</summary>
    public string? Manufacturer => Descriptor.Manufacturer;
    /// <summary>Gets the product name.</summary>
    public string ProductName => Descriptor.ProductName ?? "Serial device";
    /// <summary>Gets board-detection hints.</summary>
    public string BoardHint => Descriptor.BoardHints.Count == 0 ? "No board hint" : string.Join(", ", Descriptor.BoardHints);
    /// <summary>Gets whether this candidate has high-confidence recommendation evidence.</summary>
    public bool IsRecommended { get; }
    /// <summary>Gets a user-facing explanation of the recommendation.</summary>
    public string Recommendation { get; }
    /// <inheritdoc />
    public override string ToString() => $"{PortName} · {ProductName} · {UsbId}{(IsRecommended ? $" · {Recommendation}" : string.Empty)}";
}
