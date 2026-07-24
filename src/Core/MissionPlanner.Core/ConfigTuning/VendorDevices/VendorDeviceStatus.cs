namespace MissionPlanner.Core.ConfigTuning.VendorDevices;

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
