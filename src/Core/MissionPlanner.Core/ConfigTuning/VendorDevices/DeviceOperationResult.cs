namespace MissionPlanner.Core.ConfigTuning.VendorDevices;

/// <summary>Represents a correlated MAVLink device-operation result.</summary>
/// <param name="ResultCode">Zero for success; otherwise the vehicle-reported failure code.</param>
/// <param name="RegisterStart">The first register returned by a read.</param>
/// <param name="Data">The returned bytes.</param>
public sealed record DeviceOperationResult(byte ResultCode, byte RegisterStart, byte[] Data)
{
    /// <summary>Gets whether the vehicle reported success.</summary>
    public bool IsSuccess => ResultCode == 0;
}
