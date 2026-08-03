using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using MissionPlanner.Firmware.Connected;
using MissionPlanner.Firmware.Model;
using MissionPlanner.Firmware.Operations;

namespace MissionPlanner.Firmware.Tests;

public sealed class EmbeddedBootloaderUpdateServiceTests
{
    [Theory]
    [InlineData(ConnectedFirmwareCommandResult.Accepted, "bootloader-update.accepted", true)]
    [InlineData(ConnectedFirmwareCommandResult.TemporarilyRejected, "bootloader-update.temporarily-rejected", false)]
    [InlineData(ConnectedFirmwareCommandResult.Denied, "bootloader-update.denied", false)]
    [InlineData(ConnectedFirmwareCommandResult.Unsupported, "bootloader-update.unsupported-or-no-embedded-image", false)]
    [InlineData(ConnectedFirmwareCommandResult.Failed, "bootloader-update.failed", false)]
    [InlineData(ConnectedFirmwareCommandResult.Timeout, "bootloader-update.timeout", false)]
    public async Task SurfacesEveryCommandAckPrecisely(ConnectedFirmwareCommandResult commandResult, string code, bool rebootRequired)
    {
        var gateway = new FakeGateway { CommandResult = commandResult };
        var service = CreateService(gateway);

        var result = await service.UpdateAsync(new BootloaderUpdateRequest(true), TestContext.Current.CancellationToken);

        result.Result.Should().Be(commandResult);
        result.Code.Should().Be(code);
        result.RebootRequired.Should().Be(rebootRequired);
        gateway.Calls.Should().Be(1);
    }

    [Fact]
    public async Task ArmedVehicleCannotExecuteCommand()
    {
        var gateway = new FakeGateway { IsArmed = true };

        var result = await CreateService(gateway).UpdateAsync(new BootloaderUpdateRequest(true), TestContext.Current.CancellationToken);

        result.Code.Should().Be("bootloader-update.vehicle-armed");
        gateway.Calls.Should().Be(0);
    }

    [Theory]
    [InlineData(false, true, true, "bootloader-update.not-connected")]
    [InlineData(true, false, true, "bootloader-update.unsupported-autopilot")]
    [InlineData(true, true, false, "bootloader-update.warning-not-accepted")]
    public async Task PreconditionsBlockCommand(bool connected, bool supported, bool warning, string code)
    {
        var gateway = new FakeGateway { IsConnected = connected, IsSupportedArduPilot = supported };

        var result = await CreateService(gateway).UpdateAsync(new BootloaderUpdateRequest(warning), TestContext.Current.CancellationToken);

        result.Code.Should().Be(code);
        gateway.Calls.Should().Be(0);
    }

    private static EmbeddedBootloaderUpdateService CreateService(IConnectedVehicleFirmwareGateway gateway) =>
        new(new FirmwareOperationCoordinator(NullLogger<FirmwareOperationCoordinator>.Instance), gateway);

    private sealed class FakeGateway : IConnectedVehicleFirmwareGateway
    {
        public bool IsConnected { get; init; } = true;
        public bool IsArmed { get; init; }
        public bool IsSupportedArduPilot { get; init; } = true;
        public ConnectedFirmwareCommandResult CommandResult { get; init; } = ConnectedFirmwareCommandResult.Accepted;
        public int Calls { get; private set; }
        public Task<ConnectedFirmwareCommandResult> FlashEmbeddedBootloaderAsync(CancellationToken cancellationToken = default)
        {
            Calls++;
            return Task.FromResult(CommandResult);
        }
    }
}
