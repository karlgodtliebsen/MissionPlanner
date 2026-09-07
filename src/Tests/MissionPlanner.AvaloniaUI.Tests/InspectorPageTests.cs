using Microsoft.Extensions.Logging.Abstractions;
using MissionPlanner.App.Presentation;
using MissionPlanner.App.Utilities.Dispatching;
using MissionPlanner.App.Views.InitSetup.Advanced.Inspector;
using MissionPlanner.Core.Setup.Advanced.Inspector;
using MissionPlanner.Core.Vehicles.Abstractions;
using MissionPlanner.Library.EventHub.Abstractions;
using MissionPlanner.MavLink;
using MissionPlanner.MavLink.Services;
using MissionPlanner.MavLink.Services.Abstractions;
using MissionPlanner.Transport;
using NSubstitute;

namespace MissionPlanner.AvaloniaUI.Tests;

public sealed class InspectorPageTests
{
    [Fact]
    public async Task FrozenDisplayDoesNotStopTotalsAndTenReopensReleaseObservers()
    {
        var dispatcher = new InlineDispatcher();
        var events = Substitute.For<IDomainEventHub>();
        var clipboard = Substitute.For<ITextClipboardService>();
        var list = new InspectorListViewModel(NullLogger<InspectorListViewModel>.Instance, dispatcher, events);
        var detail = new InspectorDetailViewModel(clipboard, NullLogger<InspectorDetailViewModel>.Instance, dispatcher, events);
        var aggregate = new InspectorAggregator(new MavLinkMessageDefinitionRegistry(), TimeProvider.System);
        var tap = new MavLinkInspectionTap();
        var connection = Substitute.For<IMavLinkConnection>();
        connection.Inspection.Returns(tap);
        var vehicle = Substitute.For<IVehicleConnectionSession>();
        vehicle.Connection.Returns(connection);
        var session = new InspectorSession(vehicle, aggregate);
        using var model = new MavLinkInspectorViewModel(list, detail, session, Substitute.For<IFileSaveService>(),
            NullLogger<MavLinkInspectorViewModel>.Instance, dispatcher, events);
        for (var cycle = 0; cycle < 10; cycle++)
        {
            await model.ActivateAsync();
            await model.ActivateAsync();
            Assert.True(tap.HasObservers);
            var frame = new MavLinkFrame(1, 1, new TransportEndPoint("test"), 0, 3, new byte[1], new byte[12], DateTimeOffset.UtcNow);
            aggregate.Observe(new(MavLinkTrafficDirection.Inbound, frame, null, true));
            list.Frozen = false;
            list.Frozen = true;
            var displayed = Assert.Single(list.Rows);
            list.Selected = displayed;
            var frozenDetail = detail.Text;
            aggregate.Observe(new(MavLinkTrafficDirection.Inbound, frame, null, true));
            Assert.Equal(1, Assert.Single(list.Rows).Count);
            Assert.Equal(2, Assert.Single(session.Rows(null)).Count);
            Assert.Equal(frozenDetail, detail.Text);
            await detail.CopyCommand.ExecuteAsync(null);
            await clipboard.Received().SetTextAsync(frozenDetail);
            list.ClearCommand.Execute(null);
            Assert.Empty(session.Rows(null));
            await model.DeactivateAsync();
            Assert.False(tap.HasObservers);
            Assert.Empty(detail.Text);
        }
        await connection.DidNotReceive().DisposeAsync();
    }

    private sealed class InlineDispatcher : IUiDispatcher
    {
        public bool CheckAccess() => true;
        public void Dispatch(Action action) => action();
        public T Dispatch<T>(Func<T> action) => action();
        public Task DispatchAsync(Action action)
        {
            action();
            return Task.CompletedTask;
        }
        public Task<T> DispatchAsync<T>(Func<T> action) => Task.FromResult(action());
        public Task DispatchAsync(Func<Task> action) => action();
        public Task<T> DispatchAsync<T>(Func<Task<T>> action) => action();
    }
}
