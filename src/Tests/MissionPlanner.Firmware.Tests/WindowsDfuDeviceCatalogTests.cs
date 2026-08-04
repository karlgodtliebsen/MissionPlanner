using FluentAssertions;
using Microsoft.Extensions.Options;
using MissionPlanner.Firmware.Dfu;

namespace MissionPlanner.Firmware.Tests;

public sealed class WindowsDfuDeviceCatalogTests
{
    [Fact]
    public async Task DefaultStm32BootloaderIsDetectedWithoutSerialPortEvidence()
    {
        var source = new FakeSnapshotSource([
            Snapshot("USB\\VID_0483&PID_DF11\\ABC", "WinUSB", friendlyName: "STM32 BOOTLOADER"),
            new WindowsDfuPnPSnapshot("USB\\VID_1234&PID_5678\\OTHER", 0x1234, 0x5678, true, DriverService: "WinUSB")]);
        var catalog = CreateCatalog(source);

        var result = await catalog.GetDevicesAsync(TestContext.Current.CancellationToken);

        result.Should().ContainSingle();
        result[0].ProductName.Should().Be("STM32 BOOTLOADER");
        result[0].SerialNumber.Should().Be("ABC");
        result[0].DriverState.Should().Be(DfuDriverState.PresentReady);
    }

    [Theory]
    [InlineData("libusb0", null, false, DfuDriverState.PresentWrongDriver)]
    [InlineData("WinUSB", 28, false, DfuDriverState.PresentWithProblem)]
    [InlineData("WinUSB", null, true, DfuDriverState.Busy)]
    [InlineData(null, null, false, DfuDriverState.Unknown)]
    public async Task DriverEvidenceMapsToDistinctState(string? service, int? problemCode, bool busy, DfuDriverState expected)
    {
        var source = new FakeSnapshotSource([Snapshot("device", service, problemCode, busy)]);

        var result = await CreateCatalog(source).GetDevicesAsync(TestContext.Current.CancellationToken);

        result.Should().ContainSingle().Which.DriverState.Should().Be(expected);
    }

    [Fact]
    public async Task ArrivalTimeIsStableUntilDeviceIsRemovedAndReturns()
    {
        var clock = new ManualTimeProvider(new DateTimeOffset(2026, 8, 4, 10, 0, 0, TimeSpan.Zero));
        var source = new FakeSnapshotSource([Snapshot("device", "WinUSB")]);
        var catalog = CreateCatalog(source, clock);
        var first = await catalog.GetDevicesAsync(TestContext.Current.CancellationToken);
        clock.Advance(TimeSpan.FromMinutes(1));
        var second = await catalog.GetDevicesAsync(TestContext.Current.CancellationToken);
        source.Snapshots = [];
        await catalog.GetDevicesAsync(TestContext.Current.CancellationToken);
        clock.Advance(TimeSpan.FromMinutes(1));
        source.Snapshots = [Snapshot("device", "WinUSB")];
        var returned = await catalog.GetDevicesAsync(TestContext.Current.CancellationToken);

        second[0].ArrivedAt.Should().Be(first[0].ArrivedAt);
        returned[0].ArrivedAt.Should().Be(clock.GetUtcNow());
        returned[0].ArrivedAt.Should().NotBe(first[0].ArrivedAt);
    }

    [Fact]
    public async Task NonPresentSnapshotIsAbsenceRatherThanWrongDriver()
    {
        var source = new FakeSnapshotSource([Snapshot("device", "libusb0") with { IsPresent = false }]);

        var result = await CreateCatalog(source).GetDevicesAsync(TestContext.Current.CancellationToken);

        result.Should().BeEmpty();
    }

    private static WindowsDfuDeviceCatalog CreateCatalog(FakeSnapshotSource source, TimeProvider? timeProvider = null) =>
        new(source, Options.Create(new DfuOptions()), timeProvider ?? TimeProvider.System);

    private static WindowsDfuPnPSnapshot Snapshot(
        string id,
        string? service,
        int? problemCode = null,
        bool busy = false,
        string? friendlyName = null) =>
        new(id, 0x0483, 0xDF11, true, FriendlyName: friendlyName, UsbSerialNumber: id.Split('\\').Last(),
            DriverService: service, DriverProvider: "STMicroelectronics", DriverVersion: "1.2.3", ProblemCode: problemCode, IsBusy: busy);

    private sealed class FakeSnapshotSource(IReadOnlyList<WindowsDfuPnPSnapshot> snapshots) : IWindowsDfuPnPSnapshotSource
    {
        public IReadOnlyList<WindowsDfuPnPSnapshot> Snapshots { get; set; } = snapshots;

        public Task<IReadOnlyList<WindowsDfuPnPSnapshot>> GetSnapshotsAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(Snapshots);
        }
    }

    private sealed class ManualTimeProvider(DateTimeOffset now) : TimeProvider
    {
        private DateTimeOffset current = now;
        public override DateTimeOffset GetUtcNow() => current;
        public void Advance(TimeSpan duration) => current += duration;
    }
}
