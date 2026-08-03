using FluentAssertions;
using MissionPlanner.Core.Commands;
using MissionPlanner.Core.Firmware;
using MissionPlanner.Core.Vehicles.Abstractions;
using MissionPlanner.Core.Vehicles.Models;
using MissionPlanner.Firmware.Connected;
using NSubstitute;

namespace MissionPlanner.Core.Tests;

/// <summary>Verifies the MAVLink adapter used by connected bootloader updates.</summary>
public sealed class ConnectedVehicleFirmwareGatewayTests
{
    /// <summary>Verifies the ArduPilot command identifier, confirmation key, and acknowledgement mapping.</summary>
    [Theory]
    [InlineData(VehicleCommandResult.Accepted, ConnectedFirmwareCommandResult.Accepted)]
    [InlineData(VehicleCommandResult.TemporarilyRejected, ConnectedFirmwareCommandResult.TemporarilyRejected)]
    [InlineData(VehicleCommandResult.Busy, ConnectedFirmwareCommandResult.TemporarilyRejected)]
    [InlineData(VehicleCommandResult.Denied, ConnectedFirmwareCommandResult.Denied)]
    [InlineData(VehicleCommandResult.Unsupported, ConnectedFirmwareCommandResult.Unsupported)]
    [InlineData(VehicleCommandResult.Timeout, ConnectedFirmwareCommandResult.Timeout)]
    [InlineData(VehicleCommandResult.Failed, ConnectedFirmwareCommandResult.Failed)]
    public async Task FlashEmbeddedBootloaderUsesAcknowledgedExpertCommand(
        VehicleCommandResult commandResult,
        ConnectedFirmwareCommandResult expectedResult)
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var vehicleId = new VehicleId(1, 1);
        var activeVehicle = Substitute.For<IActiveVehicleContext>();
        activeVehicle.VehicleId.Returns(vehicleId);
        var commandService = Substitute.For<IVehicleCommandService>();
        commandService.ExecuteExpertAsync(Arg.Any<ExpertVehicleCommand>(), true, cancellationToken)
            .Returns(new VehicleCommandResponse(vehicleId, commandResult, DateTimeOffset.UtcNow));
        var gateway = new ConnectedVehicleFirmwareGateway(activeVehicle, commandService);

        var result = await gateway.FlashEmbeddedBootloaderAsync(cancellationToken);

        result.Should().Be(expectedResult);
        await commandService.Received(1).ExecuteExpertAsync(
            Arg.Is<ExpertVehicleCommand>(command =>
                command.VehicleId == vehicleId &&
                command.CommandId == 42650 &&
                command.Parameters is { Count: 7 } parameters &&
                parameters[4] == 290876),
            true,
            cancellationToken);
    }
}
