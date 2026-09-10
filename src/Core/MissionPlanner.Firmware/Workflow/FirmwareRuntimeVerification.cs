namespace MissionPlanner.Firmware.Workflow;

/// <summary>
/// Strength of an application-runtime identification. Verification applies to the runtime
/// only; it never implies that the exact flight-controller board has been established.
/// </summary>
public enum FirmwareRuntimeVerification
{
    /// <summary>The runtime has not been identified.</summary>
    None,
    /// <summary>A discovery hint suggests a runtime but no protocol identity confirms it.</summary>
    Hinted,
    /// <summary>A live protocol identity or an existing vehicle session proves the runtime.</summary>
    Verified
}
