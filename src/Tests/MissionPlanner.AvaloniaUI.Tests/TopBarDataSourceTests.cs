using Avalonia;
using Avalonia.Headless;
using Microsoft.Extensions.Logging.Abstractions;
using MissionPlanner.App.Configuration;
using MissionPlanner.App.Utilities.Dialogs;
using MissionPlanner.App.Views.Common;
using MissionPlanner.App.Views.Navigation;
using MissionPlanner.Core.ConfigTuning.Planner;
using MissionPlanner.Core.Replay;
using MissionPlanner.Core.Simulation;
using MissionPlanner.Core.Vehicles;
using MissionPlanner.Core.Vehicles.Models;
using MissionPlanner.Core.Vehicles.Abstractions;
using MissionPlanner.Library.EventHub.Abstractions;
using MissionPlanner.Library.Factory.Domain.Abstractions;
using MissionPlanner.Shared.Models.Vehicles.Models;
using MissionPlanner.Simulation;
using NSubstitute;

namespace MissionPlanner.AvaloniaUI.Tests;

[Collection("Document rendering")]
public sealed class TopBarDataSourceTests
{
    public static AppBuilder BuildAvaloniaApp() => ConnectPopupTests.BuildAvaloniaApp();

    [Fact]
    public async Task ModeFollowsSelectedVehicleAndReplayRatherThanNetworkTransport()
    {
        var session = HeadlessUnitTestSession.StartNew(typeof(TopBarDataSourceTests));
        try
        {
        await session.Dispatch(() =>
        {
            var active = Substitute.For<IActiveVehicleContext>();
            active.Current.Returns(ActiveVehicleSnapshot.Empty);
            using var state = new ApplicationStateService(active);
            state.SelectedChannel = "UDP";
            var replay = Substitute.For<IReplaySessionManager>();
            replay.Snapshot.Returns(ReplaySessionSnapshot.Unloaded);
            var settings = Substitute.For<IPlannerSettingsService>();
            settings.Current.Returns(new PlannerSettings());
            var channels = new SimulationVehicleChannelRegistry();
            var simulatorId = new VehicleId(42, 1);
            var channel = new SimulationVehicleChannel(Guid.NewGuid(), simulatorId,
                Substitute.For<IVehicleConnectionSession>(), SimulatorProfile.CreateDefault(), DateTimeOffset.UtcNow);
            channels.Register(channel);
            using var model = new TopBarViewModel(state, Substitute.For<IServiceFactory>(),
                Substitute.For<IDialogService>(), Substitute.For<IDomainEventHub>(), replay, Substitute.For<INavigationService>(),
                settings, NullLogger<TopBarViewModel>.Instance, simulationChannels: channels);
            Assert.Equal("LIVE", model.DataSourceMode);
            state.VehicleId = simulatorId;
            Assert.Equal("SIMULATION", model.DataSourceMode);
            state.VehicleId = new VehicleId(16, 1);
            Assert.Equal("LIVE", model.DataSourceMode);
            state.VehicleId = simulatorId;
            Assert.Equal("SIMULATION", model.DataSourceMode);
            replay.Changed += Raise.Event<Action<ReplaySessionChangedEventArgs>>(new ReplaySessionChangedEventArgs(
                ReplaySessionSnapshot.Unloaded with { State = ReplaySessionState.Paused }));
            Assert.Equal("REPLAY · READ ONLY", model.DataSourceMode);
            Assert.False(model.CanOpenConnection);
            replay.Changed += Raise.Event<Action<ReplaySessionChangedEventArgs>>(new ReplaySessionChangedEventArgs(ReplaySessionSnapshot.Unloaded));
            Assert.Equal("SIMULATION", model.DataSourceMode);
            channels.Remove(channel.SessionId);
            state.VehicleId = null;
            Assert.Equal("LIVE", model.DataSourceMode);
        }, TestContext.Current.CancellationToken);
        }
        finally
        {
            await Task.Run(session.Dispose, CancellationToken.None);
        }
    }
}
