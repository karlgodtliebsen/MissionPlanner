using Microsoft.Extensions.Logging.Abstractions;
using MissionPlanner.App.Utilities.Dispatching;
using MissionPlanner.App.Views.InitSetup.Advanced.Proximity;
using MissionPlanner.Core.Setup.Advanced.Proximity;
using MissionPlanner.Core.Vehicles.Abstractions;
using MissionPlanner.Library.EventHub.Abstractions;
using MissionPlanner.MavLink.Messages;
using MissionPlanner.MavLink.Services;
using MissionPlanner.MavLink.Services.Abstractions;
using MissionPlanner.Shared.Models.Vehicles.Models;
using MissionPlanner.Transport;
using NSubstitute;

namespace MissionPlanner.AvaloniaUI.Tests;

public sealed class ProximityPageTests
{
    [Fact]
    public async Task BurstsCoalesceAndTenPageCyclesReleaseOnlyTheirOwnObserver()
    {
        var clock = new Clock();
        var aggregate = new ProximityAggregator(clock, new());
        var tap = new MavLinkInspectionTap();
        var connection = Substitute.For<IMavLinkConnection>();
        connection.Inspection.Returns(tap);
        var vehicle = Substitute.For<IVehicleConnectionSession>();
        vehicle.Connection.Returns(connection);
        var active = Substitute.For<IActiveVehicleContext>();
        active.VehicleId.Returns(new VehicleId(1, 1));
        active.IsOnline.Returns(true);
        var dispatcher = new InlineDispatcher();
        var events = Substitute.For<IDomainEventHub>();
        var radar = new ProximityRadarViewModel(NullLogger<ProximityRadarViewModel>.Instance, dispatcher, events);
        var diagnostics = new ProximityDiagnosticsViewModel(NullLogger<ProximityDiagnosticsViewModel>.Instance, dispatcher, events);
        var session = new ProximitySession(vehicle, active, aggregate, clock);
        using var model = new ProximityViewModel(session, radar, diagnostics, clock,
            NullLogger<ProximityViewModel>.Instance, dispatcher, events);
        for (var cycle = 0; cycle < 10; cycle++)
        {
            await model.ActivateAsync();
            await model.ActivateAsync();
            Assert.True(tap.HasObservers);
            for (var sample = 0; sample < 1000; sample++)
            {
                aggregate.Observe(new DistanceSensorMessage(1, 1, new TransportEndPoint("test"),
                    0, 10, 1000, (ushort)(100 + sample % 100), 0, 1, 0, 255, 0, 0, [1, 0, 0, 0], 0, clock.GetUtcNow()));
            }
            Assert.Empty(radar.Snapshot.Points);
            var changed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            void OnChange(object? sender, System.ComponentModel.PropertyChangedEventArgs args)
            {
                if (args.PropertyName == nameof(model.Summary))
                {
                    changed.TrySetResult();
                }
            }
            model.PropertyChanged += OnChange;
            clock.Tick();
            await changed.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
            model.PropertyChanged -= OnChange;
            Assert.Equal(1.99, Assert.Single(radar.Snapshot.Points).DistanceMeters);
            Assert.Single(diagnostics.Points);
            await model.DeactivateAsync();
            Assert.False(tap.HasObservers);
            Assert.Empty(radar.Snapshot.Points);
            Assert.Empty(diagnostics.Points);
        }
        await connection.DidNotReceive().DisposeAsync();
    }

    private sealed class Clock : TimeProvider
    {
        private Timer? timer;
        public override DateTimeOffset GetUtcNow() => DateTimeOffset.Parse("2026-09-07T00:00:00Z");
        public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
        {
            Assert.Equal(TimeSpan.FromMilliseconds(250), period);
            return timer = new Timer(callback, state);
        }
        public void Tick() => timer!.Tick();
        private sealed class Timer(TimerCallback callback, object? state) : ITimer
        {
            private bool disposed;
            public void Tick() { if (!disposed) { callback(state); } }
            public bool Change(TimeSpan dueTime, TimeSpan period) => !disposed;
            public void Dispose() => disposed = true;
            public ValueTask DisposeAsync() { Dispose(); return ValueTask.CompletedTask; }
        }
    }

    private sealed class InlineDispatcher : IUiDispatcher
    {
        public bool CheckAccess() => true;
        public void Dispatch(Action action) => action();
        public T Dispatch<T>(Func<T> action) => action();
        public Task DispatchAsync(Action action) { action(); return Task.CompletedTask; }
        public Task<T> DispatchAsync<T>(Func<T> action) => Task.FromResult(action());
        public Task DispatchAsync(Func<Task> action) => action();
        public Task<T> DispatchAsync<T>(Func<Task<T>> action) => action();
    }
}
