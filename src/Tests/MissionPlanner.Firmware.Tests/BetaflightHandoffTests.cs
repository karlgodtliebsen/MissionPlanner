using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using MissionPlanner.Firmware.Betaflight;
using MissionPlanner.Firmware.Configuration;
using MissionPlanner.Firmware.Devices;
using MissionPlanner.Firmware.Dfu;
using MissionPlanner.Firmware.Entry;
using MissionPlanner.Firmware.Installation;
using MissionPlanner.Firmware.Model;
using MissionPlanner.Firmware.Operations;

namespace MissionPlanner.Firmware.Tests;

public sealed class BetaflightHandoffTests
{
    [Theory]
    [InlineData(false, false, false, true)]
    [InlineData(true, false, false, true)]
    [InlineData(false, true, false, false)]
    [InlineData(false, false, true, false)]
    public async Task CorrelationRequiresUniqueNewDeviceAtSamePhysicalLocation(bool unrelated, bool ambiguous, bool preexisting, bool success)
    {
        var fixture = new Fixture();
        var a = new DfuDeviceDescriptor("A", 0x0483, 0xdf11, DfuDriverState.PresentReady);
        var b = a with { ProviderId = "B" };
        fixture.Current = unrelated || ambiguous ? [a, b] : [a];
        fixture.Before = preexisting ? [a] : [];
        fixture.Locations["A"] = "USB-1";
        fixture.Locations["B"] = ambiguous ? "USB-1" : "USB-2";
        var result = await fixture.Service().RebootAsync(fixture.Source, cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal(success, result.Succeeded);
        Assert.True(fixture.MonitorDisposed);
        if (success)
        {
            Assert.Equal("A", result.Device!.ProviderId);
            Assert.Equal("USB-1", result.PhysicalLocation);
        }
    }

    [Fact]
    public async Task MissingTopologyDoesNotRebootAndWrongTopologyDoesNotMatch()
    {
        var fixture = new Fixture();
        fixture.Locations.Clear();
        Assert.Equal("betaflight.physical-location-unavailable",
            (await fixture.Service().RebootAsync(fixture.Source, cancellationToken: TestContext.Current.CancellationToken)).Code);
        Assert.False(fixture.Rebooted);
        fixture.Locations["source"] = "USB-1";
        fixture.Locations["A"] = "USB-2";
        fixture.Current = [new("A", 0x0483, 0xdf11, DfuDriverState.PresentReady)];
        Assert.False((await fixture.Service().RebootAsync(fixture.Source, cancellationToken: TestContext.Current.CancellationToken)).Succeeded);
    }

    [Fact]
    public async Task CancellationReleasesMonitorAndOperationLease()
    {
        var fixture = new Fixture { Wait = true };
        using var cancellation = new CancellationTokenSource();
        var service = fixture.Service();
        var pending = service.RebootAsync(fixture.Source, cancellationToken: cancellation.Token);
        await fixture.Started.Task.WaitAsync(TestContext.Current.CancellationToken);
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pending);
        Assert.True(fixture.MonitorDisposed);
        using var lease = fixture.Operations.Begin(FirmwareOperationKind.ProbeFirmwareIdentity);
        lease.RequestCancellation();
    }

    private sealed class Fixture : IBootloaderEntryService, IDfuDeviceCatalog, IDfuDeviceMonitor,
        IFirmwareSerialDeviceCatalog, IUsbTopologyProvider, IFirmwareConnectionGateway
    {
        public SerialDeviceDescriptor Source { get; } = new("COM11", "source")
            { BetaflightIdentity = new("COM11", new Version(1, 46), "BTFL") };
        public Dictionary<string, string> Locations { get; } = new() { ["source"] = "USB-1" };
        public IReadOnlyList<DfuDeviceDescriptor> Before { get; set; } = [];
        public IReadOnlyList<DfuDeviceDescriptor> Current { get; set; } = [];
        public bool Rebooted { get; private set; }
        public bool MonitorDisposed { get; private set; }
        public bool Wait { get; init; }
        public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public FirmwareOperationCoordinator Operations { get; } = new(NullLogger<FirmwareOperationCoordinator>.Instance);
        public BetaflightDfuHandoff Service() => new(this, this, this, this, this, Operations, this,
            Options.Create(new DfuOptions()), TimeProvider.System);
        public Task<BootloaderEntryResult> EnterAsync(BootloaderEntryContext context, CancellationToken cancellationToken = default)
        {
            Rebooted = true;
            Assert.Equal(BootloaderEntryTarget.Stm32RomDfu, context.Target);
            return Task.FromResult(new BootloaderEntryResult(BootloaderEntryOutcome.DfuRebootInitiated, "accepted"));
        }
        Task<IReadOnlyList<DfuDeviceDescriptor>> IDfuDeviceCatalog.GetDevicesAsync(CancellationToken cancellationToken) => Task.FromResult(Rebooted ? Current : Before);
        Task<IReadOnlyList<SerialDeviceDescriptor>> IFirmwareSerialDeviceCatalog.GetDevicesAsync(CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyList<SerialDeviceDescriptor>>([]);
        public Task<string?> GetLocationAsync(string? instanceId, CancellationToken cancellationToken = default)
            => Task.FromResult(instanceId is not null && Locations.TryGetValue(instanceId, out var location) ? location : null);
        public async IAsyncEnumerable<IReadOnlyList<DfuDeviceDescriptor>> WatchAsync(
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            try
            {
                Started.TrySetResult();
                if (Wait)
                {
                    await Task.Delay(Timeout.Infinite, cancellationToken);
                }
                yield return Current;
            }
            finally
            {
                MonitorDisposed = true;
            }
        }
        public bool IsVehicleConnected => false;
        public ConnectionTransportKind? ActiveTransportKind => null;
        public Task RequestDisconnectAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}
