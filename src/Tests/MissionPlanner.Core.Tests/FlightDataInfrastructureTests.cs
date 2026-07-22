using FluentAssertions;
using CommunityToolkit.Maui.Storage;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MissionPlanner.App.Configuration;
using MissionPlanner.App.Presentation;
using MissionPlanner.App.Views.FlightData;
using MissionPlanner.App.Views.FlightData.Hud;
using MissionPlanner.App.Views.FlightData.Tabs;
using MissionPlanner.Core.DomainEvents;
using MissionPlanner.Core.Notifications;
using MissionPlanner.Core.Vehicles;
using MissionPlanner.Core.Vehicles.Abstractions;
using MissionPlanner.Core.Vehicles.Models;
using MissionPlanner.Library.DateTime.Domain;
using MissionPlanner.Library.EventHub.Abstractions;
using MissionPlanner.Transport;
using NSubstitute;

namespace MissionPlanner.Core.Tests;

/// <summary>
/// Verifies the shared active-vehicle and Flight Data tab lifecycle infrastructure.
/// </summary>
public sealed class FlightDataInfrastructureTests
{
    /// <summary>
    /// Verifies that effective active-vehicle changes reach a consumer exactly once.
    /// </summary>
    [Fact]
    public async Task ActiveVehicleChangeUpdatesConsumerExactlyOnce()
    {
        var fixture = CreateContextFixture();
        using var context = fixture.Context;
        var changes = new List<ActiveVehicleSnapshot>();
        context.Changed += (_, args) => changes.Add(args.Current);

        await fixture.Connected!(new VehicleConnected(fixture.Session.Id, "UDP", "14550", DateTimeOffset.UtcNow), TestContext.Current.CancellationToken);
        await fixture.Updated!(new VehicleStateUpdated(fixture.Session.State), TestContext.Current.CancellationToken);

        changes.Should().ContainSingle();
        changes[0].VehicleId.Should().Be(fixture.Session.Id);
        changes[0].State.Should().Be(fixture.Session.State);
    }

    /// <summary>
    /// Verifies that disconnecting cancels a running operation and retains an immutable offline snapshot.
    /// </summary>
    [Fact]
    public async Task DisconnectCancelsOperationAndRetainsOfflineSnapshot()
    {
        var fixture = CreateContextFixture();
        using var context = fixture.Context;
        await fixture.Connected!(new VehicleConnected(fixture.Session.Id, "UDP", "14550", DateTimeOffset.UtcNow), TestContext.Current.CancellationToken);
        var runner = new AsyncOperationRunner(context);
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var operation = runner.RunAsync(
            async (_, token) =>
            {
                started.SetResult();
                await Task.Delay(Timeout.InfiniteTimeSpan, token);
                return AsyncOperationState.Success();
            },
            cancellationToken: TestContext.Current.CancellationToken);

        await started.Task;
        await fixture.Disconnected!(new VehicleDisconnected(fixture.Session.Id, DateTimeOffset.UtcNow), TestContext.Current.CancellationToken);
        var result = await operation;

        result.Status.Should().Be(AsyncOperationStatus.Disconnected);
        context.State.Should().NotBeNull();
        context.State!.ConnectionState.Should().Be(VehicleConnectionState.Offline);
        context.VehicleId.Should().Be(fixture.Session.Id);
    }

    /// <summary>
    /// Verifies that reconnecting replaces and disposes work tied to the previous connection lifetime.
    /// </summary>
    [Fact]
    public async Task ReconnectDoesNotRetainDisposedConnectionWork()
    {
        var fixture = CreateContextFixture();
        using var context = fixture.Context;
        await fixture.Connected!(new VehicleConnected(fixture.Session.Id, "UDP", "14550", DateTimeOffset.UtcNow), TestContext.Current.CancellationToken);
        var starts = 0;
        var disposals = 0;
        await using var lifecycle = new FlightDataTabLifecycle(
            "Test",
            context,
            startAsync: _ =>
            {
                starts++;
                return Task.FromResult<IDisposable?>(new CallbackDisposable(() => disposals++));
            });

        await lifecycle.ActivateAsync(TestContext.Current.CancellationToken);
        await fixture.Disconnected!(new VehicleDisconnected(fixture.Session.Id, DateTimeOffset.UtcNow), TestContext.Current.CancellationToken);
        await lifecycle.WhenSettledAsync();
        await fixture.Connected!(new VehicleConnected(fixture.Session.Id, "UDP", "14550", DateTimeOffset.UtcNow), TestContext.Current.CancellationToken);
        await lifecycle.WhenSettledAsync();

        starts.Should().Be(2);
        disposals.Should().Be(1);
        await lifecycle.DeactivateAsync();
        disposals.Should().Be(2);
    }

    /// <summary>
    /// Verifies deterministic lazy initialization and activation/deactivation behavior.
    /// </summary>
    [Fact]
    public async Task TabLifecycleStartsAndStopsDeterministically()
    {
        var fixture = CreateContextFixture();
        using var context = fixture.Context;
        await fixture.Connected!(new VehicleConnected(fixture.Session.Id, "UDP", "14550", DateTimeOffset.UtcNow), TestContext.Current.CancellationToken);
        var initializations = 0;
        var starts = 0;
        var disposals = 0;
        await using var lifecycle = new FlightDataTabLifecycle(
            "Test",
            context,
            _ =>
            {
                initializations++;
                return Task.CompletedTask;
            },
            _ =>
            {
                starts++;
                return Task.FromResult<IDisposable?>(new CallbackDisposable(() => disposals++));
            });

        await lifecycle.ActivateAsync(TestContext.Current.CancellationToken);
        await lifecycle.ActivateAsync(TestContext.Current.CancellationToken);
        await lifecycle.DeactivateAsync();
        await lifecycle.DeactivateAsync();
        await lifecycle.ActivateAsync(TestContext.Current.CancellationToken);

        initializations.Should().Be(1);
        starts.Should().Be(2);
        disposals.Should().Be(1);
        lifecycle.IsActive.Should().BeTrue();
        lifecycle.IsInitialized.Should().BeTrue();
    }

    /// <summary>
    /// Verifies that the application dependency graph resolves every current Flight Data view model and shared service.
    /// </summary>
    [Fact]
    public async Task DependencyInjectionResolvesFlightDataViewModels()
    {
        var values = new Dictionary<string, string?>
        {
            ["ApplicationSettings:Channel"] = "UDP",
            ["ApplicationSettings:BaudRate"] = "115200",
            ["ApplicationSettings:Host"] = "127.0.0.1",
            ["ApplicationSettings:Port"] = "14550"
        };
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(values).Build();
        var services = new ServiceCollection();
        services.AddApplicationConfiguration(configuration);
        services.AddSingleton(Substitute.For<IDispatcher>());
        services.AddSingleton(Substitute.For<IFileSaver>());
        await using var provider = services.BuildServiceProvider();

        provider.GetRequiredService<IActiveVehicleContext>().Should().NotBeNull();
        provider.GetRequiredService<IUserNotificationService>().Should().NotBeNull();
        provider.GetRequiredService<IApplicationNotificationStore>().Should().NotBeNull();
        provider.GetRequiredService<IVehicleMessageStore>().Should().NotBeNull();
        provider.GetRequiredService<ITextClipboardService>().Should().NotBeNull();
        provider.GetRequiredService<IUserConfirmationService>().Should().NotBeNull();
        provider.GetRequiredService<AsyncOperationRunner>().Should().NotBeNull();
        provider.GetRequiredService<FlightDataViewModel>().Should().NotBeNull();
        provider.GetRequiredService<HudViewModel>().Should().NotBeNull();
        provider.GetRequiredService<QuickTabViewModel>().Should().NotBeNull();
        provider.GetRequiredService<ActionsTabViewModel>().Should().NotBeNull();
        provider.GetRequiredService<MessagesTabViewModel>().Should().NotBeNull();
        provider.GetRequiredService<PreflightTabViewModel>().Should().NotBeNull();
        provider.GetRequiredService<GaugesTabViewModel>().Should().NotBeNull();
        provider.GetRequiredService<TransponderTabViewModel>().Should().NotBeNull();
        provider.GetRequiredService<StatusTabViewModel>().Should().NotBeNull();
        provider.GetRequiredService<ServoRelayTabViewModel>().Should().NotBeNull();
        provider.GetRequiredService<AuxFunctionTabViewModel>().Should().NotBeNull();
        provider.GetRequiredService<ScriptsTabViewModel>().Should().NotBeNull();
        provider.GetRequiredService<PayloadControlTabViewModel>().Should().NotBeNull();
        provider.GetRequiredService<TelemetryLogsTabViewModel>().Should().NotBeNull();
        provider.GetRequiredService<DataFlashLogsTabViewModel>().Should().NotBeNull();
    }

    private static ContextFixture CreateContextFixture()
    {
        Func<VehicleConnected, CancellationToken, Task>? connected = null;
        Func<VehicleDisconnected, CancellationToken, Task>? disconnected = null;
        Func<VehicleStateUpdated, CancellationToken, Task>? updated = null;
        var eventHub = Substitute.For<IDomainEventHub>();
        eventHub.SubscribeDomainEventAsync(Arg.Do<Func<VehicleConnected, CancellationToken, Task>>(handler => connected = handler))
            .Returns(Substitute.For<IDisposable>());
        eventHub.SubscribeDomainEventAsync(Arg.Do<Func<VehicleDisconnected, CancellationToken, Task>>(handler => disconnected = handler))
            .Returns(Substitute.For<IDisposable>());
        eventHub.SubscribeDomainEventAsync(Arg.Do<Func<VehicleStateUpdated, CancellationToken, Task>>(handler => updated = handler))
            .Returns(Substitute.For<IDisposable>());
        eventHub.SubscribeDomainEventAsync(Arg.Any<Func<VehicleRegistryReset, CancellationToken, Task>>())
            .Returns(Substitute.For<IDisposable>());

        var now = DateTimeOffset.UtcNow;
        var state = new VehicleState(new VehicleId(1, 1), 0, 2, 3, 0, 4, 3, VehicleConnectionState.Online, now, VehicleMode.Unknown, false, null, null, null, null, null, null, null, null);
        var clock = Substitute.For<IDateTimeProvider>();
        clock.UtcNow.Returns(now);
        var session = new VehicleSession(state, new TransportEndPoint("test"), clock);
        var registry = Substitute.For<IVehicleRegistry>();
        registry.GetRequired(session.Id).Returns(session);
        var context = new ActiveVehicleContext(eventHub, registry);
        return new ContextFixture(context, session, connected, disconnected, updated);
    }

    private sealed record ContextFixture(
        ActiveVehicleContext Context,
        VehicleSession Session,
        Func<VehicleConnected, CancellationToken, Task>? Connected,
        Func<VehicleDisconnected, CancellationToken, Task>? Disconnected,
        Func<VehicleStateUpdated, CancellationToken, Task>? Updated);

    private sealed class CallbackDisposable(Action callback) : IDisposable
    {
        private bool disposed;

        public void Dispose()
        {
            if (disposed)
            {
                return;
            }

            disposed = true;
            callback();
        }
    }
}
