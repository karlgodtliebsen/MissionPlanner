using CommunityToolkit.Maui.Storage;
using Microsoft.Extensions.Logging.Abstractions;
using MissionPlanner.App.Configuration;
using MissionPlanner.App.Views.ConfigTuning.Tabs;
using MissionPlanner.Core.DomainEvents;
using MissionPlanner.Core.Vehicles;
using MissionPlanner.Core.Vehicles.Abstractions;
using MissionPlanner.Core.Vehicles.Models;
using MissionPlanner.Library.DateTime.Domain;
using MissionPlanner.Library.EventHub.Abstractions;
using MissionPlanner.Transport;
using NSubstitute;

namespace MissionPlanner.Core.Tests;

/// <summary>
/// Verifies that navigation-owned MAVFTP view models release their work without affecting the next instance.
/// </summary>
public sealed class MavFtpTabLifecycleTests
{
    /// <summary>
    /// Verifies that disposal stops the delayed refresh timer and disposes the view-owned client.
    /// </summary>
    [Fact]
    public async Task DisposeStopsDelayedRefreshAndDisposesOwnedClient()
    {
        var fixture = CreateFixture();

        fixture.Timer.Received(1).Start();
        fixture.ViewModel.Dispose();

        fixture.Timer.Received(1).Stop();
        await fixture.FileSystemDisposed.Task.WaitAsync(TestContext.Current.CancellationToken);
        await fixture.FileSystem.Received(1).DisposeAsync();
        fixture.ApplicationState.Dispose();
    }

    /// <summary>
    /// Verifies that an active directory read observes cancellation before its client is disposed.
    /// </summary>
    [Fact]
    public async Task DisposeCancelsDirectoryReadBeforeDisposingClient()
    {
        var readStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var cancellationObserved = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseRead = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var fixture = CreateFixture(async cancellationToken =>
        {
            readStarted.TrySetResult();
            try
            {
                await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                cancellationObserved.TrySetResult();
                await releaseRead.Task;
                throw;
            }

            return [];
        });

        var refresh = fixture.ViewModel.RefreshCommand.ExecuteAsync(null);
        await readStarted.Task.WaitAsync(TestContext.Current.CancellationToken);

        fixture.ViewModel.Dispose();
        await cancellationObserved.Task.WaitAsync(TestContext.Current.CancellationToken);
        await fixture.FileSystem.DidNotReceive().DisposeAsync();

        releaseRead.TrySetResult();
        await refresh.WaitAsync(TestContext.Current.CancellationToken);
        await fixture.FileSystemDisposed.Task.WaitAsync(TestContext.Current.CancellationToken);
        await fixture.FileSystem.Received(1).DisposeAsync();
        fixture.ApplicationState.Dispose();
    }

    private static Fixture CreateFixture(
        Func<CancellationToken, Task<IReadOnlyList<VehicleFileSystemEntry>>>? listDirectory = null)
    {
        var vehicleId = new VehicleId(1, 1);
        var state = new VehicleState(
            vehicleId,
            0,
            2,
            3,
            0,
            4,
            3,
            VehicleConnectionState.Online,
            DateTimeOffset.UtcNow,
            VehicleMode.Stabilize,
            false,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null);
        var activeVehicle = Substitute.For<IActiveVehicleContext>();
        activeVehicle.Current.Returns(new ActiveVehicleSnapshot(vehicleId, state));
        var applicationState = new ApplicationStateService(activeVehicle);

        var session = new VehicleSession(
            state,
            new TransportEndPoint("udp", "127.0.0.1", 14550),
            Substitute.For<IDateTimeProvider>());
        var registry = Substitute.For<IVehicleRegistry>();
        registry.Vehicles.Returns([session]);

        var fileSystemDisposed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var fileSystem = Substitute.For<IVehicleFileSystemService>();
        fileSystem.DisposeAsync().Returns(_ =>
        {
            fileSystemDisposed.TrySetResult();
            return ValueTask.CompletedTask;
        });
        fileSystem.ListDirectoryAsync(
                vehicleId,
                Arg.Any<string>(),
                Arg.Any<CancellationToken>())
            .Returns(call => listDirectory is null
                ? Task.FromResult<IReadOnlyList<VehicleFileSystemEntry>>([])
                : listDirectory(call.ArgAt<CancellationToken>(2)));

        var connectionSession = Substitute.For<IVehicleConnectionSession>();
        connectionSession.CreateMavFtpConnection().Returns(fileSystem);

        var eventHub = Substitute.For<IDomainEventHub>();
        eventHub.SubscribeDomainEventAsync<VehicleConnected>(
                Arg.Any<Func<VehicleConnected, CancellationToken, Task>>())
            .Returns(Substitute.For<IDisposable>());
        eventHub.SubscribeDomainEventAsync<VehicleDisconnected>(
                Arg.Any<Func<VehicleDisconnected, CancellationToken, Task>>())
            .Returns(Substitute.For<IDisposable>());

        var timer = Substitute.For<IDispatcherTimer>();
        var dispatcher = Substitute.For<IDispatcher>();
        dispatcher.CreateTimer().Returns(timer);
        dispatcher.Dispatch(Arg.Any<Action>()).Returns(call =>
        {
            call.Arg<Action>()!.Invoke();
            return true;
        });

        var viewModel = new MavFtpTabViewModel(
            registry,
            connectionSession,
            applicationState,
            eventHub,
            Substitute.For<IFileSaver>(),
            dispatcher,
            NullLogger<MavFtpTabViewModel>.Instance);
        return new Fixture(viewModel, applicationState, fileSystem, fileSystemDisposed, timer);
    }

    private sealed record Fixture(
        MavFtpTabViewModel ViewModel,
        ApplicationStateService ApplicationState,
        IVehicleFileSystemService FileSystem,
        TaskCompletionSource FileSystemDisposed,
        IDispatcherTimer Timer);
}
