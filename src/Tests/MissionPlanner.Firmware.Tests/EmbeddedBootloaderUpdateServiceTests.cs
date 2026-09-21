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

        var result = await service.UpdateAsync(new BootloaderUpdateRequest(true) { Identity = MatchingIdentity() }, TestContext.Current.CancellationToken);

        result.Result.Should().Be(commandResult);
        result.Code.Should().Be(code);
        result.RebootRequired.Should().Be(rebootRequired);
        gateway.Calls.Should().Be(1);
    }

    [Fact]
    public async Task ArmedVehicleCannotExecuteCommand()
    {
        var gateway = new FakeGateway { IsArmed = true };

        var result = await CreateService(gateway).UpdateAsync(new BootloaderUpdateRequest(true) { Identity = MatchingIdentity() }, TestContext.Current.CancellationToken);

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

    /// <summary>Unknown, mismatched and combined-image requests cannot transmit FLASH_BOOTLOADER.</summary>
    [Theory]
    [InlineData("omnibusf4", 1002, false, true)]
    [InlineData("speedybeef4", 134, false, false)]
    [InlineData("other-target", 1002, false, false)]
    [InlineData("omnibusf4", 134, false, false)]
    [InlineData(null, 1002, false, false)]
    [InlineData("omnibusf4", 1002, true, false)]
    public async Task IdentityGuardControlsCommandDispatch(string? target, int board, bool combined, bool allowed)
    {
        var identity = MatchingIdentity();
        var gateway = new FakeGateway { RunningIdentity = identity.Running! with { Target = target, BoardId = board } };
        var result = await CreateService(gateway).UpdateAsync(new(true)
        {
            Identity = identity, UsesCombinedDfuImage = combined
        }, TestContext.Current.CancellationToken);
        Assert.Equal(allowed ? 1 : 0, gateway.Calls);
        Assert.Equal(allowed ? ConnectedFirmwareCommandResult.Accepted : ConnectedFirmwareCommandResult.Denied, result.Result);
    }

    /// <summary>A caller cannot bypass the guard by omitting selection evidence.</summary>
    [Fact]
    public async Task MissingRequestIdentityIsDenied()
    {
        var gateway = new FakeGateway();
        var result = await CreateService(gateway).UpdateAsync(new(true), TestContext.Current.CancellationToken);
        Assert.Equal(ConnectedFirmwareCommandResult.Denied, result.Result);
        Assert.Equal(0, gateway.Calls);
    }

    private static FirmwareIdentitySnapshot MatchingIdentity()
    {
        var telemetry = new VehicleFirmwareIdentity(FirmwareFamily.ArduCopter, 2, 3, null, null, 0, 1002u << 16, 0, 0, null, null);
        return new(null, null, new("omnibusf4", 1002, FirmwareVehicleType.Copter, "4.7.1", null, telemetry),
            new("omnibusf4", 1002, FirmwareVehicleType.Copter, "4.7.1", null, null, FirmwareIdentitySource.OfficialCatalogue));
    }

    private static EmbeddedBootloaderUpdateService CreateService(IConnectedVehicleFirmwareGateway gateway) =>
        new(new FirmwareOperationCoordinator(NullLogger<FirmwareOperationCoordinator>.Instance), gateway);

    private sealed class FakeGateway : IConnectedVehicleFirmwareGateway
    {
        public RunningFirmwareIdentity? RunningIdentity { get; init; } = MatchingIdentity().Running;
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
