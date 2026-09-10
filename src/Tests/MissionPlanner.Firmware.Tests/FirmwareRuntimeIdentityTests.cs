using MissionPlanner.Firmware.Model;
using MissionPlanner.Firmware.Workflow;

namespace MissionPlanner.Firmware.Tests;

/// <summary>
/// Task 01 invariants: application-runtime identity is represented separately from exact
/// flight-controller board identity, boot environment and evidence/verification strength.
/// </summary>
public sealed class FirmwareRuntimeIdentityTests
{
    private static readonly BootloaderIdentity SampleBootloader = new(
        boardId: 9, bootloaderRevision: 5, flashSize: 2_000_000);

    [Fact]
    public void VerifiedArduPilotRuntimeCoexistsWithUnknownExactBoard()
    {
        var context = new FirmwareWorkflowContext
        {
            PhysicalTarget = FirmwarePhysicalTarget.Serial,
            Runtime = FirmwareRuntimeKind.ArduPilot,
            RuntimeVerification = FirmwareRuntimeVerification.Verified,
            BootEnvironment = FirmwareBootEnvironment.None,
            IdentityConfidence = FirmwareIdentityConfidence.Hint,
            Bootloader = null
        };

        var plan = FirmwareInstallationPlanResolver.Resolve(context);

        // The runtime remains ArduPilot even though the exact board is unresolved.
        Assert.Equal(FirmwareRuntimeKind.ArduPilot, plan.Context.Runtime);
        Assert.Null(plan.Context.Bootloader);
        Assert.NotEqual(FirmwareIdentityConfidence.Verified, plan.Context.IdentityConfidence);
        // A validated package is still required before starting automatic bootloader entry.
        Assert.False(plan.CanExecute);
        Assert.Equal("artifact.not-validated", plan.Capabilities.BlockCode);
    }

    [Fact]
    public void VerifiedBetaflightRuntimeCoexistsWithUnknownExactBoard()
    {
        var context = new FirmwareWorkflowContext
        {
            PhysicalTarget = FirmwarePhysicalTarget.Serial,
            Runtime = FirmwareRuntimeKind.Betaflight,
            BootEnvironment = FirmwareBootEnvironment.None,
            IdentityConfidence = FirmwareIdentityConfidence.Hint
        };

        var plan = FirmwareInstallationPlanResolver.Resolve(context);

        Assert.Equal(FirmwareRuntimeKind.Betaflight, plan.Context.Runtime);
        Assert.Equal(FirmwareArtifactFormat.WithBootloaderHex, plan.Capabilities.RequiredArtifactFormat);
        Assert.NotEqual(FirmwareIdentityConfidence.Verified, plan.Context.IdentityConfidence);
        Assert.False(plan.CanExecute);
    }

    [Fact]
    public void UsbHintCannotBecomeVerifiedRuntimeOrExactBoard()
    {
        var usbHint = new FirmwareRuntimeProbeResult(FirmwareRuntimeKind.Unknown, FirmwareBootEnvironment.None, "runtime.usb-hint")
        {
            Evidence = FirmwareRuntimeEvidence.UsbHint,
            Verification = FirmwareRuntimeVerification.Hinted
        };

        // A USB hint is never a verified runtime and never proves an exact board.
        Assert.NotEqual(FirmwareRuntimeVerification.Verified, usbHint.Verification);

        var context = new FirmwareWorkflowContext
        {
            PhysicalTarget = FirmwarePhysicalTarget.Serial,
            Runtime = FirmwareRuntimeKind.Unknown,
            IdentityConfidence = FirmwareIdentityConfidence.Hint,
            IdentityEvidence = "USB/product hints are not exact board proof"
        };
        var plan = FirmwareInstallationPlanResolver.Resolve(context);
        Assert.NotEqual(FirmwareIdentityConfidence.Verified, plan.Context.IdentityConfidence);
        Assert.False(plan.CanExecute);
    }

    [Fact]
    public void ArduPilotBootloaderModeCarriesExactBoardIndependentlyOfRuntime()
    {
        var context = new FirmwareWorkflowContext
        {
            PhysicalTarget = FirmwarePhysicalTarget.Serial,
            Runtime = FirmwareRuntimeKind.None,
            BootEnvironment = FirmwareBootEnvironment.ArduPilotBootloader,
            Bootloader = SampleBootloader,
            IdentityConfidence = FirmwareIdentityConfidence.Verified,
            IdentityEvidence = "ArduPilot protocol board ID 9"
        };

        var plan = FirmwareInstallationPlanResolver.Resolve(context);

        // Board identity is verified from the bootloader even though no application runtime is running.
        Assert.Equal(FirmwareRuntimeKind.None, plan.Context.Runtime);
        Assert.Equal(FirmwareIdentityConfidence.Verified, plan.Context.IdentityConfidence);
        Assert.NotNull(plan.Context.Bootloader);
        Assert.Equal(FirmwareBootEntryRequirement.AlreadyInBootloader, plan.BootEntry);
    }

    [Fact]
    public void DfuModeDoesNotInheritAnApplicationRuntime()
    {
        var context = new FirmwareWorkflowContext
        {
            PhysicalTarget = FirmwarePhysicalTarget.Stm32Dfu,
            Runtime = FirmwareRuntimeKind.None,
            BootEnvironment = FirmwareBootEnvironment.Stm32RomDfu,
            IdentityConfidence = FirmwareIdentityConfidence.Unknown
        };

        var plan = FirmwareInstallationPlanResolver.Resolve(context);

        Assert.Equal(FirmwareRuntimeKind.None, plan.Context.Runtime);
        Assert.Equal(FirmwareArtifactFormat.WithBootloaderHex, plan.Capabilities.RequiredArtifactFormat);
        // ROM DFU never establishes an exact board on its own.
        Assert.NotEqual(FirmwareIdentityConfidence.Verified, plan.Context.IdentityConfidence);
    }

    [Fact]
    public void UnknownRuntimeRetainsTypedDiagnosticOutcome()
    {
        var timedOut = new FirmwareRuntimeProbeResult(FirmwareRuntimeKind.Unknown, FirmwareBootEnvironment.None, "runtime.mavlink-timeout")
        {
            Evidence = FirmwareRuntimeEvidence.None,
            Verification = FirmwareRuntimeVerification.None
        };

        Assert.Equal(FirmwareRuntimeKind.Unknown, timedOut.Runtime);
        Assert.Equal("runtime.mavlink-timeout", timedOut.Code);
        Assert.Equal(FirmwareRuntimeVerification.None, timedOut.Verification);
    }

    [Fact]
    public void MavLinkAndMspProbesCarryDistinctVerifiedEvidence()
    {
        var ardu = new FirmwareRuntimeProbeResult(FirmwareRuntimeKind.ArduPilot, FirmwareBootEnvironment.None, "runtime.ardupilot")
        {
            Evidence = FirmwareRuntimeEvidence.MavLinkProbe,
            Verification = FirmwareRuntimeVerification.Verified
        };
        var beta = new FirmwareRuntimeProbeResult(FirmwareRuntimeKind.Betaflight, FirmwareBootEnvironment.None, "runtime.betaflight")
        {
            Evidence = FirmwareRuntimeEvidence.MspProbe,
            Verification = FirmwareRuntimeVerification.Verified
        };

        Assert.Equal(FirmwareRuntimeEvidence.MavLinkProbe, ardu.Evidence);
        Assert.Equal(FirmwareRuntimeEvidence.MspProbe, beta.Evidence);
        Assert.Equal(FirmwareRuntimeVerification.Verified, ardu.Verification);
        Assert.Equal(FirmwareRuntimeVerification.Verified, beta.Verification);
    }
}
