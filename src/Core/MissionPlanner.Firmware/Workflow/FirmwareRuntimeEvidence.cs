namespace MissionPlanner.Firmware.Workflow;

/// <summary>
/// Source of the evidence that identified an application runtime. This is deliberately
/// separate from exact flight-controller board identity: USB metadata is only a discovery
/// hint, while a live protocol exchange or an existing vehicle session can prove the runtime.
/// </summary>
public enum FirmwareRuntimeEvidence
{
    /// <summary>No runtime evidence has been gathered.</summary>
    None,
    /// <summary>USB friendly name or VID/PID only; never authoritative for the runtime or board.</summary>
    UsbHint,
    /// <summary>An already-connected Mission Planner vehicle session for the same serial endpoint.</summary>
    ExistingVehicleSession,
    /// <summary>A bounded, read-only isolated MAVLink probe.</summary>
    MavLinkProbe,
    /// <summary>A bounded Betaflight MSP probe.</summary>
    MspProbe,
    /// <summary>An ArduPilot serial bootloader handshake.</summary>
    ArduPilotBootloader
}
