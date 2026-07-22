using MissionPlanner.Core.Vehicles.Models;
using MissionPlanner.MavLink.Generated;

namespace MissionPlanner.Core.Configuration.VendorDevices;

/// <summary>Represents a correlated MAVLink device-operation result.</summary>
/// <param name="ResultCode">Zero for success; otherwise the vehicle-reported failure code.</param>
/// <param name="RegisterStart">The first register returned by a read.</param>
/// <param name="Data">The returned bytes.</param>
public sealed record DeviceOperationResult(byte ResultCode, byte RegisterStart, byte[] Data)
{
    /// <summary>Gets whether the vehicle reported success.</summary>
    public bool IsSuccess => ResultCode == 0;
}

/// <summary>Provides correlated MAVLink device read/write operations.</summary>
public interface IDeviceOperationClient
{
    /// <summary>Reads device registers through a connected vehicle.</summary>
    /// <param name="vehicleId">The target vehicle.</param>
    /// <param name="busType">The documented bus type.</param>
    /// <param name="bus">The documented bus number.</param>
    /// <param name="address">The documented device address.</param>
    /// <param name="registerStart">The first register.</param>
    /// <param name="count">The byte count.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The correlated read result.</returns>
    Task<DeviceOperationResult> ReadAsync(
        VehicleId vehicleId,
        DeviceOpBustype busType,
        byte bus,
        byte address,
        byte registerStart,
        byte count,
        CancellationToken cancellationToken = default);

    /// <summary>Writes device registers through a connected vehicle.</summary>
    /// <param name="vehicleId">The target vehicle.</param>
    /// <param name="busType">The documented bus type.</param>
    /// <param name="bus">The documented bus number.</param>
    /// <param name="address">The documented device address.</param>
    /// <param name="registerStart">The first register.</param>
    /// <param name="data">The bytes to write.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The correlated write result.</returns>
    Task<DeviceOperationResult> WriteAsync(
        VehicleId vehicleId,
        DeviceOpBustype busType,
        byte bus,
        byte address,
        byte registerStart,
        ReadOnlyMemory<byte> data,
        CancellationToken cancellationToken = default);
}
