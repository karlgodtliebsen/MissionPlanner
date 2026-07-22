using System.Text.Json;
using CommunityToolkit.Maui.Storage;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MissionPlanner.App.Presentation;
using MissionPlanner.App.Views.FlightData.Tabs;
using MissionPlanner.Core.Notifications;
using MissionPlanner.Core.Vehicles;
using MissionPlanner.Core.Vehicles.Abstractions;
using MissionPlanner.Core.Vehicles.Handlers;
using MissionPlanner.Core.Vehicles.Models;
using MissionPlanner.Library.DateTime.Domain;
using MissionPlanner.Library.EventHub.Abstractions;
using MissionPlanner.MavLink;
using MissionPlanner.MavLink.Messages;
using MissionPlanner.Transport;
using NSubstitute;

namespace MissionPlanner.Core.Tests;

/// <summary>
/// Verifies status-text assembly, bounded histories, filtering, export, and Messages-tab lifecycle behavior.
/// </summary>
public sealed class VehicleMessagesTests
{
    /// <summary>Verifies MAVLink 1 frames and duplicate-free MAVLink 2 chunk assembly.</summary>
    [Fact]
    public async Task HandlerAssemblesChunksAndHandlesSingleFrameMessages()
    {
        var fixture = CreateHandlerFixture();
        await fixture.Handler.Handle(Message("legacy", id: null, chunk: null), TestContext.Current.CancellationToken);
        await fixture.Handler.Handle(Message(new string('A', 50), id: 10, chunk: 0, terminated: false), TestContext.Current.CancellationToken);
        await fixture.Handler.Handle(Message(new string('A', 50), id: 10, chunk: 0, terminated: false), TestContext.Current.CancellationToken);
        await fixture.Handler.Handle(Message(new string('B', 50), id: 10, chunk: 1, terminated: false), TestContext.Current.CancellationToken);
        await fixture.Handler.Handle(Message(new string('B', 50), id: 10, chunk: 1, terminated: false), TestContext.Current.CancellationToken);
        await fixture.Handler.Handle(Message("done", id: 10, chunk: 2), TestContext.Current.CancellationToken);
        await fixture.Handler.Handle(Message("done", id: 10, chunk: 2), TestContext.Current.CancellationToken);
        await fixture.Handler.Handle(Message("single", id: 11, chunk: 0), TestContext.Current.CancellationToken);
        await fixture.Handler.Handle(Message("single", id: 11, chunk: 0), TestContext.Current.CancellationToken);

        var messages = fixture.Store.GetMessages(fixture.VehicleId);
        messages.Should().HaveCount(3);
        messages[0].Text.Should().Be("legacy");
        messages[0].IsAssembled.Should().BeFalse();
        messages[1].Text.Should().Be(new string('A', 50) + new string('B', 50) + "done");
        messages[1].IsAssembled.Should().BeTrue();
        messages[1].IsTruncated.Should().BeFalse();
        messages[2].Text.Should().Be("single");
    }

    /// <summary>Verifies interleaved IDs remain isolated and missing chunks are explicitly truncated.</summary>
    [Fact]
    public async Task HandlerKeepsInterleavedIdsSeparateAndMarksGaps()
    {
        var fixture = CreateHandlerFixture();
        await fixture.Handler.Handle(Message("one-", id: 20, chunk: 0, terminated: false), TestContext.Current.CancellationToken);
        await fixture.Handler.Handle(Message("two-", id: 21, chunk: 0, terminated: false), TestContext.Current.CancellationToken);
        await fixture.Handler.Handle(Message("end", id: 20, chunk: 1), TestContext.Current.CancellationToken);
        await fixture.Handler.Handle(Message("end", id: 21, chunk: 1), TestContext.Current.CancellationToken);
        await fixture.Handler.Handle(Message("missing-", id: 30, chunk: 0, terminated: false), TestContext.Current.CancellationToken);
        await fixture.Handler.Handle(Message("orphan", id: 30, chunk: 2), TestContext.Current.CancellationToken);

        var messages = fixture.Store.GetMessages(fixture.VehicleId);
        messages.Select(message => message.Text).Should().ContainInOrder("one-end", "two-end", "missing-", "orphan");
        messages.Take(2).Should().OnlyContain(message => message.IsAssembled && !message.IsTruncated);
        messages.Skip(2).Should().OnlyContain(message => message.IsTruncated);
    }

    /// <summary>Verifies incomplete assemblies flush after the configured timeout.</summary>
    [Fact]
    public async Task HandlerFlushesIncompleteChunkOnTimeout()
    {
        var fixture = CreateHandlerFixture(TimeSpan.FromMilliseconds(20));
        await fixture.Handler.Handle(Message("partial", id: 40, chunk: 0, terminated: false), TestContext.Current.CancellationToken);

        await EventuallyAsync(
            () => fixture.Store.GetMessages(fixture.VehicleId).Count == 1,
            TimeSpan.FromSeconds(1),
            TestContext.Current.CancellationToken);

        fixture.Store.GetMessages(fixture.VehicleId).Single().IsTruncated.Should().BeTrue();
    }

    /// <summary>Verifies bounded eviction and strict per-vehicle history isolation.</summary>
    [Fact]
    public void MessageStoreEvictsOldestPerVehicle()
    {
        var store = new VehicleMessageStore(Options.Create(new VehicleMessageStoreOptions { Capacity = 2 }));
        var first = new VehicleId(1, 1);
        var second = new VehicleId(2, 1);

        store.Add(Status(first, "one"));
        store.Add(Status(first, "two"));
        store.Add(Status(first, "three"));
        store.Add(Status(second, "other"));

        store.GetMessages(first).Select(message => message.Text).Should().Equal("two", "three");
        store.GetMessages(second).Select(message => message.Text).Should().Equal("other");
    }

    /// <summary>Verifies filtering, text search, separate local origin, and complete text/JSON exports.</summary>
    [Fact]
    public void ViewModelFiltersAndExportsCompleteIdentity()
    {
        var now = DateTimeOffset.UtcNow;
        var state = CreateState(new VehicleId(1, 1), now);
        var fixture = CreateViewModelFixture(state);
        fixture.VehicleStore.Add(Status(state.VehicleId, "motor warning", MavSeverity.Warning, now));
        fixture.VehicleStore.Add(Status(state.VehicleId, "navigation ready", MavSeverity.Info, now.AddSeconds(1)));
        fixture.ApplicationStore.Add(
            new UserNotification("command timeout", "Arm", UserNotificationSeverity.Error, VehicleId: state.VehicleId),
            now.AddSeconds(2));
        fixture.ViewModel.SelectedSeverity = "Warning";
        fixture.ViewModel.SearchText = "motor";

        fixture.ViewModel.Items.Should().ContainSingle().Which.Text.Should().Be("motor warning");
        var text = fixture.ViewModel.CreateTextExport();
        text.Should().Contain(now.ToString("O")).And.Contain("MAVLink 1:1").And.Contain("Warning");
        using var json = JsonDocument.Parse(fixture.ViewModel.CreateJsonExport());
        json.RootElement.GetArrayLength().Should().Be(1);

        fixture.ViewModel.SelectedSeverity = "Application";
        fixture.ViewModel.SearchText = string.Empty;
        fixture.ViewModel.Items.Should().ContainSingle().Which.Origin.Should().Be(MessageListOrigin.Application);
    }

    /// <summary>Verifies pause/resume auto-scroll and retained history across a temporary disconnect.</summary>
    [Fact]
    public async Task ViewModelRetainsReconnectHistoryAndHonorsAutoScrollPause()
    {
        var now = DateTimeOffset.UtcNow;
        var state = CreateState(new VehicleId(1, 1), now);
        var fixture = CreateViewModelFixture(state);
        await fixture.ViewModel.ActivateAsync(TestContext.Current.CancellationToken);

        fixture.VehicleStore.Add(Status(state.VehicleId, "first", receivedAt: now));
        var initialScroll = fixture.ViewModel.ScrollRequestVersion;
        fixture.ViewModel.TogglePauseCommand.Execute(null);
        fixture.VehicleStore.Add(Status(state.VehicleId, "second", receivedAt: now.AddSeconds(1)));
        fixture.ViewModel.ScrollRequestVersion.Should().Be(initialScroll);

        await fixture.ViewModel.DeactivateAsync();
        fixture.Active.SetState(state with { Connection = state.Connection with { State = VehicleConnectionState.Offline } });
        fixture.ViewModel.Items.Should().HaveCount(2);
        fixture.Active.SetState(state);
        await fixture.ViewModel.ActivateAsync(TestContext.Current.CancellationToken);

        fixture.ViewModel.Items.Select(item => item.Text).Should().Equal("first", "second");
        fixture.ViewModel.TogglePauseCommand.Execute(null);
        fixture.ViewModel.ScrollRequestVersion.Should().BeGreaterThan(initialScroll);
    }

    private static HandlerFixture CreateHandlerFixture(TimeSpan? timeout = null)
    {
        var now = DateTimeOffset.UtcNow;
        var vehicleId = new VehicleId(1, 1);
        var state = CreateState(vehicleId, now);
        var clock = Substitute.For<IDateTimeProvider>();
        clock.UtcNow.Returns(now);
        var session = new VehicleSession(state, new TransportEndPoint("test"), clock);
        var registry = Substitute.For<IVehicleRegistry>();
        registry.GetRequired(vehicleId).Returns(session);
        registry.Vehicles.Returns([session]);
        var options = Options.Create(new VehicleMessageStoreOptions
        {
            Capacity = 20,
            ChunkTimeout = timeout ?? TimeSpan.FromSeconds(1)
        });
        var store = new VehicleMessageStore(options);
        var handler = new StatusTextHandler(
            registry,
            store,
            Substitute.For<IDomainEventHub>(),
            options,
            Substitute.For<ILogger<StatusTextHandler>>());
        return new HandlerFixture(handler, store, vehicleId);
    }

    private static ViewModelFixture CreateViewModelFixture(VehicleState state)
    {
        var active = new TestActiveVehicleContext(state);
        var options = Options.Create(new VehicleMessageStoreOptions { Capacity = 20 });
        var vehicleStore = new VehicleMessageStore(options);
        var applicationStore = new ApplicationNotificationStore(options);
        var dispatcher = Substitute.For<IDispatcher>();
        dispatcher.Dispatch(Arg.Any<Action>()).Returns(call =>
        {
            call.Arg<Action>()!.Invoke();
            return true;
        });
        var viewModel = new MessagesTabViewModel(
            active,
            vehicleStore,
            applicationStore,
            Substitute.For<ITextClipboardService>(),
            Substitute.For<IFileSaver>(),
            dispatcher,
            Substitute.For<ILogger<MessagesTabViewModel>>());
        return new ViewModelFixture(viewModel, vehicleStore, applicationStore, active);
    }

    private static StatusTextMessage Message(
        string text,
        ushort? id,
        byte? chunk,
        bool terminated = true) =>
        new(1, 1, new TransportEndPoint("test"), MavSeverity.Info, text, id, chunk, DateTimeOffset.UtcNow, terminated);

    private static VehicleStatusText Status(
        VehicleId vehicleId,
        string text,
        MavSeverity severity = MavSeverity.Info,
        DateTimeOffset? receivedAt = null) =>
        new(vehicleId, vehicleId.SystemId, vehicleId.ComponentId, severity, text, receivedAt ?? DateTimeOffset.UtcNow);

    private static VehicleState CreateState(VehicleId vehicleId, DateTimeOffset now) =>
        new(vehicleId, 0, 2, 3, 0, 4, 3, VehicleConnectionState.Online, now, VehicleMode.Stabilize,
            false, null, null, null, null, null, null, null, null);

    private static async Task EventuallyAsync(Func<bool> condition, TimeSpan timeout, CancellationToken cancellationToken)
    {
        var deadline = DateTimeOffset.UtcNow + timeout;
        while (!condition() && DateTimeOffset.UtcNow < deadline)
        {
            await Task.Delay(10, cancellationToken);
        }

        condition().Should().BeTrue();
    }

    private sealed record HandlerFixture(StatusTextHandler Handler, VehicleMessageStore Store, VehicleId VehicleId);

    private sealed record ViewModelFixture(
        MessagesTabViewModel ViewModel,
        VehicleMessageStore VehicleStore,
        ApplicationNotificationStore ApplicationStore,
        TestActiveVehicleContext Active);

    private sealed class TestActiveVehicleContext : IActiveVehicleContext
    {
        private CancellationTokenSource connection = new();

        public TestActiveVehicleContext(VehicleState state)
        {
            Current = new ActiveVehicleSnapshot(state.VehicleId, state);
        }

        public ActiveVehicleSnapshot Current { get; private set; }

        public VehicleId? VehicleId => Current.VehicleId;

        public VehicleState? State => Current.State;

        public bool IsOnline => Current.IsOnline;

        public CancellationToken ConnectionCancellationToken => connection.Token;

        public event EventHandler<ActiveVehicleChangedEventArgs>? Changed;

        public void SetState(VehicleState state)
        {
            var previous = Current;
            connection.Cancel();
            connection.Dispose();
            connection = new CancellationTokenSource();
            Current = new ActiveVehicleSnapshot(state.VehicleId, state);
            Changed?.Invoke(this, new ActiveVehicleChangedEventArgs(previous, Current));
        }
    }
}
