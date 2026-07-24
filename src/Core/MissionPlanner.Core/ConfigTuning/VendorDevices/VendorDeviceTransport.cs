namespace MissionPlanner.Core.ConfigTuning.VendorDevices;

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
