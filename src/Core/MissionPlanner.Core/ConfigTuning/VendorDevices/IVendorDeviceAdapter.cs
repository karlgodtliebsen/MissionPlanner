using MissionPlanner.Core.Vehicles.Models;

namespace MissionPlanner.Core.ConfigTuning.VendorDevices;

/// <summary>Defines a typed adapter for one vendor-device family.</summary>
/// <typeparam name="TConfiguration">The isolated device-specific configuration type.</typeparam>
public interface IVendorDeviceAdapter<TConfiguration>
    where TConfiguration : IVendorDeviceConfiguration
{
    /// <summary>Gets the stable device-family key.</summary>
    string DeviceType { get; }

    /// <summary>Discovers and reads a device using only its documented mechanism.</summary>
    /// <param name="vehicleId">The connected vehicle proxying the operation.</param>
    /// <param name="authentication">Optional device authentication.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The discovery result.</returns>
    Task<VendorDeviceDiscoveryResult<TConfiguration>> DiscoverAsync(
        VehicleId vehicleId,
        VendorDeviceAuthentication? authentication = null,
        CancellationToken cancellationToken = default);

    /// <summary>Reads a fresh configuration snapshot.</summary>
    /// <param name="vehicleId">The connected vehicle proxying the operation.</param>
    /// <param name="authentication">Optional device authentication.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The confirmed snapshot.</returns>
    Task<VendorDeviceSnapshot<TConfiguration>> ReadAsync(
        VehicleId vehicleId,
        VendorDeviceAuthentication? authentication = null,
        CancellationToken cancellationToken = default);

    /// <summary>Validates a device-specific configuration without I/O.</summary>
    /// <param name="configuration">The candidate configuration.</param>
    /// <returns>All validation issues.</returns>
    IReadOnlyList<VendorDeviceValidationIssue> Validate(TConfiguration configuration);

    /// <summary>Applies, reads back, and rolls back on failure.</summary>
    /// <param name="vehicleId">The connected vehicle proxying the operation.</param>
    /// <param name="original">The read-before-edit snapshot used for rollback.</param>
    /// <param name="desired">The desired configuration.</param>
    /// <param name="authentication">Optional device authentication.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The apply result.</returns>
    Task<VendorDeviceApplyResult<TConfiguration>> ApplyAsync(
        VehicleId vehicleId,
        VendorDeviceSnapshot<TConfiguration> original,
        TConfiguration desired,
        VendorDeviceAuthentication? authentication = null,
        CancellationToken cancellationToken = default);

    /// <summary>Exports a non-secret configuration document.</summary>
    /// <param name="configuration">The configuration to export.</param>
    /// <returns>The exported document.</returns>
    string Export(TConfiguration configuration);
}
