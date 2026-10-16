using FluentAssertions;
using Microsoft.Extensions.Options;
using MissionPlanner.Firmware.Betaflight;
using MissionPlanner.Firmware.Configuration;
using MissionPlanner.Firmware.Devices;
using MissionPlanner.Firmware.Model;
using MissionPlanner.Firmware.Recovery;

namespace MissionPlanner.Firmware.Tests;

public sealed class FirmwareApplicationDiscoveryServiceTests
{
    [Fact]
    public async Task MatchesUsbSerialAcrossPortNameChange()
    {
        var bootloader = new SerialDeviceDescriptor("COM9", "boot", new UsbIdentifier(0x1209, 0x5740), "ABC", "Bootloader");
        var application = new SerialDeviceDescriptor("COM14", "app", new UsbIdentifier(0x1209, 0x5740), "ABC", "CubeOrange");
        var service = Create([], [
            new FirmwareDeviceChange(FirmwareDeviceChangeKind.Removed, bootloader, DateTimeOffset.UtcNow),
            new FirmwareDeviceChange(FirmwareDeviceChangeKind.Arrived, application, DateTimeOffset.UtcNow)
        ]);

        var result = await service.FindAsync(new FirmwareApplicationDiscoveryRequest(bootloader), TestContext.Current.CancellationToken);

        result.Should().Be(application);
        result!.PortName.Should().NotBe(bootloader.PortName);
    }

    [Fact]
    public async Task MissingApplicationReturnsNullWithoutFailing()
    {
        var bootloader = new SerialDeviceDescriptor("COM9", "boot");
        var service = Create([], []);

        var result = await service.FindAsync(
            new FirmwareApplicationDiscoveryRequest(bootloader, Timeout: TimeSpan.FromMilliseconds(20)),
            TestContext.Current.CancellationToken);

        result.Should().BeNull();
    }

    [Fact]
    public async Task MatchesUsbSerialWhenNoStableOsPathExistsAndPortNameIsReused()
    {
        var bootloader = new SerialDeviceDescriptor("COM9", usbIdentifier: new UsbIdentifier(0x1209, 0x5740), usbSerialNumber: "ABC", productName: "ArduPilot Bootloader");
        var application = new SerialDeviceDescriptor("COM9", usbIdentifier: new UsbIdentifier(0x1209, 0x5740), usbSerialNumber: "ABC", productName: "ArduPilot");
        var service = Create([], [
            new FirmwareDeviceChange(FirmwareDeviceChangeKind.Removed, bootloader, DateTimeOffset.UtcNow),
            new FirmwareDeviceChange(FirmwareDeviceChangeKind.Arrived, application, DateTimeOffset.UtcNow)
        ]);

        var result = await service.FindAsync(new FirmwareApplicationDiscoveryRequest(bootloader), TestContext.Current.CancellationToken);

        result.Should().Be(application);
    }

    [Fact]
    public async Task IgnoresUnrelatedRecentSerialArrival()
    {
        var bootloader = new SerialDeviceDescriptor("COM9", productName: "Bootloader");
        var unrelated = new SerialDeviceDescriptor("COM12", productName: "GPS Receiver");
        var service = Create([], [
            new FirmwareDeviceChange(FirmwareDeviceChangeKind.Removed, bootloader, DateTimeOffset.UtcNow),
            new FirmwareDeviceChange(FirmwareDeviceChangeKind.Arrived, unrelated, DateTimeOffset.UtcNow)
        ]);

        var result = await service.FindAsync(
            new FirmwareApplicationDiscoveryRequest(bootloader, Timeout: TimeSpan.FromMilliseconds(20)),
            TestContext.Current.CancellationToken);

        result.Should().BeNull();
    }

    [Fact]
    public async Task SamePortSnapshotNeedsNoRemovalEvent()
    {
        var bootloader = new SerialDeviceDescriptor("COM14", usbSerialNumber: "ABC");
        var application = new SerialDeviceDescriptor("COM14", usbSerialNumber: "ABC");
        var result = await Create([application], []).FindAsync(new(bootloader), TestContext.Current.CancellationToken);
        Assert.Equal(application, result);
    }

    [Fact]
    public async Task IdenticalUsbProductWithDifferentSerialNeverMatches()
    {
        var bootloader = new SerialDeviceDescriptor("COM14", usbIdentifier: new(0x1209, 0x5741), usbSerialNumber: "ABC");
        var unrelated = new SerialDeviceDescriptor("COM15", usbIdentifier: bootloader.UsbIdentifier, usbSerialNumber: "OTHER", productName: "ArduPilot");
        var result = await Create([unrelated], []).FindAsync(new(bootloader, Timeout: TimeSpan.FromMilliseconds(20)),
            TestContext.Current.CancellationToken);
        Assert.Null(result);
    }

    [Fact]
    public async Task DiscoveryHonorsCallerCancellation()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Create([], []).FindAsync(
            new(new SerialDeviceDescriptor("COM14")), cancellation.Token));
    }

    private static FirmwareApplicationDiscoveryService Create(
        IReadOnlyList<SerialDeviceDescriptor> snapshot,
        IReadOnlyList<FirmwareDeviceChange> changes,
        IUsbTopologyProvider? topology = null)
    {
        return new FirmwareApplicationDiscoveryService(
            new FakeCatalog(() => snapshot),
            new FakeMonitor(changes),
            Options.Create(new FirmwareOptions
            {
                BootloaderDiscoveryTimeout = TimeSpan.FromMilliseconds(50),
                BootloaderDiscoveryPollInterval = TimeSpan.FromMilliseconds(1)
            }), topology ?? new UnsupportedUsbTopologyProvider());
    }

    /// <summary>DFU conversion can change every USB identity field, including the serial number and COM port.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DfuRediscoveryUsesCapturedLocationAfterDelayedDriverEnumeration(bool hasPreviousApplication)
    {
        var dfu = new SerialDeviceDescriptor("DFU", "dfu", new(0x0483, 0xdf11), "ROM-SERIAL");
        var previous = new SerialDeviceDescriptor("COM9", "betaflight", new(0x0483, 0x5740), "BTFL-SERIAL");
        var application = new SerialDeviceDescriptor("COM14", "ardupilot", new(0x1209, 0x5741), "AP-SERIAL", "ArduPilot");
        var unrelated = new SerialDeviceDescriptor("COM15", "other", productName: "ArduPilot");
        var scans = 0;
        var service = new FirmwareApplicationDiscoveryService(
            new FakeCatalog(() => ++scans < 3 ? [unrelated] : [unrelated, application]),
            new FakeMonitor([]),
            Options.Create(new FirmwareOptions { BootloaderDiscoveryPollInterval = TimeSpan.FromMilliseconds(1) }),
            new Topology(new() { ["ardupilot"] = "USB-PORT-1", ["other"] = "USB-PORT-2" }));

        var result = await service.FindAsync(new(dfu, hasPreviousApplication ? previous : null, TimeSpan.FromSeconds(1))
        {
            PhysicalLocation = "USB-PORT-1"
        }, TestContext.Current.CancellationToken);

        Assert.Same(application, result);
        Assert.True(scans >= 3);
    }

    /// <summary>A matching serial number cannot override missing or conflicting physical-port evidence.</summary>
    [Theory]
    [InlineData(null)]
    [InlineData("USB-PORT-2")]
    public async Task PhysicalLocationDoesNotFallBackToAnUnrelatedDevice(string? candidateLocation)
    {
        var dfu = new SerialDeviceDescriptor("DFU", usbSerialNumber: "ABC");
        var candidate = new SerialDeviceDescriptor("COM14", "candidate", usbSerialNumber: "ABC");
        var service = Create([candidate], [], new Topology(new() { ["candidate"] = candidateLocation }));

        var result = await service.FindAsync(new(dfu, Timeout: TimeSpan.FromMilliseconds(20))
        {
            PhysicalLocation = "USB-PORT-1"
        }, TestContext.Current.CancellationToken);

        Assert.Null(result);
    }

    /// <summary>An arrival event cannot select one of multiple current interfaces at the same USB location.</summary>
    [Fact]
    public async Task AmbiguousPhysicalLocationRemainsUnmatched()
    {
        var first = new SerialDeviceDescriptor("COM14", "interface-1");
        var second = new SerialDeviceDescriptor("COM15", "interface-2");
        var service = Create([first, second],
            [new(FirmwareDeviceChangeKind.Arrived, first, DateTimeOffset.UtcNow)],
            new Topology(new() { ["interface-1"] = "USB-PORT-1", ["interface-2"] = "USB-PORT-1" }));

        var result = await service.FindAsync(new(new SerialDeviceDescriptor("DFU"), Timeout: TimeSpan.FromMilliseconds(20))
        {
            PhysicalLocation = "USB-PORT-1"
        }, TestContext.Current.CancellationToken);

        Assert.Null(result);
    }

    private sealed class Topology(Dictionary<string, string?> locations) : IUsbTopologyProvider
    {
        public Task<string?> GetLocationAsync(string? instanceId, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(instanceId is null ? null : locations.GetValueOrDefault(instanceId));
        }
    }

    private sealed class FakeCatalog(Func<IReadOnlyList<SerialDeviceDescriptor>> devices) : IFirmwareSerialDeviceCatalog
    {
        public Task<IReadOnlyList<SerialDeviceDescriptor>> GetDevicesAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(devices());
        }
    }

    private sealed class FakeMonitor(IReadOnlyList<FirmwareDeviceChange> changes) : IFirmwareDeviceMonitor
    {
        public async IAsyncEnumerable<FirmwareDeviceChange> WatchAsync(
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            foreach (var change in changes)
            {
                yield return change;
            }

            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
        }
    }
}
