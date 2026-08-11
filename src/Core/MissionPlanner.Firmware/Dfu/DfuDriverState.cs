namespace MissionPlanner.Firmware.Dfu;

/// <summary>Describes the host driver state for a DFU USB device.</summary>
public enum DfuDriverState
{
    /// <summary>No matching DFU USB device is present.</summary>
    NotPresent,

    /// <summary>The device and expected provider driver are ready.</summary>
    PresentReady,

    /// <summary>The device is present with an incompatible driver.</summary>
    PresentWrongDriver,

    /// <summary>Windows reports a device or driver problem.</summary>
    PresentWithProblem,

    /// <summary>The provider reports that the device is busy.</summary>
    Busy,

    /// <summary>Available evidence cannot determine driver readiness.</summary>
    Unknown
}
