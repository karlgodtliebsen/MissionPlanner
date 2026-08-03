using FluentAssertions;
using Microsoft.Extensions.Options;
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
            new FirmwareDeviceChange(FirmwareDeviceChangeKind.Arrived, application, DateTimeOffset.UtcNow)]);

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

    private static FirmwareApplicationDiscoveryService Create(
        IReadOnlyList<SerialDeviceDescriptor> snapshot,
        IReadOnlyList<FirmwareDeviceChange> changes) => new(
            new FakeCatalog(snapshot),
            new FakeMonitor(changes),
            Options.Create(new FirmwareOptions { BootloaderDiscoveryTimeout = TimeSpan.FromMilliseconds(50) }));

    private sealed class FakeCatalog(IReadOnlyList<SerialDeviceDescriptor> devices) : IFirmwareSerialDeviceCatalog
    {
        public Task<IReadOnlyList<SerialDeviceDescriptor>> GetDevicesAsync(CancellationToken cancellationToken = default) => Task.FromResult(devices);
    }

    private sealed class FakeMonitor(IReadOnlyList<FirmwareDeviceChange> changes) : IFirmwareDeviceMonitor
    {
        public async IAsyncEnumerable<FirmwareDeviceChange> WatchAsync(
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            foreach (var change in changes) yield return change;
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
        }
    }
}
