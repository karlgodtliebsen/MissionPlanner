using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using MissionPlanner.Firmware.Betaflight;
using MissionPlanner.Firmware.Installation;
using MissionPlanner.Firmware.Model;
using MissionPlanner.Firmware.Operations;
using MissionPlanner.Firmware.Workflow;

namespace MissionPlanner.Firmware.Tests;

public sealed class FirmwareRuntimeDiscoveryTests
{
    [Fact]
    public async Task ArduPilotSkipsMspAndDoesNotInferBoard()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }
        var msp = new Probe();
        var service = Create(msp, new Verifier((_, _) => Task.FromResult<ArduPilotRuntimeIdentity?>(new(1, 1, null))));
        var devices = await service.EnrichAsync([new("COM10", productName: "ArduPilot")], cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal(0, msp.Calls);
        Assert.Equal(FirmwareRuntimeKind.ArduPilot, devices[0].RuntimeProbe!.Runtime);
        Assert.Equal("Application", devices[0].RuntimeProbe!.OperatingMode);
        Assert.Null(devices[0].BootloaderIdentity);
    }

    [Fact]
    public async Task TimeoutOnEarlierPortDoesNotStarveNextDevice()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }
        var service = Create(new Probe(), new Verifier(async (device, token) =>
        {
            if (device.PortName == "COM1")
            {
                await Task.Delay(Timeout.Infinite, token);
            }
            return new(1, 1, null);
        }));
        var devices = await service.EnrichAsync([new("COM1"), new("COM10")], cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal(FirmwareRuntimeProbeOutcome.Timeout, devices[0].RuntimeProbe!.Outcome);
        Assert.Equal(FirmwareRuntimeKind.ArduPilot, devices[1].RuntimeProbe!.Runtime);
    }

    [Fact]
    public async Task InvalidatedProbeCannotApplyLateArduPilotResult()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }
        FirmwareDeviceIdentityService? service = null;
        service = Create(new Probe(), new Verifier((_, _) =>
        {
            service!.Invalidate();
            return Task.FromResult<ArduPilotRuntimeIdentity?>(new(1, 1, null));
        }));
        var devices = await service.EnrichAsync([new("COM10")], cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal(FirmwareRuntimeKind.Unknown, devices[0].RuntimeProbe!.Runtime);
    }

    [Fact]
    public async Task MavLinkTimeoutFallsBackToBetaflight()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }
        var msp = new Probe { Result = new(BetaflightProbeOutcome.Success, new("COM10", new Version(1, 0), "BTFL")) };
        var service = Create(msp, new Verifier((_, _) => Task.FromResult<ArduPilotRuntimeIdentity?>(null)));
        var devices = await service.EnrichAsync([new("COM10")], cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal(1, msp.Calls);
        Assert.Equal(FirmwareRuntimeKind.Betaflight, devices[0].RuntimeProbe!.Runtime);
        Assert.Equal("Application", devices[0].RuntimeProbe!.OperatingMode);
        Assert.Null(devices[0].BootloaderIdentity);
    }

    private static FirmwareDeviceIdentityService Create(Probe msp, IArduPilotRuntimeVerifier verifier)
    {
        return new(msp, new Connection(), new FirmwareOperationCoordinator(NullLogger<FirmwareOperationCoordinator>.Instance),
            TimeProvider.System, Options.Create(new BetaflightOptions { DiscoveryTimeout = TimeSpan.FromMilliseconds(100) }), verifier);
    }

    private sealed class Probe : IBetaflightDeviceProbe
    {
        public int Calls { get; private set; }
        public BetaflightProbeResult Result { get; init; } = new(BetaflightProbeOutcome.NotMsp);
        public Task<BetaflightProbeResult> ProbeAsync(string portName, CancellationToken cancellationToken = default)
        {
            Calls++;
            return Task.FromResult(Result);
        }
    }

    private sealed class Verifier(Func<SerialDeviceDescriptor, CancellationToken, Task<ArduPilotRuntimeIdentity?>> verify) : IArduPilotRuntimeVerifier
    {
        public Task<ArduPilotRuntimeIdentity?> VerifyAsync(SerialDeviceDescriptor device, CancellationToken cancellationToken = default)
        {
            return verify(device, cancellationToken);
        }
    }

    private sealed class Connection : IFirmwareConnectionGateway
    {
        public bool IsVehicleConnected => false;
        public ConnectionTransportKind? ActiveTransportKind => null;
        public Task RequestDisconnectAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}
