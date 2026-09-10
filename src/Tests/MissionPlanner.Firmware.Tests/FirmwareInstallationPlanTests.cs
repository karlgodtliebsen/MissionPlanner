using MissionPlanner.Firmware.Entry;
using MissionPlanner.Firmware.Workflow;

namespace MissionPlanner.Firmware.Tests;

public sealed class FirmwareInstallationPlanTests
{
    [Theory]
    [InlineData(FirmwareArtifactFormat.Apj, true)]
    [InlineData(FirmwareArtifactFormat.WithBootloaderHex, false)]
    public void ProtocolIdentifiedBootloaderAcceptsOnlyValidatedCompatibleApj(FirmwareArtifactFormat artifact, bool executable)
    {
        var context = new FirmwareWorkflowContext
        {
            PhysicalTarget = FirmwarePhysicalTarget.Serial,
            BootEnvironment = FirmwareBootEnvironment.ArduPilotBootloader,
            Bootloader = new(50, 5, 1024 * 1024),
            IdentityConfidence = FirmwareIdentityConfidence.Verified,
            ArtifactFormat = artifact, ArtifactValid = true, TargetCompatible = true
        };
        var plan = FirmwareInstallationPlanResolver.Resolve(context);
        Assert.Equal(executable, plan.CanExecute);
        Assert.Equal(BootloaderEntryTarget.ArduPilotSerial, plan.Transport);
        Assert.False(FirmwareInstallationPlanResolver.Resolve(context with { TargetCompatible = false }).CanExecute);
        Assert.False(FirmwareInstallationPlanResolver.Resolve(context with { Bootloader = null }).CanExecute);
    }

    [Theory]
    [InlineData(FirmwareArtifactFormat.WithBootloaderHex, true)]
    [InlineData(FirmwareArtifactFormat.Apj, false)]
    public void DfuRequiresCombinedHexAndTargetSafety(FirmwareArtifactFormat artifact, bool executable)
    {
        var context = new FirmwareWorkflowContext
        {
            PhysicalTarget = FirmwarePhysicalTarget.Stm32Dfu,
            BootEnvironment = FirmwareBootEnvironment.Stm32RomDfu,
            Platform = "BETAFPV-F405", ArtifactFormat = artifact, ArtifactValid = true, TargetCompatible = true, TargetSafetyConfirmed = true
        };
        Assert.Equal(executable, FirmwareInstallationPlanResolver.Resolve(context).CanExecute);
        Assert.False(FirmwareInstallationPlanResolver.Resolve(context with { Platform = null }).CanExecute);
        Assert.False(FirmwareInstallationPlanResolver.Resolve(context with { TargetCompatible = false }).CanExecute);
        Assert.False(FirmwareInstallationPlanResolver.Resolve(context with { TargetSafetyConfirmed = false }).CanExecute);
    }

    [Fact]
    public void RuntimeRequiresBootEntryAndUnknownRuntimeCannotGuessTransport()
    {
        var context = new FirmwareWorkflowContext
        {
            PhysicalTarget = FirmwarePhysicalTarget.Serial, Runtime = FirmwareRuntimeKind.Betaflight,
            ArtifactFormat = FirmwareArtifactFormat.WithBootloaderHex, ArtifactValid = true, TargetCompatible = true
        };
        var plan = FirmwareInstallationPlanResolver.Resolve(context);
        Assert.False(plan.CanExecute);
        Assert.Equal(FirmwareBootEntryRequirement.BetaflightMsp, plan.BootEntry);
        Assert.Null(FirmwareInstallationPlanResolver.Resolve(context with { Runtime = FirmwareRuntimeKind.Unknown }).Transport);
    }
}
