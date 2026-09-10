using MissionPlanner.Firmware.Installation;
using MissionPlanner.Firmware.Workflow;

namespace MissionPlanner.Firmware.Tests;

public sealed class FirmwareWorkflowTests
{
    [Theory]
    [InlineData(null)]
    [InlineData(ConnectionTransportKind.Udp)]
    [InlineData(ConnectionTransportKind.Tcp)]
    public void PreparationWithoutHardwareIsIndependentOfTelemetry(ConnectionTransportKind? transport)
    {
        var state = FirmwareWorkflowResolver.Resolve(new() { ActiveConnection = transport });
        Assert.True(state.CanBrowseOnlineFirmware);
        Assert.True(state.CanSelectLocalFirmware);
        Assert.True(state.CanRefreshPhysicalDevices);
        Assert.False(state.CanInstall);
        Assert.Equal("target.absent", state.BlockCode);
    }

    [Theory]
    [InlineData(FirmwareRuntimeKind.Betaflight, FirmwareBootEnvironment.None, FirmwarePhysicalTarget.Serial, FirmwareArtifactFormat.WithBootloaderHex, false, true)]
    [InlineData(FirmwareRuntimeKind.Unknown, FirmwareBootEnvironment.None, FirmwarePhysicalTarget.Serial, FirmwareArtifactFormat.None, false, false)]
    [InlineData(FirmwareRuntimeKind.ArduPilot, FirmwareBootEnvironment.None, FirmwarePhysicalTarget.Serial, FirmwareArtifactFormat.Apj, true, false)]
    [InlineData(FirmwareRuntimeKind.None, FirmwareBootEnvironment.Stm32RomDfu, FirmwarePhysicalTarget.Stm32Dfu, FirmwareArtifactFormat.WithBootloaderHex, false, false)]
    [InlineData(FirmwareRuntimeKind.None, FirmwareBootEnvironment.ArduPilotBootloader, FirmwarePhysicalTarget.Serial, FirmwareArtifactFormat.Apj, false, false)]
    public void TargetStateSelectsTransportCapabilities(FirmwareRuntimeKind runtime, FirmwareBootEnvironment boot,
        FirmwarePhysicalTarget physical, FirmwareArtifactFormat format, bool enterAp, bool enterDfu)
    {
        var state = FirmwareWorkflowResolver.Resolve(new()
        {
            PhysicalTarget = physical, Runtime = runtime, BootEnvironment = boot
        });
        Assert.Equal(format, state.RequiredArtifactFormat);
        Assert.Equal(enterAp, state.CanEnterArduPilotBootloader);
        Assert.Equal(enterDfu, state.CanEnterStm32Dfu);
        Assert.False(state.CanInstall);
    }

    [Fact]
    public void BusyAndSamePortOwnershipRetainSafetyLocks()
    {
        var context = new FirmwareWorkflowContext { PhysicalTarget = FirmwarePhysicalTarget.Serial, Runtime = FirmwareRuntimeKind.Betaflight };
        Assert.False(FirmwareWorkflowResolver.Resolve(context with { TargetPortOwned = true }).CanEnterStm32Dfu);
        Assert.False(FirmwareWorkflowResolver.Resolve(context with { OperationInProgress = true }).CanBrowseOnlineFirmware);
        Assert.False(FirmwareWorkflowResolver.Resolve(context with { TargetArmed = true }).CanEnterStm32Dfu);
    }
}
