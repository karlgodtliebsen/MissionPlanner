namespace MissionPlanner.Core.ConfigTuning.VendorDevices;

/// <summary>Identifies a discovered vendor device.</summary>
/// <param name="Vendor">The vendor name.</param>
/// <param name="Product">The product name.</param>
/// <param name="SerialNumber">An optional verified serial number.</param>
/// <param name="FirmwareVersion">An optional verified firmware version.</param>
public sealed record VendorDeviceIdentity(
    string Vendor,
    string Product,
    string? SerialNumber = null,
    string? FirmwareVersion = null);
