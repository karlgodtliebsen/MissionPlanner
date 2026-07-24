namespace MissionPlanner.Core.ConfigTuning.VendorDevices;

/// <summary>Marks a typed vendor-device configuration.</summary>
public interface IVendorDeviceConfiguration
{
    /// <summary>Gets the device-specific configuration schema identifier.</summary>
    string Schema { get; }
}
