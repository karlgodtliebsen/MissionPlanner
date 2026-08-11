namespace MissionPlanner.Firmware.Dfu;

/// <summary>Restricts external-process arguments to a known DFU provider operation.</summary>
public enum DfuProcessPurpose
{
    /// <summary>Runs a non-mutating help or version probe.</summary>
    ValidateTool,

    /// <summary>Lists USB DFU devices.</summary>
    ListDevices,

    /// <summary>Inspects one selected USB DFU device.</summary>
    InspectDevice,

    /// <summary>Programs and verifies one validated Intel HEX artifact.</summary>
    ProgramAndVerify
}
