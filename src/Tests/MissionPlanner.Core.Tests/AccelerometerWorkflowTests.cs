using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using MissionPlanner.Core.Commands;
using MissionPlanner.Core.Setup.MandatoryHardware;
using MissionPlanner.Core.Vehicles;
using MissionPlanner.Core.Vehicles.Abstractions;
using MissionPlanner.Core.Vehicles.Models;
using MissionPlanner.Library.DateTime.Domain;
using MissionPlanner.Library.EventHub.Abstractions;
using MissionPlanner.MavLink.Encoding;
using MissionPlanner.MavLink.Messages;
using MissionPlanner.MavLink.Services;
using MissionPlanner.MavLink.Services.Abstractions;
using MissionPlanner.Shared.Models.Vehicles.Models;
using MissionPlanner.Transport;
using NSubstitute;

namespace MissionPlanner.Core.Tests;

/// <summary>Exercises firmware-driven accelerometer calibration with a fake decoded MAVLink sequence.</summary>
public sealed class AccelerometerWorkflowTests
{
    /// <summary>All orientations follow firmware requests and explicit success refreshes parameters/status.</summary>
    [Fact]
    public async Task SixPositionsCompleteAndRefreshReadiness()
    {
        using var fixture = new Fixture();
        var start = fixture.Service.StartSixPositionAsync(fixture.Id, TestContext.Current.CancellationToken);
        await fixture.Position(1);
        await start;
        foreach (var orientation in new[] { 1, 3, 2, 5, 4, 6 })
        {
            await fixture.Position(orientation);
            Assert.Equal((CalibrationOrientation)orientation, fixture.Service.Current.RequiredOrientation);
            await fixture.Service.ConfirmOrientationAsync(TestContext.Current.CancellationToken);
            Assert.Equal(CalibrationWorkflowState.Sampling, fixture.Service.Current.State);
        }
        await fixture.Position(16777215);
        Assert.Equal(CalibrationWorkflowState.Success, fixture.Service.Current.State);
        Assert.Equal(6, fixture.Service.Current.CompletedOrientations.Count);
        await fixture.Parameters.Received().RequestParameterAsync(fixture.Id, "INS_ACCOFFS_X", Arg.Any<CancellationToken>());
        Assert.Contains(fixture.Commands, command => command.Command == 512 && command.Parameters[0] == 1);
        Assert.Null(fixture.Gate.GetCurrentOperation(fixture.Id));
    }

    /// <summary>Cancellation releases ownership and allows a new calibration run.</summary>
    [Fact]
    public async Task CancelAndRetry()
    {
        using var fixture = new Fixture();
        var start = fixture.Service.StartSixPositionAsync(fixture.Id, TestContext.Current.CancellationToken);
        await fixture.Position(1);
        await start;
        await fixture.Service.CancelAsync(TestContext.Current.CancellationToken);
        Assert.Equal(CalibrationWorkflowState.Cancelled, fixture.Service.Current.State);
        fixture.Service.Reset();
        var retry = fixture.Service.StartSixPositionAsync(fixture.Id, TestContext.Current.CancellationToken);
        await fixture.Position(2);
        await retry;
        Assert.Equal(CalibrationOrientation.Left, fixture.Service.Current.RequiredOrientation);
    }

    /// <summary>Missing startup evidence and firmware failure both leave retryable terminal states.</summary>
    [Fact]
    public async Task StartupTimeoutAndExplicitFailure()
    {
        using var fixture = new Fixture(TimeSpan.FromMilliseconds(30));
        await fixture.Service.StartSixPositionAsync(fixture.Id, TestContext.Current.CancellationToken);
        Assert.Equal(CalibrationWorkflowState.Failed, fixture.Service.Current.State);
        fixture.Service.Reset();
        var start = fixture.Service.StartSixPositionAsync(fixture.Id, TestContext.Current.CancellationToken);
        await fixture.Position(16777216);
        await start;
        Assert.Equal(CalibrationWorkflowState.Failed, fixture.Service.Current.State);
    }

    /// <summary>Accepted six-position calibration cannot own the vehicle operation gate indefinitely.</summary>
    [Fact]
    public async Task SixPositionDeadlineIsBounded()
    {
        using var fixture = new Fixture(sixPositionTimeout: TimeSpan.FromMilliseconds(80));
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        fixture.Service.StateChanged += args =>
        {
            if (args.Snapshot.State == CalibrationWorkflowState.Failed)
            {
                completion.TrySetResult();
            }
        };
        var start = fixture.Service.StartSixPositionAsync(fixture.Id, TestContext.Current.CancellationToken);
        await fixture.Position(1);
        await start;
        await completion.Task.WaitAsync(TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken);
        Assert.Contains("timed out", fixture.Service.Current.FailureReason);
    }

    /// <summary>Malformed orientation values cannot crash the shared decoded-message stream.</summary>
    [Fact]
    public async Task NonfiniteOrientationIsIgnored()
    {
        using var fixture = new Fixture();
        var start = fixture.Service.StartSixPositionAsync(fixture.Id, TestContext.Current.CancellationToken);
        await fixture.Position(float.NaN);
        await fixture.Position(1);
        await start;
        Assert.Equal(CalibrationOrientation.Level, fixture.Service.Current.RequiredOrientation);
    }

    private sealed class Fixture : IDisposable
    {
        private Func<MavLinkMessage, CancellationToken, Task>? receive;
        private static readonly TransportEndPoint endpoint = new("test", "accelerometer");
        public VehicleId Id { get; } = new(1, 1);
        public ArduPilotCalibrationService Service { get; }
        public IVehicleParameterService Parameters { get; } = Substitute.For<IVehicleParameterService>();
        public VehicleOperationGate Gate { get; } = new();
        public List<(ushort Command, IReadOnlyList<float> Parameters)> Commands { get; } = [];

        public Fixture(TimeSpan? startTimeout = null, TimeSpan? sixPositionTimeout = null)
        {
            var now = DateTimeOffset.UtcNow;
            var state = new VehicleState(Id, 0, 2, 3, 0, 4, 3, VehicleConnectionState.Online,
                now, VehicleMode.Stabilize, false, null, null, null, null, null, null, null, null);
            var active = Substitute.For<IActiveVehicleContext>();
            active.VehicleId.Returns(Id);
            active.IsOnline.Returns(true);
            active.State.Returns(state);
            var registry = Substitute.For<IVehicleRegistry>();
            registry.GetRequired(Id).Returns(new VehicleSession(state, endpoint, Substitute.For<IDateTimeProvider>()));
            var events = Substitute.For<IEventHub>();
            events.SubscribeAsync(MavLinkEventTopics.ReceivedMessage, Arg.Any<Func<MavLinkMessage, CancellationToken, Task>>())
                .Returns(call =>
                {
                    receive = call.Arg<Func<MavLinkMessage, CancellationToken, Task>>();
                    return Substitute.For<IDisposable>();
                });
            var connection = Substitute.For<IVehicleConnectionSession>();
            connection.Connection.Returns(Substitute.For<IMavLinkConnection>());
            var encoder = Substitute.For<IMavLinkCommandEncoder>();
            encoder.EncodeCommandLong(Arg.Any<byte>(), Arg.Any<byte>(), Arg.Any<ushort>(), Arg.Any<IReadOnlyList<float>>())
                .Returns(call =>
                {
                    Commands.Add((call.Arg<ushort>(), call.Arg<IReadOnlyList<float>>()!));
                    return new byte[] { 1 };
                });
            Service = new(active, registry, events, connection, encoder, Gate,
                Substitute.For<IVehicleMessageStore>(), new VehicleParameterRegistry(), Parameters,
                Options.Create(new CalibrationOptions
                {
                    StartTimeout = startTimeout ?? TimeSpan.FromSeconds(2),
                    SixPositionTimeout = sixPositionTimeout ?? TimeSpan.FromSeconds(10)
                }), NullLogger<ArduPilotCalibrationService>.Instance);
        }

        public Task Position(float position) => receive!(new CommandLongMessage(1, 1, endpoint,
            255, 190, 42429, 0, position, 0, 0, 0, 0, 0, 0, DateTimeOffset.UtcNow), TestContext.Current.CancellationToken);

        public void Dispose() => Service.Dispose();
    }
}
