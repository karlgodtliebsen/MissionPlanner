using MissionPlanner.Core.Commands;
using MissionPlanner.Core.Setup.Abstractions;
using MissionPlanner.Core.Setup.OptionalHardware.Motor;
using MissionPlanner.Core.Vehicles;
using MissionPlanner.Core.Vehicles.Abstractions;
using MissionPlanner.Core.Vehicles.Models;
using MissionPlanner.MavLink.Parameters;
using MissionPlanner.Shared.Models.Vehicles.Models;
using NSubstitute;

namespace MissionPlanner.Core.Tests;

public sealed class MotorOrderTests
{
    [Fact]
    public async Task MotorTestArmedHeartbeatDoesNotEndTheRunAndSelectionWaitsForDisarm()
    {
        using var fixture = new Fixture();
        fixture.Service.Start(true);
        fixture.Actuators.TestMotorAsync(fixture.Id, Arg.Any<MotorTestRequest>(), Arg.Any<CancellationToken>()).Returns(_ =>
        {
            fixture.Active.State.Returns(fixture.State with { Flight = fixture.State.Flight with { IsArmed = true } });
            return new MotorTestResult(true, "Accepted");
        });
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        var ready = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        fixture.Service.ObservationReady += () => ready.TrySetResult();
        var pulse = fixture.Service.PulseAsync(10, cancellation.Token);
        await ready.Task.WaitAsync(TestContext.Current.CancellationToken);
        Assert.True(fixture.Service.HasSession);
        Assert.True(fixture.Service.CanObserve);
        cancellation.Cancel();
        await Task.Delay(100, TestContext.Current.CancellationToken);
        Assert.False(pulse.IsCompleted); // A stop packet alone is not disarmed telemetry.
        Assert.Throws<InvalidOperationException>(() => fixture.Service.Observe(2));
        fixture.Active.State.Returns(fixture.State);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pulse);
        fixture.Service.Observe(2);
        Assert.Equal(2, fixture.Service.Current!.OutputChannel);
        Assert.Empty(fixture.Writes);
    }

    [Fact]
    public async Task AssignmentChangeWhileWaitingForDisarmPreventsTheNextPulse()
    {
        using var fixture = new Fixture();
        fixture.Service.Start(true);
        fixture.Active.State.Returns(fixture.State with { Flight = fixture.State.Flight with { IsArmed = true } });
        var pulse = fixture.Service.PulseAsync(10, TestContext.Current.CancellationToken);
        Assert.False(pulse.IsCompleted);
        fixture.Store("SERVO1_FUNCTION", 34);
        fixture.Active.State.Returns(fixture.State);
        await Assert.ThrowsAsync<InvalidOperationException>(() => pulse);
        Assert.Empty(fixture.Pulses);
        Assert.False(fixture.Service.HasSession);
    }

    [Fact]
    public async Task PersistentArmedStateStopsTheRunWithoutAdvancingOrWriting()
    {
        using var fixture = new Fixture();
        fixture.Service.Start(true);
        fixture.Actuators.TestMotorAsync(fixture.Id, Arg.Any<MotorTestRequest>(), Arg.Any<CancellationToken>()).Returns(_ =>
        {
            fixture.Active.State.Returns(fixture.State with { Flight = fixture.State.Flight with { IsArmed = true } });
            return new MotorTestResult(true, "Accepted");
        });
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Service.PulseAsync(10, TestContext.Current.CancellationToken));
        Assert.Contains("has not reported disarmed", exception.Message);
        Assert.Throws<InvalidOperationException>(() => fixture.Service.Observe(2));
        await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Service.PulseAsync(10, TestContext.Current.CancellationToken));
        Assert.Equal(1, fixture.Service.Current!.OutputChannel);
        Assert.False((await fixture.Service.SaveAsync(cancellationToken: TestContext.Current.CancellationToken)).Success);
        Assert.Empty(fixture.Writes);
        await fixture.Actuators.Received(1).TestMotorAsync(fixture.Id, Arg.Any<MotorTestRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AcknowledgedPulseCanBeStoppedAndIdentifiedBeforeItsDurationExpires()
    {
        using var fixture = new Fixture();
        fixture.Service.Start(true);
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        var ready = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        fixture.Service.ObservationReady += () => ready.TrySetResult();
        var pulse = fixture.Service.PulseAsync(10, cancellation.Token);
        await ready.Task.WaitAsync(TestContext.Current.CancellationToken);
        Assert.True(fixture.Service.CanObserve);
        Assert.False(pulse.IsCompleted);
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pulse);
        fixture.Service.Observe(2);
        Assert.Equal(2, fixture.Service.Assignments[0].ObservedMotor!.MotorNumber);
        Assert.Equal(2, fixture.Service.Current!.OutputChannel);
        Assert.Empty(fixture.Writes);
    }

    [Theory]
    [InlineData(false, 1, 1)]
    [InlineData(true, 1, 1)]
    [InlineData(false, 12, 5)]
    public async Task ClicksMapPhysicalOutputsUsingTheExistingFunctionsAndFrameTestOrder(bool shifted, int frameType, int firstOutput)
    {
        using var fixture = new Fixture(shifted ? [34, 35, 36, 33] : [33, 34, 35, 36], frameType, firstOutput);
        fixture.Service.Start(true);
        var steps = fixture.Service.Assignments;
        var observed = new[] { 2, 3, 4, 1 };
        for (var i = 0; i < 4; i++)
        {
            await fixture.Service.PulseAsync(10, TestContext.Current.CancellationToken);
            var request = fixture.Pulses[i];
            Assert.Equal(steps[i].TestMotor.TestOrder, request.TestOrder);
            Assert.Equal(1, request.DurationSeconds);
            Assert.Equal(MotorThrottleType.Percent, request.ThrottleType);
            fixture.Service.Observe(observed[i]);
        }
        Assert.True(fixture.Service.IsComplete);
        Assert.Empty(fixture.Writes);
        var result = await fixture.Service.SaveAsync(cancellationToken: TestContext.Current.CancellationToken);
        Assert.True(result.Success, result.Message);
        Assert.Equal(new float[] { 34, 35, 36, 33 }, fixture.OutputValues());
        Assert.Equal(1, fixture.Parameters.GetParameter(fixture.Id, "SERVO12_FUNCTION")!.Value);
        Assert.False(fixture.Service.HasSession);
    }

    [Fact]
    public async Task AmbiguousAssignmentsMustBeRepairedBeforeAnyPulse()
    {
        using var fixture = new Fixture([35, 34, 34, 34]);
        fixture.Service.Start(true);
        Assert.True(fixture.Service.NeedsRepair);
        await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Service.PulseAsync(10, TestContext.Current.CancellationToken));
        Assert.Empty(fixture.Pulses);
        Assert.Empty(fixture.Writes);
        Assert.False((await fixture.Service.SaveAsync(cancellationToken: TestContext.Current.CancellationToken)).Success);
        var result = await fixture.Service.SaveAsync(repair: true, cancellationToken: TestContext.Current.CancellationToken);
        Assert.True(result.Success, result.Message);
        Assert.Equal(new float[] { 33, 34, 35, 36 }, fixture.OutputValues());
        Assert.Contains("Reboot", result.Message);
        Assert.False(fixture.Service.HasSession);
    }

    [Fact]
    public async Task RejectedPulseDoesNotAllowSelectionOrWriting()
    {
        using var fixture = new Fixture();
        fixture.Actuators.TestMotorAsync(fixture.Id, Arg.Any<MotorTestRequest>(), Arg.Any<CancellationToken>())
            .Returns(new MotorTestResult(false, "Safety switch"));
        fixture.Service.Start(true);
        await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Service.PulseAsync(10, TestContext.Current.CancellationToken));
        Assert.Throws<InvalidOperationException>(() => fixture.Service.Observe(1));
        Assert.False((await fixture.Service.SaveAsync(cancellationToken: TestContext.Current.CancellationToken)).Success);
        Assert.Empty(fixture.Writes);
    }

    [Fact]
    public async Task EachPositionCanOnlyBeSelectedOnceAndRestartDiscardsObservations()
    {
        using var fixture = new Fixture();
        fixture.Service.Start(true);
        Assert.Throws<InvalidOperationException>(() => fixture.Service.Observe(2));
        await fixture.Service.PulseAsync(10, TestContext.Current.CancellationToken);
        fixture.Service.Observe(2);
        await fixture.Service.PulseAsync(10, TestContext.Current.CancellationToken);
        Assert.Throws<InvalidOperationException>(() => fixture.Service.Observe(2));
        fixture.Service.Restart();
        Assert.All(fixture.Service.Assignments, item => Assert.Null(item.ObservedMotor));
        Assert.False(fixture.Service.CanObserve);
        Assert.Empty(fixture.Writes);
    }

    [Fact]
    public async Task CancellationStopsAnInflightPulseAndNeverWrites()
    {
        using var fixture = new Fixture();
        fixture.Service.Start(true);
        var pulse = fixture.Service.PulseAsync(10, TestContext.Current.CancellationToken);
        await fixture.Service.CancelAsync();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pulse);
        Assert.False(fixture.Service.HasSession);
        Assert.False(fixture.Service.CanObserve);
        Assert.Empty(fixture.Writes);
        await fixture.Actuators.Received().EmergencyStopAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DisconnectAndReconnectCannotReuseObservations()
    {
        using var fixture = new Fixture();
        fixture.Service.Start(true);
        var pulse = fixture.Service.PulseAsync(10, TestContext.Current.CancellationToken);
        fixture.ChangeConnection(false);
        fixture.ChangeConnection(true);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pulse);
        Assert.False(fixture.Service.HasSession);
        Assert.Throws<InvalidOperationException>(() => fixture.Service.Observe(1));
        Assert.Empty(fixture.Writes);
    }

    [Fact]
    public async Task FrameAndAssignmentChangesInvalidateTheRun()
    {
        using var fixture = new Fixture();
        fixture.Service.Start(true);
        fixture.Store("SERVO1_FUNCTION", 34);
        await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Service.PulseAsync(10, TestContext.Current.CancellationToken));
        Assert.Empty(fixture.Pulses);
        Assert.Empty(fixture.Writes);
    }

    [Fact]
    public async Task FailedSaveRestoresAllAttemptedAssignmentsIncludingTheUnacknowledgedWrite()
    {
        using var fixture = new Fixture([35, 34, 34, 34]);
        fixture.Service.Start(true);
        var calls = 0;
        fixture.Protocol.SetParameterAsync(fixture.Id, Arg.Any<string>(), Arg.Any<float>(), Arg.Any<MavParamType>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                fixture.Store(call.Arg<string>(), call.Arg<float>());
                return ++calls != 2; // Simulate a write that reached the board despite transport failure.
            });
        var result = await fixture.Service.SaveAsync(repair: true, cancellationToken: TestContext.Current.CancellationToken);
        Assert.False(result.Success);
        Assert.Contains("restored and verified", result.Message);
        Assert.Equal(new float[] { 35, 34, 34, 34 }, fixture.OutputValues());
        Assert.False(fixture.Service.HasSession);
    }

    [Fact]
    public async Task SaveRequiresFreshReadbackAndReportsIncompleteRecovery()
    {
        using var fixture = new Fixture([35, 34, 34, 34]);
        fixture.Service.Start(true);
        fixture.Protocol.RequestParameterAsync(fixture.Id, Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(false);
        var result = await fixture.Service.SaveAsync(repair: true, cancellationToken: TestContext.Current.CancellationToken);
        Assert.False(result.Success);
        Assert.Contains("Recovery could not be verified", result.Message);
        Assert.Contains("SERVO1_FUNCTION = 35", result.Message);
    }

    [Fact]
    public async Task ConnectionLossDuringSaveStopsWritesAndDoesNotRestoreToAReplacementConnection()
    {
        using var fixture = new Fixture([35, 34, 34, 34]);
        fixture.Service.Start(true);
        var writes = 0;
        fixture.Protocol.SetParameterAsync(fixture.Id, Arg.Any<string>(), Arg.Any<float>(), Arg.Any<MavParamType>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                writes++;
                fixture.Store(call.Arg<string>(), call.Arg<float>());
                fixture.ChangeConnection(false);
                fixture.ChangeConnection(true);
                return false;
            });
        var result = await fixture.Service.SaveAsync(repair: true, cancellationToken: TestContext.Current.CancellationToken);
        Assert.False(result.Success);
        Assert.Equal(1, writes);
        Assert.Contains("Recovery could not be verified", result.Message);
        Assert.False(fixture.Service.HasSession);
    }

    [Fact]
    public async Task ConcurrentOutputChangesDuringSavePreventFurtherWrites()
    {
        using var fixture = new Fixture([35, 34, 34, 34]);
        fixture.Service.Start(true);
        var writes = new List<string>();
        fixture.Protocol.SetParameterAsync(fixture.Id, Arg.Any<string>(), Arg.Any<float>(), Arg.Any<MavParamType>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                var name = call.Arg<string>();
                writes.Add(name);
                fixture.Store(name, call.Arg<float>());
                fixture.Store("SERVO2_FUNCTION", 36);
                return true;
            });
        var result = await fixture.Service.SaveAsync(repair: true, cancellationToken: TestContext.Current.CancellationToken);
        Assert.False(result.Success);
        Assert.Equal(new[] { "SERVO1_FUNCTION", "SERVO1_FUNCTION" }, writes);
        Assert.Contains("Recovery could not be verified", result.Message);
        Assert.False(fixture.Service.HasSession);
    }

    [Fact]
    public void StartingRequiresAcknowledgementDisarmedVehicleAndUnambiguousGeometry()
    {
        using var fixture = new Fixture();
        Assert.Throws<InvalidOperationException>(() => fixture.Service.Start(false));
        fixture.Active.IsOnline.Returns(false);
        Assert.Throws<InvalidOperationException>(() => fixture.Service.Start(true));
        fixture.Active.IsOnline.Returns(true);
        fixture.Active.State.Returns(fixture.State with { Flight = fixture.State.Flight with { IsArmed = true } });
        Assert.Throws<InvalidOperationException>(() => fixture.Service.Start(true));
        fixture.Active.State.Returns(fixture.State);
        fixture.Store("FRAME_CLASS", 4); // Octaquad has overlapping upper and lower motors.
        Assert.Throws<InvalidOperationException>(() => fixture.Service.Start(true));
        Assert.Empty(fixture.Pulses);
        Assert.Empty(fixture.Writes);
    }

    [Fact]
    public void IncompleteParameterDownloadCannotHideAdditionalMotorAssignments()
    {
        using var fixture = new Fixture();
        fixture.Parameters.StoreParameter(fixture.Id, new("FRAME_CLASS", 1, MavParamType.Int32, 0, 100), CancellationToken.None);
        var exception = Assert.Throws<InvalidOperationException>(() => fixture.Service.Start(true));
        Assert.Contains("complete parameter download", exception.Message);
        Assert.Empty(fixture.Pulses);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(21)]
    [InlineData(double.NaN)]
    public async Task IdentificationThrottleIsBounded(double throttle)
    {
        using var fixture = new Fixture();
        fixture.Service.Start(true);
        await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Service.PulseAsync(throttle, TestContext.Current.CancellationToken));
        Assert.Empty(fixture.Pulses);
    }

    private sealed class Fixture : IDisposable
    {
        public VehicleId Id { get; } = new(1, 1);
        public VehicleParameterRegistry Parameters { get; } = new();
        public IActiveVehicleContext Active { get; } = Substitute.For<IActiveVehicleContext>();
        public IActuatorTestService Actuators { get; } = Substitute.For<IActuatorTestService>();
        public IVehicleParameterService Protocol { get; } = Substitute.For<IVehicleParameterService>();
        public VehicleState State { get; }
        public MotorOrderService Service { get; }
        public List<MotorTestRequest> Pulses { get; } = [];
        public List<(string Name, float Value)> Writes { get; } = [];
        private readonly int firstOutput;

        public Fixture(int[]? functions = null, int frameType = 1, int firstOutput = 1)
        {
            this.firstOutput = firstOutput;
            State = new(Id, 0, 2, 3, 0, 4, 3, VehicleConnectionState.Online, DateTimeOffset.UtcNow,
                VehicleMode.Stabilize, false, null, null, null, null, null, null, null, null);
            Active.VehicleId.Returns(Id);
            Active.IsOnline.Returns(true);
            Active.State.Returns(State);
            Store("FRAME_CLASS", 1);
            Store("FRAME_TYPE", frameType);
            functions ??= [33, 34, 35, 36];
            for (var i = 0; i < 4; i++) Store($"SERVO{firstOutput + i}_FUNCTION", functions[i]);
            Store("SERVO12_FUNCTION", 1);
            Actuators.TestMotorAsync(Id, Arg.Any<MotorTestRequest>(), Arg.Any<CancellationToken>()).Returns(call =>
            {
                Pulses.Add(call.Arg<MotorTestRequest>());
                return new MotorTestResult(true, "Accepted");
            });
            Protocol.SetParameterAsync(Id, Arg.Any<string>(), Arg.Any<float>(), Arg.Any<MavParamType>(), Arg.Any<CancellationToken>()).Returns(call =>
            {
                Writes.Add((call.Arg<string>(), call.Arg<float>()));
                Store(call.Arg<string>(), call.Arg<float>());
                return true;
            });
            Protocol.RequestParameterAsync(Id, Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(call =>
            {
                var parameter = Parameters.GetParameter(Id, call.Arg<string>())!;
                Store(parameter.Name, parameter.Value);
                return true;
            });
            Service = new(Active, Parameters, Protocol, new MotorLayoutResolver(), Actuators, new VehicleOperationGate());
        }

        public void Store(string name, float value) => Parameters.StoreParameter(Id, new(name, value, MavParamType.Int32, 0, 7), CancellationToken.None);
        public float[] OutputValues() => Enumerable.Range(firstOutput, 4).Select(channel => Parameters.GetParameter(Id, $"SERVO{channel}_FUNCTION")!.Value).ToArray();
        public void ChangeConnection(bool online)
        {
            var previous = new ActiveVehicleSnapshot(Id, Active.State);
            Active.IsOnline.Returns(online);
            Active.State.Returns(State with { Connection = State.Connection with { State = online ? VehicleConnectionState.Online : VehicleConnectionState.Offline } });
            Active.Changed += Raise.Event<Action<ActiveVehicleChangedEventArgs>>(new ActiveVehicleChangedEventArgs(previous, new(Id, Active.State)));
        }
        public void Dispose() => Service.Dispose();
    }
}
