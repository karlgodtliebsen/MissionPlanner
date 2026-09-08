using Microsoft.Extensions.Logging.Abstractions;
using MissionPlanner.Firmware.Betaflight;
using MissionPlanner.Firmware.Installation;
using MissionPlanner.Firmware.Model;
using MissionPlanner.Firmware.Operations;
using MissionPlanner.Test.Support;

namespace MissionPlanner.Firmware.Tests;

public sealed class BetaflightDiscoveryTests
{
    [Fact]
    public async Task PresenceCacheForceAndComReuseKeepIdentityIsolated()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }
        var probe = new Probe();
        var clock = new ManualTimeProvider(DateTimeOffset.UnixEpoch);
        var service = new FirmwareDeviceIdentityService(probe, new Connection(),
            new FirmwareOperationCoordinator(NullLogger<FirmwareOperationCoordinator>.Instance), clock);
        var device = new SerialDeviceDescriptor("COM11", "physical-A", arrivedAt: DateTimeOffset.UnixEpoch);
        var first = await service.EnrichAsync([device], cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal("BTFL", first[0].BetaflightIdentity!.FirmwareVariant);
        await service.EnrichAsync([device], cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal(1, probe.Calls);
        await service.EnrichAsync([device], true, TestContext.Current.CancellationToken);
        Assert.Equal(2, probe.Calls);
        await service.EnrichAsync([], cancellationToken: TestContext.Current.CancellationToken);
        await service.EnrichAsync([device], cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal(3, probe.Calls);
        probe.IsBetaflight = false;
        var other = new SerialDeviceDescriptor("COM11", "physical-B", arrivedAt: DateTimeOffset.UnixEpoch);
        Assert.Null((await service.EnrichAsync([other], cancellationToken: TestContext.Current.CancellationToken))[0].BetaflightIdentity);
        Assert.Equal(4, probe.Calls);
        clock.Advance(TimeSpan.FromSeconds(31));
        await service.EnrichAsync([other], cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal(5, probe.Calls);
    }

    [Fact]
    public async Task ActiveConnectionAndFirmwareOperationPreventProbes()
    {
        var probe = new Probe();
        var connection = new Connection { IsVehicleConnected = true };
        var operations = new FirmwareOperationCoordinator(NullLogger<FirmwareOperationCoordinator>.Instance);
        var service = new FirmwareDeviceIdentityService(probe, connection, operations, TimeProvider.System);
        var device = new SerialDeviceDescriptor("COM11");
        await service.EnrichAsync([device], cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal(0, probe.Calls);
        connection.IsVehicleConnected = false;
        using var lease = operations.Begin(FirmwareOperationKind.InstallApplicationFirmware);
        await service.EnrichAsync([device], cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal(0, probe.Calls);
        lease.RequestCancellation();
    }

    private sealed class Probe : IBetaflightDeviceProbe
    {
        public int Calls { get; private set; }
        public bool IsBetaflight { get; set; } = true;
        public Task<BetaflightProbeResult> ProbeAsync(string portName, CancellationToken cancellationToken = default)
        {
            Calls++;
            return Task.FromResult(IsBetaflight ? new BetaflightProbeResult(BetaflightProbeOutcome.Success,
                new(portName, new Version(1, 46), "BTFL")) : new BetaflightProbeResult(BetaflightProbeOutcome.NotMsp));
        }
    }

    private sealed class Connection : IFirmwareConnectionGateway
    {
        public bool IsVehicleConnected { get; set; }
        public ConnectionTransportKind? ActiveTransportKind => null;
        public Task RequestDisconnectAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}
