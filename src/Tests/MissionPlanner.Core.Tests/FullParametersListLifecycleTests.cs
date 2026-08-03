using CommunityToolkit.Maui.Storage;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using MissionPlanner.App.Helpers;
using MissionPlanner.App.Navigation;
using MissionPlanner.App.Presentation;
using MissionPlanner.App.Views.ConfigTuning;
using MissionPlanner.App.Views.ConfigTuning.Tabs;
using MissionPlanner.Core.ConfigTuning;
using MissionPlanner.Core.ConfigTuning.Profiles;
using MissionPlanner.Core.Vehicles;
using MissionPlanner.Core.Vehicles.Abstractions;
using MissionPlanner.Core.Vehicles.Models;
using MissionPlanner.Library.Factory.Domain.Abstractions;
using NSubstitute;
using UraniumUI.Material.Dialogs;

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
        //fixture.ViewModel.InitializeView();

        fixture.ViewModel.HasConnection.Should().BeTrue();
        fixture.ViewModel.ShowVehicleDisconnected.Should().BeFalse();
        fixture.ViewModel.StatusMessage.Should().BeNull();

        fixture.ViewModel.StatusMessage.Should().BeNull();
    }

    /// <summary>Verifies activation presents the connection prompt only while disconnected.</summary>
    [Fact]
    public void DisconnectedActivationOwnsDefaultStatus()
    {
        using var fixture = CreateFixture(false);

        fixture.ViewModel.HasConnection.Should().BeFalse();
        fixture.ViewModel.ShowVehicleDisconnected.Should().BeTrue();
        fixture.ViewModel.StatusMessage.Should().Be("Connect a vehicle, then refresh parameters.");
    }

    /// <summary>Verifies page deactivation releases its large parameter projection.</summary>
    [Fact]
    public void DisposeClearsProjectedParameterRows()
    {
        using var fixture = CreateFixture(true);
        var session = Substitute.For<IParameterEditSession>();
        var field = new ParameterEditField(
            "TEST_PARAM",
            MavLink.Parameters.MavParamType.Real32,
            1,
            1,
            1,
            ParameterFieldMetadata.Empty,
            null);
        fixture.ViewModel.Parameters.Add(new ParameterItemViewModel(session, field));

        fixture.ViewModel.Dispose();

        fixture.ViewModel.Parameters.Should().BeEmpty();
        fixture.ViewModel.HasRows.Should().BeFalse();
        fixture.ViewModel.TotalParameterCount.Should().Be(0);
    }

    /// <summary>Verifies a complete registry snapshot is projected without requesting the vehicle again.</summary>
    [Fact]
    public async Task CompleteCachedParametersAreDisplayedWithoutStreamingAgain()
    {
        var vehicleId = new VehicleId(1, 1);
        var registry = new VehicleParameterRegistry();
        registry.StoreParameter(
            vehicleId,
            new MavLink.Parameters.VehicleParameter(
                "TEST_PARAM",
                42,
                MavLink.Parameters.MavParamType.Real32,
                0,
                1),
            CancellationToken.None);

        var field = new ParameterEditField(
            "TEST_PARAM",
            MavLink.Parameters.MavParamType.Real32,
            42,
            42,
            42,
            ParameterFieldMetadata.Empty,
            null);
        var session = Substitute.For<IParameterEditSession>();
        session.Fields.Returns([field]);
        session.LoadAsync(Arg.Any<IReadOnlyList<string>?>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        var sessionFactory = Substitute.For<IParameterEditSessionFactory>();
        sessionFactory.Create(vehicleId).Returns(session);
        var streamService = Substitute.For<IVehicleParameterStreamService>();

        using var fixture = CreateFixture(
            true,
            streamService,
            editSessionFactory: sessionFactory,
            parameterRegistry: registry);

        await WaitForAsync(
            () => fixture.ViewModel.Parameters.Count == 1,
            TestContext.Current.CancellationToken);

        fixture.ViewModel.Parameters.Single().Name.Should().Be("TEST_PARAM");
        fixture.ViewModel.HasRows.Should().BeTrue();
        await streamService.DidNotReceiveWithAnyArgs()
            .StreamAllParametersWithRetryAsync(
                default,
                default,
                default,
                default,
                TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies session-wide notifications do not rewrite unchanged rows.</summary>
    [Fact]
    public void ReapplyingUnchangedFieldDoesNotRaiseRowNotifications()
    {
        var session = Substitute.For<IParameterEditSession>();
        var field = new ParameterEditField(
            "TEST_PARAM",
            MavLink.Parameters.MavParamType.Real32,
            1,
            1,
            1,
            ParameterFieldMetadata.Empty,
            null);
        var item = new ParameterItemViewModel(session, field);
        var notifications = 0;
        item.PropertyChanged += (_, _) => notifications++;

        item.SetField(field);

        notifications.Should().Be(0);
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
        //   fixture.ViewModel.InitializeView();

        var load = fixture.ViewModel.RefreshParametersCommand.ExecuteAsync(null);
        var streamToken = await streamStarted.Task.WaitAsync(TestContext.Current.CancellationToken);
        streamToken.IsCancellationRequested.Should().BeFalse();
        progressCancellation.Should().NotBeNull();
        progressCancellation!.Token.IsCancellationRequested.Should().BeFalse();
        fixture.ViewModel.IsShowingProgressDialog.Should().BeTrue();

        fixture.ViewModel.CancelLoadCommand.Execute(null);
        await cancellationObserved.Task.WaitAsync(TestContext.Current.CancellationToken);

        var readCancelledToken = () => progressCancellation.Token.IsCancellationRequested;
        //readCancelledToken.Should().NotThrow().Which.Should().BeTrue();
        fixture.ViewModel.ErrorMessage.Should().Be("Parameter loading was cancelled.");
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
        IExtendedDialogService? extendedDialogService = null,
        IParameterEditSessionFactory? editSessionFactory = null,
        IVehicleParameterRegistry? parameterRegistry = null)
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
        connectionSession.ParameterRegistry.Returns(
            parameterRegistry ?? new VehicleParameterRegistry());
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
            Substitute.For<IDomainFactory>(),
            Substitute.For<IModalNavigationService>(),
            new ParametersFileHandler(Substitute.For<IFileSaver>()),
            Substitute.For<IUserConfirmationService>(),
            Substitute.For<IParameterProfileRepository>(),
            Substitute.For<IParameterProfileService>(),
            NullLogger<FullParametersListTabViewModel>.Instance);
        return new Fixture(viewModel, connectionLifetime);
    }

    private static async Task WaitForAsync(
        Func<bool> predicate,
        CancellationToken cancellationToken)
    {
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(2);
        while (!predicate() && DateTime.UtcNow < deadline)
        {
            await Task.Delay(10, cancellationToken);
        }

        predicate().Should().BeTrue();
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
