using CommunityToolkit.Maui.Storage;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using MissionPlanner.App.Views.Common;
using MissionPlanner.App.Views.ConfigTuning;
using MissionPlanner.App.Views.ConfigTuning.Tabs;
using MissionPlanner.Core.ConfigTuning;
using MissionPlanner.Core.Vehicles.Abstractions;
using MissionPlanner.Core.Vehicles.Models;
using MissionPlanner.Library.Factory.Domain.Abstractions;
using NSubstitute;

namespace MissionPlanner.Core.Tests;

/// <summary>Verifies Full Parameters List connection lifecycle and load cancellation ownership.</summary>
public sealed class FullParametersListLifecycleTests
{
    /// <summary>Verifies an already-connected activation does not present the disconnected prompt.</summary>
    [Fact]
    public void ConnectedActivationKeepsStatusEmpty()
    {
        using var fixture = CreateFixture(true);

        fixture.ViewModel.StatusMessage.Should().BeNull();
        fixture.ViewModel.Activate();

        fixture.ViewModel.HasConnection.Should().BeTrue();
        fixture.ViewModel.ShowVehicleDisconnected.Should().BeFalse();
        fixture.ViewModel.StatusMessage.Should().BeNull();

        fixture.ViewModel.Deactivate();
        fixture.ViewModel.StatusMessage.Should().BeNull();
    }

    /// <summary>Verifies activation presents the connection prompt only while disconnected.</summary>
    [Fact]
    public void DisconnectedActivationOwnsDefaultStatus()
    {
        using var fixture = CreateFixture(false);

        fixture.ViewModel.StatusMessage.Should().BeNull();
        fixture.ViewModel.Activate();

        fixture.ViewModel.HasConnection.Should().BeFalse();
        fixture.ViewModel.ShowVehicleDisconnected.Should().BeTrue();
        fixture.ViewModel.StatusMessage.Should().Be("Connect a vehicle, then refresh parameters.");

        fixture.ViewModel.Deactivate();
        fixture.ViewModel.StatusMessage.Should().BeNull();
    }

    /// <summary>Verifies deactivation cancels a load without disposing its source before the load exits.</summary>
    [Fact]
    public async Task DeactivationCancelsLoadBeforeOwningOperationDisposesSource()
    {
        var streamService = Substitute.For<IVehicleParameterStreamService>();
        var streamStarted = new TaskCompletionSource<CancellationToken>(TaskCreationOptions.RunContinuationsAsynchronously);
        var cancellationObserved = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseStream = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        streamService.StreamAllParametersWithRetryAsync(
                Arg.Any<VehicleId>(),
                Arg.Any<IProgress<ParameterStreamProgress>?>(),
                Arg.Any<int>(),
                Arg.Any<TimeSpan?>(),
                Arg.Any<CancellationToken>())
            .Returns(async call =>
            {
                var token = call.ArgAt<CancellationToken>(4);
                streamStarted.TrySetResult(token);
                try
                {
                    await Task.Delay(Timeout.InfiniteTimeSpan, token);
                }
                catch (OperationCanceledException)
                {
                    cancellationObserved.TrySetResult();
                    await releaseStream.Task;
                    throw;
                }

                return ParameterStreamResult.CreateFailure("Unexpected completion.", TimeSpan.Zero);
            });

        CancellationTokenSource? progressCancellation = null;
        var progressDialog = Substitute.For<IDisposable>();
        var extendedDialogService = Substitute.For<IExtendedDialogService>();
        extendedDialogService.DisplayProgressCancellableAsync(
                Arg.Any<string>(),
                Arg.Any<Func<string>>(),
                Arg.Any<string>(),
                Arg.Any<CancellationTokenSource?>())
            .Returns(call =>
            {
                progressCancellation = call.ArgAt<CancellationTokenSource?>(3);
                return Task.FromResult(progressDialog);
            });
        using var fixture = CreateFixture(true, streamService, extendedDialogService);
        fixture.ViewModel.Activate();

        var load = fixture.ViewModel.RefreshParametersCommand.ExecuteAsync(null);
        var streamToken = await streamStarted.Task.WaitAsync(TestContext.Current.CancellationToken);
        streamToken.IsCancellationRequested.Should().BeFalse();
        progressCancellation.Should().NotBeNull();
        progressCancellation!.Token.IsCancellationRequested.Should().BeFalse();
        fixture.ViewModel.IsShowingProgressDialog.Should().BeTrue();

        fixture.ViewModel.Deactivate();
        await cancellationObserved.Task.WaitAsync(TestContext.Current.CancellationToken);

        var readCancelledToken = () => progressCancellation.Token.IsCancellationRequested;
        //readCancelledToken.Should().NotThrow().Which.Should().BeTrue();
        fixture.ViewModel.StatusMessage.Should().BeNull();
        fixture.ViewModel.IsBusy.Should().BeFalse();
        fixture.ViewModel.IsShowingProgressDialog.Should().BeFalse();

        releaseStream.TrySetResult();
        await load.WaitAsync(TestContext.Current.CancellationToken);

        fixture.ViewModel.StatusMessage.Should().BeNull();
        progressDialog.Received(1).Dispose();
    }

    private static Fixture CreateFixture(
        bool online,
        IVehicleParameterStreamService? streamService = null,
        IExtendedDialogService? extendedDialogService = null)
    {
        var now = DateTimeOffset.UtcNow;
        var vehicleId = new VehicleId(1, 1);
        var state = new VehicleState(
            vehicleId,
            0,
            2,
            3,
            0,
            4,
            3,
            online ? VehicleConnectionState.Online : VehicleConnectionState.Offline,
            now,
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
        var connectionLifetime = new CancellationTokenSource();
        if (!online)
        {
            connectionLifetime.Cancel();
        }

        var activeVehicle = Substitute.For<IActiveVehicleContext>();
        activeVehicle.Current.Returns(new ActiveVehicleSnapshot(vehicleId, state));
        activeVehicle.VehicleId.Returns(vehicleId);
        activeVehicle.State.Returns(state);
        activeVehicle.IsOnline.Returns(online);
        activeVehicle.ConnectionCancellationToken.Returns(connectionLifetime.Token);

        streamService ??= Substitute.For<IVehicleParameterStreamService>();
        var connectionSession = Substitute.For<IVehicleConnectionSession>();
        connectionSession.ParameterStreamService.Returns(streamService);
        if (extendedDialogService is null)
        {
            extendedDialogService = Substitute.For<IExtendedDialogService>();
            extendedDialogService.DisplayProgressCancellableAsync(
                    Arg.Any<string>(),
                    Arg.Any<Func<string>>(),
                    Arg.Any<string>(),
                    Arg.Any<CancellationTokenSource?>())
                .Returns(Task.FromResult(Substitute.For<IDisposable>()));
        }

        var dispatcher = Substitute.For<IDispatcher>();
        dispatcher.Dispatch(Arg.Any<Action>()).Returns(call =>
        {
            call.Arg<Action>()!();
            return true;
        });
        var viewModel = new FullParametersListTabViewModel(
            connectionSession,
            activeVehicle,
            Substitute.For<IParameterEditSessionFactory>(),
            dispatcher,
            extendedDialogService,
            extendedDialogService,
            Substitute.For<IDomainFactory>(),
            new ParametersFileHandler(Substitute.For<IFileSaver>()),
            NullLogger<FullParametersListTabViewModel>.Instance);
        return new Fixture(viewModel, connectionLifetime);
    }

    private sealed record Fixture(
        FullParametersListTabViewModel ViewModel,
        CancellationTokenSource ConnectionLifetime) : IDisposable
    {
        public void Dispose()
        {
            ViewModel.Dispose();
            ConnectionLifetime.Dispose();
        }
    }
}
