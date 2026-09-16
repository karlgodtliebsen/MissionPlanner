using Microsoft.Extensions.Logging.Abstractions;
using MissionPlanner.Firmware.Betaflight;
using MissionPlanner.Firmware.Devices;
using MissionPlanner.Firmware.Dfu;
using MissionPlanner.Firmware.Installation;
using MissionPlanner.Firmware.Model;
using MissionPlanner.Firmware.Operations;
using NSubstitute;

namespace MissionPlanner.AvaloniaUI.Tests;

public sealed class BetaflightConversionScenarioTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ReviewedConversionRequiresVerifiedProgrammingBeforeProvingReturnedArduPilot(bool verified)
    {
        var identity = new BetaflightDeviceInfo("COM10", new Version(1, 46), "BTFL", McuUniqueId: "unique-mcu");
        var source = new SerialDeviceDescriptor("COM10", "source") { BetaflightIdentity = identity };
        var returned = new SerialDeviceDescriptor("COM11", "returned");
        var dfuDevice = new DfuDeviceDescriptor("usb", 0x0483, 0xdf11, DfuDriverState.PresentReady);
        var mapping = new BetaflightArduPilotMapping("TEST", "Target", "Board", "TEST", 1, "Board", 50,
            "Test fixture only", new DateOnly(2026, 1, 1));
        var entry = new FirmwareManifestEntry(new FirmwareVersion("4.6.0"), FirmwareReleaseChannel.Stable,
            new FirmwareBoardTarget(50, "Board", FirmwareVehicleType.Copter, FirmwareVehicleType.Copter),
            new FirmwareArtifact(new Uri("https://example.test/Board/arducopter.apj"), FirmwareImageFormat.Apj));
        var artifact = new DfuArtifact("arducopter_with_bl.hex", "cached.hex",
            new DfuArtifactMetadata(100, 4, 0x08000000, 0x08000003, new string('a', 64),
                [new DfuMemoryRange(0x08000000, new byte[4])], []), Platform: "Board", BoardId: 50);
        var probe = Substitute.For<IBetaflightDeviceProbe>();
        probe.ProbeAsync("COM10", Arg.Any<CancellationToken>()).Returns(new BetaflightProbeResult(BetaflightProbeOutcome.Success, identity));
        var compatibility = Substitute.For<IBetaflightArduPilotCompatibilityProvider>();
        compatibility.Resolve(identity).Returns(mapping);
        var handoff = Substitute.For<IBetaflightDfuHandoff>();
        handoff.RebootAsync(source, Arg.Any<IProgress<FirmwareProgress>?>(), Arg.Any<CancellationToken>())
            .Returns(new BetaflightDfuHandoffResult(true, "dfu.correlated", source, dfuDevice, "usb-location"));
        var artifacts = Substitute.For<IDfuArtifactResolver>();
        artifacts.ResolveAsync(Arg.Any<DfuInstallationRequest>(), Arg.Any<CancellationToken>()).Returns(artifact);
        var installer = Substitute.For<IDfuInstallationService>();
        installer.InstallAsync(Arg.Any<DfuInstallationRequest>(), Arg.Any<IProgress<DfuProgress>?>(), Arg.Any<CancellationToken>())
            .Returns(new DfuProgrammingResult(verified ? DfuOperationState.Completed : DfuOperationState.Failed,
                true, verified, false, verified ? null : new DfuFailure("dfu.verify-failed", DfuOperationState.Verifying, "Verification failed")));
        var serial = Substitute.For<IFirmwareSerialDeviceCatalog>();
        serial.GetDevicesAsync(Arg.Any<CancellationToken>()).Returns(new[] { returned });
        var dfu = Substitute.For<IDfuDeviceCatalog>();
        dfu.GetDevicesAsync(Arg.Any<CancellationToken>()).Returns(new[] { dfuDevice });
        var topology = Substitute.For<IUsbTopologyProvider>();
        topology.GetLocationAsync(Arg.Any<string?>(), Arg.Any<CancellationToken>()).Returns("usb-location");
        var verifier = Substitute.For<IArduPilotRuntimeVerifier>();
        verifier.VerifyAsync(returned, Arg.Any<CancellationToken>()).Returns(new ArduPilotRuntimeIdentity(1, 1, new Version(4, 6)));
        var connection = Substitute.For<IFirmwareConnectionGateway>();
        connection.IsVehicleConnected.Returns(true);
        connection.ActiveTransportKind.Returns(ConnectionTransportKind.Udp);
        var identities = Substitute.For<IFirmwareDeviceIdentityService>();
        var service = new BetaflightToArduPilotConversionService(probe, compatibility, handoff, artifacts, installer,
            serial, dfu, topology, verifier, connection, identities,
            new FirmwareOperationCoordinator(NullLogger<FirmwareOperationCoordinator>.Instance), TimeProvider.System);

        var result = await service.ConvertAsync(new(source, entry, true, true), cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(verified, result.Succeeded);
        Assert.Equal(verified ? "betaflight.ardupilot-verified" : "dfu.verify-failed", result.Code);
        await installer.Received(1).InstallAsync(Arg.Is<DfuInstallationRequest>(request => request.Artifact == artifact
            && request.PreviousApplicationDevice == source && request.ConfirmationPhrase == "FLASH Board"),
            Arg.Any<IProgress<DfuProgress>?>(), Arg.Any<CancellationToken>());
        await verifier.Received(verified ? 1 : 0).VerifyAsync(returned, Arg.Any<CancellationToken>());
    }
}
