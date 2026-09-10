namespace MissionPlanner.Firmware.Workflow;

/// <summary>Resolves capabilities without treating unrelated telemetry as a global lock.</summary>
public static class FirmwareWorkflowResolver
{
    /// <summary>Resolves preparation and target-specific actions from current evidence.</summary>
    public static FirmwareWorkflowCapabilities Resolve(FirmwareWorkflowContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var idle = !context.OperationInProgress;
        var serial = context.PhysicalTarget == FirmwarePhysicalTarget.Serial;
        var available = idle && context.HardwareSupported && !context.TargetPortOwned && !context.TargetArmed;
        var format = context.BootEnvironment switch
        {
            FirmwareBootEnvironment.ArduPilotBootloader => FirmwareArtifactFormat.Apj,
            FirmwareBootEnvironment.Stm32RomDfu => FirmwareArtifactFormat.WithBootloaderHex,
            _ when context.PhysicalTarget == FirmwarePhysicalTarget.Stm32Dfu => FirmwareArtifactFormat.WithBootloaderHex,
            _ when serial && context.Runtime == FirmwareRuntimeKind.ArduPilot => FirmwareArtifactFormat.Apj,
            _ when serial && context.Runtime == FirmwareRuntimeKind.Betaflight => FirmwareArtifactFormat.WithBootloaderHex,
            _ => FirmwareArtifactFormat.None
        };
        var (code, reason) = context switch
        {
            { OperationInProgress: true } => ("workflow.busy", "A firmware operation is in progress."),
            { HardwareSupported: false } => ("workflow.unsupported", "Hardware installation is unavailable on this platform."),
            { PhysicalTarget: FirmwarePhysicalTarget.None } => ("target.absent", "Select a physical controller. Firmware preparation is available."),
            { TargetPortOwned: true } => ("target.port-owned", "Disconnect the telemetry session that owns the selected serial port."),
            { TargetArmed: true } => ("target.armed", "Disarm the selected controller before firmware operations."),
            _ when format == FirmwareArtifactFormat.None => ("target.runtime-unknown", "Probe the controller runtime or use manual BOOT/RESET recovery."),
            _ when context.BootEnvironment == FirmwareBootEnvironment.None && serial => ("target.boot-entry-required",
                format == FirmwareArtifactFormat.Apj ? "Enter the ArduPilot bootloader to identify the board." : "Enter STM32 DFU before installation."),
            { ArtifactValid: false } => ("artifact.not-validated", "Select and validate firmware for this controller."),
            _ when context.ArtifactFormat != format => ("artifact.wrong-format", $"This transport requires {format}."),
            _ when format == FirmwareArtifactFormat.Apj && (context.Bootloader is null
                || context.IdentityConfidence != FirmwareIdentityConfidence.Verified) => ("target.board-unproven", "Read the ArduPilot bootloader board identity."),
            _ when format == FirmwareArtifactFormat.WithBootloaderHex && string.IsNullOrWhiteSpace(context.Platform)
                => ("target.platform-required", "Select the exact ArduPilot platform and review target safety."),
            { TargetCompatible: false } => ("target.compatibility-pending", "Identify the target and check firmware compatibility."),
            _ when format == FirmwareArtifactFormat.WithBootloaderHex && !context.TargetSafetyConfirmed
                => ("target.confirmation-required", "Review and confirm the exact DFU target before installation."),
            _ => ((string?)null, (string?)null)
        };
        return new FirmwareWorkflowCapabilities
        {
            CanBrowseOnlineFirmware = idle,
            CanSelectLocalFirmware = idle,
            CanRefreshPhysicalDevices = idle && context.HardwareSupported,
            CanProbeRuntime = available && serial && context.BootEnvironment == FirmwareBootEnvironment.None,
            CanEnterArduPilotBootloader = available && serial && context.Runtime == FirmwareRuntimeKind.ArduPilot
                && context.BootEnvironment == FirmwareBootEnvironment.None,
            CanEnterStm32Dfu = available && serial && context.Runtime == FirmwareRuntimeKind.Betaflight
                && context.BootEnvironment == FirmwareBootEnvironment.None,
            CanInstall = code is null,
            RequiredArtifactFormat = format,
            BlockCode = code,
            BlockReason = reason
        };
    }
}
