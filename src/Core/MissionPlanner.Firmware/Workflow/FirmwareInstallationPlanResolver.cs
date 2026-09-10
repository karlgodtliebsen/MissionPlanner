using MissionPlanner.Firmware.Entry;

namespace MissionPlanner.Firmware.Workflow;

/// <summary>Plans the existing AP serial and STM32 DFU services without duplicating flashing logic.</summary>
public static class FirmwareInstallationPlanResolver
{
    /// <summary>Resolves one deterministic plan from the selected target and artifact evidence.</summary>
    public static FirmwareInstallationPlan Resolve(FirmwareWorkflowContext context)
    {
        var capabilities = FirmwareWorkflowResolver.Resolve(context);
        BootloaderEntryTarget? transport = capabilities.RequiredArtifactFormat switch
        {
            FirmwareArtifactFormat.Apj => BootloaderEntryTarget.ArduPilotSerial,
            FirmwareArtifactFormat.WithBootloaderHex => BootloaderEntryTarget.Stm32RomDfu,
            _ => null
        };
        var entry = context.BootEnvironment != FirmwareBootEnvironment.None
            || context.PhysicalTarget == FirmwarePhysicalTarget.Stm32Dfu
            ? FirmwareBootEntryRequirement.AlreadyInBootloader
            : context.Runtime switch
            {
                FirmwareRuntimeKind.ArduPilot => FirmwareBootEntryRequirement.ArduPilotRebootOrManualReconnect,
                FirmwareRuntimeKind.Betaflight => FirmwareBootEntryRequirement.BetaflightMsp,
                FirmwareRuntimeKind.Unknown => FirmwareBootEntryRequirement.ManualBootReset,
                _ => FirmwareBootEntryRequirement.None
            };
        var description = context switch
        {
            { PhysicalTarget: FirmwarePhysicalTarget.None } => "Prepare firmware — no controller selected",
            { BootEnvironment: FirmwareBootEnvironment.ArduPilotBootloader } => "ArduPilot bootloader detected — install / reinstall",
            { PhysicalTarget: FirmwarePhysicalTarget.Stm32Dfu } => "STM32 DFU recovery",
            { Runtime: FirmwareRuntimeKind.Betaflight } => "Betaflight → ArduPilot conversion",
            { Runtime: FirmwareRuntimeKind.ArduPilot } => "ArduPilot firmware update / reinstall",
            _ => "Controller runtime not identified"
        };
        return new(context, capabilities, transport, entry, description);
    }
}
