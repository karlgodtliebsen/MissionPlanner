using FluentAssertions;
using Microsoft.Extensions.Logging;
using MissionPlanner.Core.Commands;
using MissionPlanner.Core.DomainEvents;
using MissionPlanner.Core.Setup;
using MissionPlanner.Core.Vehicles;
using MissionPlanner.Core.Vehicles.Abstractions;
using MissionPlanner.Core.Vehicles.Models;
using MissionPlanner.Library.DateTime.Domain;
using MissionPlanner.Library.EventHub.Abstractions;
using MissionPlanner.MavLink.Parameters;
using NSubstitute;
using MavParamType = MissionPlanner.MavLink.Parameters.MavParamType;

namespace MissionPlanner.Core.Tests;

/// <summary>Verifies live RC projection and the radio endpoint-calibration state machine.</summary>
public sealed class RadioSetupTests
{
    private static readonly VehicleId vehicleId = new(1, 1);

    /// <summary>Verifies channel projection maps pilot functions, endpoints, and normalized travel.</summary>
    [Fact]
    public void LiveChannelsProjectFunctionsAndTravel()
    {
        var registry = new VehicleParameterRegistry();
        Store(registry, "RC3_MIN", 1000);
        Store(registry, "RC3_MAX", 2000);
        Store(registry, "RC3_TRIM", 1500);
        var now = DateTimeOffset.UtcNow;
        var context = new TestActiveVehicleContext(StateWithChannels([1500, 1500, 1100, 1500], now));
        var service = CreateService(context, registry, now);

        var view = service.GetLiveChannels(vehicleId);

        view.IsStale.Should().BeFalse();
        view.Channels.Should().HaveCount(4);
        var throttle = view.Channels.Single(channel => channel.Number == 3);
        throttle.FunctionName.Should().Be("Throttle");
        throttle.Normalized.Should().BeApproximately(-0.8, 0.001);
    }

    /// <summary>Verifies stale RC telemetry is reported and cannot be mistaken for live input.</summary>
    [Fact]
    public void StaleTelemetryIsReported()
    {
        var now = DateTimeOffset.UtcNow;
        var context = new TestActiveVehicleContext(StateWithChannels([1500, 1500, 1500, 1500], now - TimeSpan.FromSeconds(5)));
        var service = CreateService(context, new VehicleParameterRegistry(), now);

        service.GetLiveChannels(vehicleId).IsStale.Should().BeTrue();
    }

    /// <summary>Verifies capture records extremes and writes confirmed endpoints.</summary>
    [Fact]
    public async Task CalibrationCapturesExtremesAndWritesEndpoints()
    {
        var registry = new VehicleParameterRegistry();
        var now = DateTimeOffset.UtcNow;
        var context = new TestActiveVehicleContext(StateWithChannels([1500, 1500, 1500, 1500], now));
        var written = new List<string>();
        var service = CreateService(context, registry, now, written);

        await service.StartAsync(vehicleId, TestContext.Current.CancellationToken);
        service.Current.State.Should().Be(RadioCalibrationState.Capturing);
        context.SetState(StateWithChannels([1000, 1000, 1000, 1000], now));
        context.SetState(StateWithChannels([2000, 2000, 2000, 2000], now));
        var result = await service.CompleteAsync(TestContext.Current.CancellationToken);

        result.Success.Should().BeTrue();
        service.Current.State.Should().Be(RadioCalibrationState.Success);
        written.Should().Contain("RC1_MIN").And.Contain("RC1_MAX").And.Contain("RC3_MIN").And.Contain("RC3_MAX");
        written.Should().Contain("RC1_TRIM", "sticks trim to the captured centre");
        written.Should().NotContain("RC3_TRIM", "throttle trim is left untouched");
    }

    /// <summary>Verifies insufficient stick movement blocks a confirmed write.</summary>
    [Fact]
    public async Task InsufficientMovementBlocksWrite()
    {
        var registry = new VehicleParameterRegistry();
        var now = DateTimeOffset.UtcNow;
        var context = new TestActiveVehicleContext(StateWithChannels([1500, 1500, 1500, 1500], now));
        var written = new List<string>();
        var service = CreateService(context, registry, now, written);

        await service.StartAsync(vehicleId, TestContext.Current.CancellationToken);
        context.SetState(StateWithChannels([1520, 1520, 1520, 1520], now));
        var result = await service.CompleteAsync(TestContext.Current.CancellationToken);

        result.Success.Should().BeFalse();
        service.Current.State.Should().Be(RadioCalibrationState.Failed);
        service.Current.Issues.Should().Contain(issue => issue.Severity == RadioIssueSeverity.Hazard);
        written.Should().BeEmpty();
    }

    /// <summary>Verifies a disconnect during capture leaves a recoverable state.</summary>
    [Fact]
    public async Task DisconnectDuringCaptureIsRecoverable()
    {
        var now = DateTimeOffset.UtcNow;
        var context = new TestActiveVehicleContext(StateWithChannels([1500, 1500, 1500, 1500], now));
        var service = CreateService(context, new VehicleParameterRegistry(), now);

        await service.StartAsync(vehicleId, TestContext.Current.CancellationToken);
        context.SetOnline(false);

        service.Current.State.Should().Be(RadioCalibrationState.Disconnected);
        service.Reset();
        service.Current.State.Should().Be(RadioCalibrationState.NotStarted);
    }

    private static RadioCalibrationService CreateService(TestActiveVehicleContext context, VehicleParameterRegistry registry, DateTimeOffset now, List<string>? written = null)
    {
        var clock = Substitute.For<IDateTimeProvider>();
        clock.UtcNow.Returns(now);
        var parameterService = Substitute.For<IVehicleParameterService>();
        parameterService.SetParameterAsync(vehicleId, Arg.Any<string>(), Arg.Any<float>(), Arg.Any<MavParamType>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                var name = call.ArgAt<string>(1);
                var value = call.ArgAt<float>(2);
                written?.Add(name);
                registry.StoreParameter(vehicleId, new VehicleParameter(name, value, MavParamType.Int16, 0, 1), CancellationToken.None);
                return Task.FromResult(true);
            });
        var eventHub = Substitute.For<IDomainEventHub>();
        eventHub.SubscribeDomainEventAsync(
                Arg.Do<Func<VehicleStateUpdated, CancellationToken, Task>>(handler =>
                    context.StateUpdated = state => handler(new VehicleStateUpdated(state), CancellationToken.None).GetAwaiter().GetResult()))
            .Returns(Substitute.For<IDisposable>());
        return new RadioCalibrationService(context, registry, parameterService, new VehicleOperationGate(), eventHub, clock,
            Substitute.For<ILogger<RadioCalibrationService>>());
    }

    private static void Store(VehicleParameterRegistry registry, string name, float value) =>
        registry.StoreParameter(vehicleId, new VehicleParameter(name, value, MavParamType.Int16, 0, 1), CancellationToken.None);

    private static VehicleState StateWithChannels(ushort[] channels, DateTimeOffset observedAt)
    {
        var now = DateTimeOffset.UtcNow;
        var state = new VehicleState(vehicleId, 0, 2, 3, 0, 4, 3, VehicleConnectionState.Online, now,
            VehicleMode.Stabilize, false, null, null, null, null, null, null, null, null) with
        {
            Flight = new VehicleFlightState(0, 0, 4, VehicleMode.Stabilize, false,
                LandedState: VehicleLandedState.OnGround, ObservedAt: now)
        };
        return state with { Radio = VehicleRadioState.Empty with { ChannelCount = channels.Length, ChannelsRaw = channels, ObservedAt = observedAt } };
    }

    private sealed class TestActiveVehicleContext(VehicleState state) : IActiveVehicleContext
    {
        private readonly CancellationTokenSource lifetime = new();

        public ActiveVehicleSnapshot Current { get; private set; } = new(state.VehicleId, state);

        public VehicleId? VehicleId => Current.VehicleId;

        public VehicleState? State => Current.State;

        public bool IsOnline => Current.IsOnline;

        public CancellationToken ConnectionCancellationToken => lifetime.Token;

        public event EventHandler<ActiveVehicleChangedEventArgs>? Changed;

        public Action<VehicleState>? StateUpdated { get; set; }

        public void SetState(VehicleState next)
        {
            Current = new ActiveVehicleSnapshot(next.VehicleId, next);
            StateUpdated?.Invoke(next);
        }

        public void SetOnline(bool online)
        {
            var previous = Current;
            var nextState = Current.State! with
            {
                Connection = Current.State!.Connection with
                {
                    State = online ? VehicleConnectionState.Online : VehicleConnectionState.Offline
                }
            };
            Current = new ActiveVehicleSnapshot(nextState.VehicleId, nextState);
            if (!online)
            {
                lifetime.Cancel();
            }

            Changed?.Invoke(this, new ActiveVehicleChangedEventArgs(previous, Current));
        }
    }
}
