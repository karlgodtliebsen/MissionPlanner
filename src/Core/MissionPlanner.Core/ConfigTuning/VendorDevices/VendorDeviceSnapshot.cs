using MissionPlanner.Core.Vehicles.Models;

namespace MissionPlanner.Core.ConfigTuning.VendorDevices;

/// <summary>Contains a read-before-edit vendor-device snapshot.</summary>
/// <typeparam name="TConfiguration">The device-specific configuration type.</typeparam>
/// <param name="VehicleId">The vehicle that proxied the device operation.</param>
/// <param name="Identity">The verified device identity.</param>
/// <param name="Transport">The transport used.</param>
/// <param name="Capabilities">The verified capabilities.</param>
/// <param name="Configuration">The confirmed configuration.</param>
/// <param name="ReadAt">The read timestamp.</param>
/// <param name="RequiresReboot">Whether the snapshot declares a reboot requirement.</param>
/// <param name="RequiresReconnect">Whether the snapshot declares a reconnect requirement.</param>
public sealed record VendorDeviceSnapshot<TConfiguration>(
    VehicleId VehicleId,
    VendorDeviceIdentity Identity,
    VendorDeviceTransport Transport,
    VendorDeviceCapabilities Capabilities,
    TConfiguration Configuration,
    DateTimeOffset ReadAt,
    bool RequiresReboot,
    bool RequiresReconnect)
    where TConfiguration : IVendorDeviceConfiguration;
