using Microsoft.Extensions.Logging.Abstractions;
using MissionPlanner.App.Utilities.Dispatching;
using MissionPlanner.App.Views.InitSetup.Advanced.Nmea;
using MissionPlanner.App.Views.InitSetup.Advanced.Output;
using MissionPlanner.Core.Setup.Advanced.Nmea;
using MissionPlanner.Core.Setup.Advanced.Output;
using MissionPlanner.Core.Vehicles.Abstractions;
using MissionPlanner.Library.EventHub.Abstractions;
using MissionPlanner.Test.Support;
using NSubstitute;

namespace MissionPlanner.AvaloniaUI.Tests;

public sealed class NmeaPageTests
{
    [Fact]
    public async Task ChildSubscriptionsAndPreviewTimerAreOwnedOnlyWhileActive()
    {
        var clock = new ManualTimeProvider(DateTimeOffset.UnixEpoch);
        var events = Substitute.For<IDomainEventHub>();
        var dispatcher = new InlineDispatcher();
        var factory = Substitute.For<IOutputSinkFactory>();
        var settings = new NmeaOptionsViewModel(NullLogger<NmeaOptionsViewModel>.Instance, dispatcher, events);
        var preview = new NmeaPreviewViewModel(NullLogger<NmeaPreviewViewModel>.Instance, dispatcher, events);
        var endpoint = new OutputEndpointViewModel(factory, NullLogger<OutputEndpointViewModel>.Instance, dispatcher, events);
        var status = new OutputStatusViewModel(NullLogger<OutputStatusViewModel>.Instance, dispatcher, events);
        var session = new NmeaSession(Substitute.For<IActiveVehicleContext>(), new BoundedOutputSession(factory, new(), clock), clock);
        using var model = new NmeaViewModel(settings, preview, endpoint, status, session, clock,
            NullLogger<NmeaViewModel>.Instance, dispatcher, events);
        for (var index = 0; index < 10; index++)
        {
            await model.ActivateAsync();
            await model.ActivateAsync();
            Assert.Equal(1, clock.TimerCount);
            settings.Gga = false;
            settings.Rmc = false;
            Assert.Contains("at least one", model.Message);
            settings.Gga = true;
            await model.StartCommand.ExecuteAsync(null); // Offline, so no endpoint should open.
            Assert.False(model.Running);
            Assert.False(endpoint.Locked);
            await model.DeactivateAsync();
            Assert.Equal(0, clock.TimerCount);
            var message = model.Message;
            settings.RateHz = index % 2 == 0 ? 2 : 1;
            endpoint.Address = $"127.0.0.{index + 1}";
            Assert.Equal(message, model.Message);
        }
        await factory.DidNotReceive().OpenAsync(Arg.Any<OutputEndpoint>(), Arg.Any<CancellationToken>());
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
