using Microsoft.Extensions.Logging.Abstractions;
using MissionPlanner.Core.Commands;
using MissionPlanner.Core.Setup.OptionalHardware;
using MissionPlanner.Core.Vehicles;
using MissionPlanner.Core.Vehicles.Abstractions;
using MissionPlanner.Core.Vehicles.Models;
using MissionPlanner.Library.DateTime.Domain;
using MissionPlanner.Library.EventHub.Abstractions;
using MissionPlanner.MavLink.Encoding;
using MissionPlanner.MavLink.Parameters;
using MissionPlanner.Shared.Models.Vehicles.Models;
using NSubstitute;

namespace MissionPlanner.Core.Tests;

/// <summary>Checks ESC preparation without sending motor commands or opening hardware.</summary>
public sealed class EscCalibrationPreparationTests
{
    /// <summary>Only current disarmed analog Copter preparation writes the explicit startup flag.</summary>
    [Theory]
    [InlineData(0, false, false, true, true, 3)]
    [InlineData(1, false, false, true, true, 3)]
    [InlineData(2, false, false, true, true, 3)]
    [InlineData(6, false, false, true, false, 0)]
    [InlineData(0, true, false, true, false, 0)]
    [InlineData(0, false, true, true, false, 0)]
    [InlineData(0, false, false, false, false, 0)]
    public async Task PreparationGuardsAndReadback(int protocol, bool armed, bool stale, bool confirmed, bool success, float expected)
    {
        var id = new VehicleId(1, 1);
        var now = DateTimeOffset.UtcNow;
        var state = new VehicleState(id, 0, 2, 3, 0, 4, 3, VehicleConnectionState.Online,
            stale ? now.AddSeconds(-10) : now, VehicleMode.Stabilize, armed, null, null, null, null, null, null, null, null);
        var active = Substitute.For<IActiveVehicleContext>();
        active.VehicleId.Returns(id); active.State.Returns(state); active.IsOnline.Returns(true);
        var clock = Substitute.For<IDateTimeProvider>(); clock.UtcNow.Returns(now);
        var parameters = new VehicleParameterRegistry();
        void Store(string name, float value) => parameters.StoreParameter(id, new(name, value, MavParamType.Int32, 0, 2), CancellationToken.None);
        Store("MOT_PWM_TYPE", protocol); Store("ESC_CALIBRATION", 0);
        var writes = new List<float>();
        var remote = Substitute.For<IVehicleParameterService>();
        remote.SetParameterAsync(id, "ESC_CALIBRATION", Arg.Any<float>(), MavParamType.Int32, Arg.Any<CancellationToken>()).Returns(call =>
        {
            var value = call.Arg<float>(); writes.Add(value); Store("ESC_CALIBRATION", value); return true;
        });
        var session = Substitute.For<IVehicleConnectionSession>(); session.ParameterService.Returns(remote);
        var encoder = Substitute.For<IMavLinkCommandEncoder>();
        var operations = new VehicleOperationGate();
        using var service = new ActuatorTestService(active, Substitute.For<IVehicleRegistry>(), Substitute.For<IEventHub>(), session,
            encoder, operations, parameters, clock, NullLogger<ActuatorTestService>.Instance);
        var result = await service.SetEscCalibrationPendingAsync(id, true, confirmed, TestContext.Current.CancellationToken);
        Assert.Equal(success, result.Success);
        Assert.Equal(expected, parameters.GetParameter(id, "ESC_CALIBRATION")!.Value);
        Assert.Equal(success ? 1 : 0, writes.Count);
        Assert.Empty(encoder.ReceivedCalls());
        Assert.Null(operations.GetCurrentOperation(id));
        if (success)
        {
            Assert.Contains("preparation", result.Message);
            Assert.True((await service.SetEscCalibrationPendingAsync(id, false, true, TestContext.Current.CancellationToken)).Success);
            Assert.Equal(new float[] { 3, 0 }, writes);
        }
    }
}
