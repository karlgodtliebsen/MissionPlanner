using System.Text.Json.Serialization;
using MissionPlanner.Core.Vehicles.Models;

namespace MissionPlanner.Core.Configuration.VendorDevices;

/// <summary>Identifies how a vendor device is reached.</summary>
public enum VendorDeviceTransport
{
    /// <summary>MAVLink device-operation proxy.</summary>
    MavLinkDeviceOperation,

    /// <summary>Direct network transport.</summary>
    Network,

    /// <summary>CAN transport.</summary>
    Can
}

/// <summary>Describes vendor-device capabilities without device-specific fields.</summary>
[Flags]
public enum VendorDeviceCapabilities
{
    /// <summary>No verified capability.</summary>
    None = 0,

    /// <summary>The device can be discovered.</summary>
    Discovery = 1,

    /// <summary>Configuration can be read.</summary>
    ReadConfiguration = 2,

    /// <summary>Configuration can be applied.</summary>
    ApplyConfiguration = 4,

    /// <summary>Applied configuration can be read back.</summary>
    ConfirmReadback = 8,

    /// <summary>A failed apply can attempt rollback.</summary>
    Rollback = 16,

    /// <summary>Configuration can be exported without secrets.</summary>
    Export = 32,

    /// <summary>The device requires authentication.</summary>
    Authentication = 64
}

/// <summary>Identifies discovery and workflow availability.</summary>
public enum VendorDeviceStatus
{
    /// <summary>No discovery has run.</summary>
    NotDiscovered,

    /// <summary>The host vehicle is not connected.</summary>
    NotConnected,

    /// <summary>Discovery is running.</summary>
    Discovering,

    /// <summary>The transport responded but the protocol or product is unsupported.</summary>
    Unsupported,

    /// <summary>Authentication is required.</summary>
    AuthenticationRequired,

    /// <summary>The device is available.</summary>
    Available,

    /// <summary>A device operation is running.</summary>
    Busy,

    /// <summary>The device requires a reconnect after apply.</summary>
    ReconnectRequired,

    /// <summary>The workflow failed.</summary>
    Error
}

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

/// <summary>Supplies optional device authentication while redacting its secret.</summary>
/// <param name="UserName">The non-secret user name.</param>
/// <param name="Secret">The secret, excluded from JSON serialization.</param>
public sealed record VendorDeviceAuthentication(
    string UserName,
    [property: JsonIgnore] string Secret)
{
    /// <inheritdoc />
    public override string ToString() => $"UserName = {UserName}, Secret = ***";
}

/// <summary>Marks a typed vendor-device configuration.</summary>
public interface IVendorDeviceConfiguration
{
    /// <summary>Gets the device-specific configuration schema identifier.</summary>
    string Schema { get; }
}

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

/// <summary>Describes one device-specific validation failure.</summary>
/// <param name="Path">The configuration path.</param>
/// <param name="Message">The validation message.</param>
public sealed record VendorDeviceValidationIssue(string Path, string Message);

/// <summary>Describes a confirmed apply or rollback result.</summary>
/// <typeparam name="TConfiguration">The device-specific configuration type.</typeparam>
/// <param name="Success">Whether apply and readback succeeded.</param>
/// <param name="Message">The user-facing result.</param>
/// <param name="ConfirmedSnapshot">The confirmed snapshot, when available.</param>
/// <param name="RollbackAttempted">Whether rollback was attempted.</param>
/// <param name="RollbackSucceeded">Whether rollback was confirmed.</param>
public sealed record VendorDeviceApplyResult<TConfiguration>(
    bool Success,
    string Message,
    VendorDeviceSnapshot<TConfiguration>? ConfirmedSnapshot,
    bool RollbackAttempted,
    bool RollbackSucceeded)
    where TConfiguration : IVendorDeviceConfiguration;
