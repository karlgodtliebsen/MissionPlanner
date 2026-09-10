namespace MissionPlanner.Firmware.Workflow;

/// <summary>Independent actions available for a firmware evidence snapshot.</summary>
public sealed record FirmwareWorkflowCapabilities
{
    /// <summary>Gets whether catalogue preparation may run.</summary>
    public bool CanBrowseOnlineFirmware { get; init; }
    /// <summary>Gets whether local artifacts may be imported.</summary>
    public bool CanSelectLocalFirmware { get; init; }
    /// <summary>Gets whether physical enumeration may run.</summary>
    public bool CanRefreshPhysicalDevices { get; init; }
    /// <summary>Gets whether the selected serial resource may be probed.</summary>
    public bool CanProbeRuntime { get; init; }
    /// <summary>Gets whether ArduPilot bootloader entry may run.</summary>
    public bool CanEnterArduPilotBootloader { get; init; }
    /// <summary>Gets whether proven Betaflight may request STM32 DFU.</summary>
    public bool CanEnterStm32Dfu { get; init; }
    /// <summary>Gets whether an install may execute.</summary>
    public bool CanInstall { get; init; }
    /// <summary>Gets the artifact required by the target's boot transport.</summary>
    public FirmwareArtifactFormat RequiredArtifactFormat { get; init; }
    /// <summary>Gets a stable code explaining the install block.</summary>
    public string? BlockCode { get; init; }
    /// <summary>Gets the next step or blocking reason.</summary>
    public string? BlockReason { get; init; }
}
