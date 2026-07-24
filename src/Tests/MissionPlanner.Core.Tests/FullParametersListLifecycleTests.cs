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
using MissionPlanner.MavLink.Parameters;
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

    /// <summary>Verifies paging, page-size changes, and search project only the requested rows.</summary>
    [Fact]
    public async Task PagingProjectsOnlyTheRequestedFilteredRows()
    {
        var vehicleId = new VehicleId(1, 1);
        var fields = Enumerable.Range(1, 25)
            .Select(index => new ParameterEditField(
                $"PARAM_{index:D3}",
                MavParamType.Real32,
                index,
                index,
                index,
                ParameterFieldMetadata.Empty,
                null))
            .ToArray();
        var session = Substitute.For<IParameterEditSession>();
        session.Fields.Returns(fields);
        session.IsValid.Returns(true);
        session.Scope.Returns(new ParameterEditScope(
            vehicleId,
            new VehicleFirmwareIdentity(
                FirmwareFamily.ArduCopter,
                2,
                3,
                null,
                null,
                0,
                0,
                0,
                0,
                null,
                null)));
        session.LoadAsync(Arg.Any<IReadOnlyList<string>?>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        var editSessionFactory = Substitute.For<IParameterEditSessionFactory>();
        editSessionFactory.Create(vehicleId).Returns(session);
        var streamService = Substitute.For<IVehicleParameterStreamService>();
        streamService.StreamAllParametersWithRetryAsync(
                vehicleId,
                Arg.Any<IProgress<ParameterStreamProgress>?>(),
                Arg.Any<int>(),
                Arg.Any<TimeSpan?>(),
                Arg.Any<CancellationToken>())
            .Returns(ParameterStreamResult.CreateSuccess(
                new Dictionary<string, VehicleParameter>(),
                fields.Length,
                TimeSpan.Zero));

        using var fixture = CreateFixture(
            true,
            streamService,
            editSessionFactory: editSessionFactory);
        fixture.ViewModel.Activate();
        await fixture.ViewModel.RefreshParametersCommand.ExecuteAsync(null);

        fixture.ViewModel.CurrentPage.Should().Be(1);
        fixture.ViewModel.PageSize.Should().Be(10);
        fixture.ViewModel.TotalPageCount.Should().Be(3);
        fixture.ViewModel.FilteredParameterCount.Should().Be(25);
        fixture.ViewModel.Parameters.Select(item => item.Name)
            .Should().Equal(Enumerable.Range(1, 10).Select(index => $"PARAM_{index:D3}"));

        fixture.ViewModel.NextPageCommand.Execute(null);
        fixture.ViewModel.CurrentPage.Should().Be(2);
        fixture.ViewModel.Parameters.Select(item => item.Name)
            .Should().Equal(Enumerable.Range(11, 10).Select(index => $"PARAM_{index:D3}"));

        fixture.ViewModel.LastPageCommand.Execute(null);
        fixture.ViewModel.CurrentPage.Should().Be(3);
        fixture.ViewModel.Parameters.Select(item => item.Name)
            .Should().Equal(Enumerable.Range(21, 5).Select(index => $"PARAM_{index:D3}"));
        fixture.ViewModel.NextPageCommand.CanExecute(null).Should().BeFalse();

        fixture.ViewModel.PageSize = 7;
        fixture.ViewModel.CurrentPage.Should().Be(1);
        fixture.ViewModel.TotalPageCount.Should().Be(4);
        fixture.ViewModel.Parameters.Should().HaveCount(7);

        fixture.ViewModel.CurrentPage = 99;
        fixture.ViewModel.CurrentPage.Should().Be(4);
        fixture.ViewModel.Parameters.Select(item => item.Name)
            .Should().Equal(Enumerable.Range(22, 4).Select(index => $"PARAM_{index:D3}"));

        fixture.ViewModel.SearchText = "PARAM_02";
        fixture.ViewModel.CurrentPage.Should().Be(1);
        fixture.ViewModel.TotalPageCount.Should().Be(1);
        fixture.ViewModel.FilteredParameterCount.Should().Be(6);
        fixture.ViewModel.Parameters.Select(item => item.Name)
            .Should().Equal(Enumerable.Range(20, 6).Select(index => $"PARAM_{index:D3}"));
    }

    private static Fixture CreateFixture(
        bool online,
        IVehicleParameterStreamService? streamService = null,
        IExtendedDialogService? extendedDialogService = null,
        IParameterEditSessionFactory? editSessionFactory = null)
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
            editSessionFactory ?? Substitute.For<IParameterEditSessionFactory>(),
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
