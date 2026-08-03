using FluentAssertions;
using MissionPlanner.Firmware.Devices;
using MissionPlanner.Firmware.Model;

namespace MissionPlanner.Firmware.Tests;

public sealed class FirmwareDeviceMonitorTests
{
    [Fact]
    public async Task TracksReEnumerationAndDeduplicatesSnapshots()
    {
        var applicationCom7 = Device("COM7", "USB\\VID_2DAE&PID_1016\\APP");
        var bootloaderCom9 = Device("COM9", "USB\\VID_2DAE&PID_1016\\BOOT");
        var applicationCom8 = Device("COM8", "USB\\VID_2DAE&PID_1016\\APP");
        var catalog = new ScriptedCatalog(
            [applicationCom7],
            [],
            [bootloaderCom9, bootloaderCom9],
            [],
            [applicationCom8]);
        var monitor = new PollingFirmwareDeviceMonitor(catalog, TimeProvider.System, TimeSpan.Zero);
        var changes = new List<FirmwareDeviceChange>();

        await foreach (var change in monitor.WatchAsync(TestContext.Current.CancellationToken))
        {
            changes.Add(change);
            if (changes.Count == 4) break;
        }

        changes.Select(change => (change.Kind, change.Device.PortName)).Should().Equal(
            (FirmwareDeviceChangeKind.Removed, "COM7"),
            (FirmwareDeviceChangeKind.Arrived, "COM9"),
            (FirmwareDeviceChangeKind.Removed, "COM9"),
            (FirmwareDeviceChangeKind.Arrived, "COM8"));
    }

    [Fact]
    public async Task MonitoringEndsPromptlyOnCancellation()
    {
        var catalog = new ScriptedCatalog([], [], []);
        var monitor = new PollingFirmwareDeviceMonitor(catalog, TimeProvider.System, TimeSpan.FromMinutes(1));
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        await using var enumerator = monitor.WatchAsync(cancellation.Token).GetAsyncEnumerator(cancellation.Token);

        cancellation.Cancel();
        var act = async () => await enumerator.MoveNextAsync().AsTask();

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task ReportsModeReplacementWhenStableIdentityAndPortAreReused()
    {
        var application = new SerialDeviceDescriptor("COM7", "device-42", new UsbIdentifier(0x2dae, 0x1016), "serial", "Cube");
        var bootloader = new SerialDeviceDescriptor("COM7", "device-42", new UsbIdentifier(0x2dae, 0x1005), "serial", "Cube-BL");
        var monitor = new PollingFirmwareDeviceMonitor(
            new ScriptedCatalog([application], [bootloader]),
            TimeProvider.System,
            TimeSpan.Zero);
        var changes = new List<FirmwareDeviceChange>();

        await foreach (var change in monitor.WatchAsync(TestContext.Current.CancellationToken))
        {
            changes.Add(change);
            if (changes.Count == 2) break;
        }

        changes.Select(change => (change.Kind, change.Device.ProductName)).Should().Equal(
            (FirmwareDeviceChangeKind.Removed, "Cube"),
            (FirmwareDeviceChangeKind.Arrived, "Cube-BL"));
    }

    [Fact]
    public void StableIdentityDoesNotUseTransientPortName()
    {
        var before = Device("COM7", "device-42");
        var after = Device("COM8", "device-42");

        before.StableIdentity.Should().Be(after.StableIdentity);
        before.PortName.Should().NotBe(after.PortName);
    }

    private static SerialDeviceDescriptor Device(string port, string id) =>
        new(port, id, new UsbIdentifier(0x2dae, 0x1016), "serial", "Cube", "CubePilot");

    private sealed class ScriptedCatalog(params IReadOnlyList<SerialDeviceDescriptor>[] snapshots) : IFirmwareSerialDeviceCatalog
    {
        private int index;
        public Task<IReadOnlyList<SerialDeviceDescriptor>> GetDevicesAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var selected = snapshots[Math.Min(index, snapshots.Length - 1)];
            index++;
            return Task.FromResult(selected);
        }
    }
}
