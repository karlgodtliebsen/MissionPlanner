using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using MissionPlanner.Firmware.Dfu;
using MissionPlanner.Firmware.Exceptions;
using MissionPlanner.Firmware.Model;
using MissionPlanner.Firmware.Operations;

namespace MissionPlanner.Firmware.Tests;

public sealed class DfuContractTests
{
    [Fact]
    public void DfuContractsRemainPlatformNeutralAndMemoryRangesOwnTheirData()
    {
        var source = new byte[] { 1, 2, 3 };
        var range = new DfuMemoryRange(0x08000000, source);
        source[0] = 99;

        range.Data.ToArray().Should().Equal(1, 2, 3);
        range.EndAddress.Should().Be(0x08000002);
        typeof(IDfuProgrammer).Assembly.GetReferencedAssemblies()
            .Select(reference => reference.Name)
            .Should().NotContain(name => name != null && (name.Contains("Maui", StringComparison.OrdinalIgnoreCase) || name.Contains("WinUI", StringComparison.OrdinalIgnoreCase)));
    }

    [Fact]
    public void SerialConnectedAndDfuKindsShareOneExclusiveOperationLease()
    {
        var coordinator = new FirmwareOperationCoordinator(NullLogger<FirmwareOperationCoordinator>.Instance);
        var serial = coordinator.Begin(FirmwareOperationKind.InstallApplicationFirmware);

        var dfuWhileSerial = () => coordinator.Begin(FirmwareOperationKind.InstallApplicationAndBootloaderDfu);

        dfuWhileSerial.Should().Throw<FirmwareBusyException>();
        serial.RequestCancellation().Should().BeTrue();
        serial.Dispose();

        var dfu = coordinator.Begin(FirmwareOperationKind.InstallApplicationAndBootloaderDfu);
        var connectedWhileDfu = () => coordinator.Begin(FirmwareOperationKind.UpdateEmbeddedBootloader);

        connectedWhileDfu.Should().Throw<FirmwareBusyException>();
        dfu.RequestCancellation().Should().BeTrue();
        dfu.Dispose();
    }

    [Fact]
    public void DfuMemoryRangeRejectsEmptyAndOverflowingData()
    {
        var empty = () => new DfuMemoryRange(0x08000000, ReadOnlyMemory<byte>.Empty);
        var overflow = () => new DfuMemoryRange(uint.MaxValue, new byte[] { 1, 2 });

        empty.Should().Throw<ArgumentException>();
        overflow.Should().Throw<ArgumentOutOfRangeException>();
    }
}
