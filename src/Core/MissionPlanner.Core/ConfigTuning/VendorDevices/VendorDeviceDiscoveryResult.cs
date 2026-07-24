namespace MissionPlanner.Core.ConfigTuning.VendorDevices;

/// <summary>Describes a device discovery result.</summary>
/// <typeparam name="TConfiguration">The device-specific configuration type.</typeparam>
/// <param name="Status">The discovery status.</param>
/// <param name="Message">The user-facing result.</param>
/// <param name="Snapshot">The confirmed snapshot when available.</param>
public sealed record VendorDeviceDiscoveryResult<TConfiguration>(
    VendorDeviceStatus Status,
    string Message,
    VendorDeviceSnapshot<TConfiguration>? Snapshot)
    where TConfiguration : IVendorDeviceConfiguration;
