using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using MissionPlanner.App.Utilities.Dispatching;
using MissionPlanner.App.Views.InitSetup.OptionalHardware.Sections;
using MissionPlanner.Core.ConfigTuning;
using MissionPlanner.Core.Vehicles;
using MissionPlanner.Core.Vehicles.Abstractions;
using MissionPlanner.Core.Vehicles.Models;
using MissionPlanner.Library.EventHub.Abstractions;
using MissionPlanner.MavLink.Parameters;
using MissionPlanner.Shared.Models.Vehicles.Models;
using NSubstitute;

namespace MissionPlanner.AvaloniaUI.Tests;

public sealed class SerialPortsViewModelTests
{
    [Fact]
    public async Task DisconnectedDoesNotLoadOrWriteAndReconnectRebuildsRows()
    {
        using var fixture = new Fixture();
        fixture.Select(null);
        await fixture.Model.ActivateAsync();
        Assert.Empty(fixture.Model.Ports);
        Assert.False(fixture.Model.CanEdit);
        await fixture.Model.ApplyCommand.ExecuteAsync(null);
        Assert.Empty(fixture.Writes);

        fixture.Select(Fixture.First);
        await fixture.Model.RefreshCommand.ExecuteAsync(null);
        Assert.Equal("SERIAL1", Assert.Single(fixture.Model.Ports).DisplayName);
        var second = new VehicleId(2, 1);
        fixture.Store(second, "SERIAL10_PROTOCOL", 42);
        fixture.Select(second);
        await fixture.Model.RefreshCommand.ExecuteAsync(null);
        Assert.Equal("SERIAL10", Assert.Single(fixture.Model.Ports).DisplayName);
        Assert.Null(fixture.Model.Ports[0].Speed);
        Assert.Empty(fixture.Writes);
        await fixture.Model.DeactivateAsync();
        fixture.Select(Fixture.First);
        Assert.Empty(fixture.Model.Ports);
    }

    [Fact]
    public async Task SharedEditorsWriteOnlyChangedSerialFieldsWithConfirmedReadback()
    {
        using var fixture = new Fixture();
        await fixture.Model.ActivateAsync();
        var row = Assert.Single(fixture.Model.Ports);
        Assert.Equal("RCIN", row.Protocol!.SelectedValue);
        Assert.Equal("115200", row.Speed!.SelectedValue);
        Assert.Equal(129, row.Options!.LiveValue);
        await fixture.Model.ApplyCommand.ExecuteAsync(null);
        Assert.Empty(fixture.Writes);

        row.Protocol.SelectedValue = "None";
        row.Speed.SelectedValue = "230400";
        var options = row.Options;
        options.SelectedBitmaskItems.Remove(options.SelectedBitmaskItems.Single());
        options.SelectedBitmaskItems.Add(options.BitmaskOptions!.Single(option => option.Value == 2));
        Assert.Equal(130, options.Value); // Unknown bit 7 preserved; known bit 0 cleared and bit 1 set.
        Assert.True(fixture.Session.TrySetPending("RC_OPTIONS", 1024, out _));
        await fixture.Model.ApplyCommand.ExecuteAsync(null);
        Assert.Equal(3, fixture.Writes.Count);
        Assert.Contains(("SERIAL1_PROTOCOL", -1f), fixture.Writes);
        Assert.Contains(("SERIAL1_BAUD", 230f), fixture.Writes);
        Assert.Contains(("SERIAL1_OPTIONS", 130f), fixture.Writes);
        Assert.Equal(-1, row.Protocol.LiveValue);
        Assert.Equal(230, row.Speed.LiveValue);
        Assert.Equal(130, row.Options.LiveValue);
        Assert.True(fixture.Model.RebootRequired);
        Assert.True(fixture.Session.GetField("RC_OPTIONS")!.IsModified); // Other-page edit is untouched.
        await fixture.Model.ApplyCommand.ExecuteAsync(null);
        Assert.Equal(3, fixture.Writes.Count);
    }

    [Fact]
    public async Task RejectedWriteKeepsActualReadbackAndSurfacesError()
    {
        using var fixture = new Fixture { RejectWrites = true };
        await fixture.Model.ActivateAsync();
        var protocol = fixture.Model.Ports[0].Protocol!;
        protocol.SelectedValue = "None";
        await fixture.Model.ApplyCommand.ExecuteAsync(null);
        Assert.True(fixture.Model.HasError);
        Assert.Equal(23, protocol.LiveValue);
        Assert.Equal(-1, protocol.Value); // Pending edit remains visibly distinct from readback.
        Assert.Equal(ParameterEditWriteStatus.Failed, protocol.WriteStatus);
        Assert.False(fixture.Model.RebootRequired);
    }

    [Fact]
    public async Task UnknownProtocolAndBaudRemainVisibleAndUnchanged()
    {
        using var fixture = new Fixture();
        fixture.Store(Fixture.First, "SERIAL1_PROTOCOL", 999);
        fixture.Store(Fixture.First, "SERIAL1_BAUD", 777);
        await fixture.Model.ActivateAsync();
        var row = fixture.Model.Ports[0];
        Assert.Contains("999", row.Protocol!.SelectedValue);
        Assert.Contains("777", row.Speed!.SelectedValue);
        Assert.Equal(999, row.Protocol.Value);
        Assert.Equal(777, row.Speed.Value);
        await fixture.Model.ApplyCommand.ExecuteAsync(null);
        Assert.Empty(fixture.Writes);
    }

    [Theory]
    [InlineData("9600", 9)]
    [InlineData("57600", 57)]
    [InlineData("115200", 115)]
    [InlineData("230400", 230)]
    [InlineData("460800", 460)]
    [InlineData("921600", 921)]
    public async Task BaudLabelsRoundTripThroughMetadata(string label, int encoded)
    {
        using var fixture = new Fixture();
        await fixture.Model.ActivateAsync();
        fixture.Model.Ports[0].Speed!.SelectedValue = label;
        await fixture.Model.ApplyCommand.ExecuteAsync(null);
        Assert.Equal(encoded, fixture.Registry.GetParameter(Fixture.First, "SERIAL1_BAUD")!.Value);
        Assert.Equal(label, fixture.Model.Ports[0].Speed!.SelectedValue);
        Assert.DoesNotContain(fixture.Model.Ports[0].Speed!.ValuesItems!, item => item.Name.StartsWith("Unknown"));
    }

    [Fact]
    public async Task ReadOnlyAndUnadvertisedNewBitsAreRejected()
    {
        using var fixture = new Fixture(readOnlyProtocol: true);
        await fixture.Model.ActivateAsync();
        Assert.False(fixture.Model.Ports[0].CanEditProtocol);
        Assert.False(fixture.Session.TrySetPending("SERIAL1_PROTOCOL", -1, out _));
        Assert.False(fixture.Session.TrySetPending("SERIAL1_OPTIONS", 129 + 256, out _));
        Assert.True(fixture.Session.TrySetPending("SERIAL1_OPTIONS", 130, out _));
    }

    [Fact]
    public async Task LateMetadataFromPreviousVehicleCannotReplaceNewRows()
    {
        using var fixture = new Fixture();
        var pending = new TaskCompletionSource<IReadOnlyDictionary<string, ParameterMetadata>>(TaskCreationOptions.RunContinuationsAsynchronously);
        fixture.Metadata.GetAllMetadataAsync(Fixture.First, Arg.Any<CancellationToken>()).Returns(pending.Task);
        var loading = fixture.Model.ActivateAsync();
        var second = new VehicleId(2, 1);
        fixture.Store(second, "SERIAL10_PROTOCOL", 42);
        fixture.Select(second);
        await fixture.Model.RefreshCommand.ExecuteAsync(null);
        pending.SetResult(fixture.Definitions);
        await loading;
        Assert.Equal("SERIAL10", Assert.Single(fixture.Model.Ports).DisplayName);
        Assert.True(fixture.Model.CanEdit);
    }

    [Fact]
    public async Task AcceptedWriteWithoutReadbackIsNotReportedAsSuccess()
    {
        using var fixture = new Fixture { SuppressReadback = true };
        await fixture.Model.ActivateAsync();
        fixture.Model.Ports[0].Protocol!.SelectedValue = "None";
        await fixture.Model.ApplyCommand.ExecuteAsync(null);
        Assert.True(fixture.Model.HasError);
        Assert.Equal(23, fixture.Model.Ports[0].Protocol!.LiveValue);
        Assert.False(fixture.Model.RebootRequired);
    }

    [Fact]
    public async Task NewPortsArrivingDuringMetadataLoadAreEventuallyIncluded()
    {
        using var fixture = new Fixture();
        await fixture.Model.ActivateAsync();
        var pending = new TaskCompletionSource<IReadOnlyDictionary<string, ParameterMetadata>>(TaskCreationOptions.RunContinuationsAsynchronously);
        fixture.Metadata.GetAllMetadataAsync(Fixture.First, Arg.Any<CancellationToken>()).Returns(pending.Task);
        fixture.Store(Fixture.First, "SERIAL3_PROTOCOL", 23); // Triggers queued async reload.
        fixture.Store(Fixture.First, "SERIAL10_BAUD", 57); // Arrives while that load is busy.
        pending.SetResult(fixture.Definitions);
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(2));
        while (fixture.Model.IsBusy || fixture.Model.Ports.Count != 3)
        {
            await Task.Delay(5, timeout.Token);
        }
        Assert.NotNull(fixture.Model.Ports[2].Speed);
        Assert.Equal(57, fixture.Model.Ports[2].Speed!.Value);
        Assert.Empty(fixture.Writes);
    }

    private sealed class Fixture : IDisposable
    {
        public static readonly VehicleId First = new(1, 1);
        public IActiveVehicleContext Active { get; } = Substitute.For<IActiveVehicleContext>();
        public VehicleParameterRegistry Registry { get; } = new();
        public IVehicleParameterMetadataService Metadata { get; } = Substitute.For<IVehicleParameterMetadataService>();
        public Dictionary<string, ParameterMetadata> Definitions { get; } = [];
        public List<(string Name, float Value)> Writes { get; } = [];
        public bool RejectWrites
        {
            get; set;
        }
        public bool SuppressReadback
        {
            get; set;
        }
        public SerialPortsViewModel Model
        {
            get;
        }
        public ParameterEditSession Session => sessionByVehicle[Active.VehicleId!.Value];
        private readonly Dictionary<VehicleId, ParameterEditSession> sessionByVehicle = [];
        private CancellationTokenSource connectionLifetime = new();

        public Fixture(bool readOnlyProtocol = false)
        {
            Active.Current.Returns(ActiveVehicleSnapshot.Empty);
            Select(First);
            Store(First, "SERIAL1_PROTOCOL", 23);
            Store(First, "SERIAL1_BAUD", 115);
            Store(First, "SERIAL1_OPTIONS", 129);
            Store(First, "RC_OPTIONS", 0);
            Definitions["SERIAL1_PROTOCOL"] = Definition("SERIAL1_PROTOCOL", "-1:None,23:RCIN,42:DisplayPort", null, readOnlyProtocol);
            Definitions["SERIAL1_BAUD"] = Definition("SERIAL1_BAUD", "9:9600,57:57600,115:115200,230:230400,460:460800,921:921600");
            Definitions["SERIAL1_OPTIONS"] = Definition("SERIAL1_OPTIONS", null, "0:Invert RX,1:Invert TX");
            Definitions["RC_OPTIONS"] = Definition("RC_OPTIONS", null, "10:Multiple Receiver Support");
            Metadata.GetAllMetadataAsync(Arg.Any<VehicleId>(), Arg.Any<CancellationToken>())
                .Returns(_ => Task.FromResult<IReadOnlyDictionary<string, ParameterMetadata>>(Definitions));
            var protocol = Substitute.For<IVehicleParameterService>();
            protocol.SetParameterAsync(Arg.Any<VehicleId>(), Arg.Any<string>(), Arg.Any<float>(), Arg.Any<MavParamType>(), Arg.Any<CancellationToken>())
                .Returns(call =>
                {
                    var name = call.Arg<string>()!;
                    var value = call.Arg<float>();
                    Writes.Add((name, value));
                    if (!RejectWrites && !SuppressReadback)
                    {
                        Store(call.Arg<VehicleId>(), name, value);
                    }
                    return !RejectWrites;
                });
            var factory = Substitute.For<IParameterEditSessionFactory>();
            factory.Create(Arg.Any<VehicleId>()).Returns(call =>
            {
                var id = call.Arg<VehicleId>();
                if (!sessionByVehicle.TryGetValue(id, out var session))
                {
                    session = new ParameterEditSession(new ParameterEditScope(id, Active.State!.Identity.Firmware),
                        Active, Registry, protocol, Metadata, Options.Create(new ParameterEditSessionOptions { ReadbackTimeout = TimeSpan.FromMilliseconds(50) }),
                        NullLogger<ParameterEditSession>.Instance);
                    sessionByVehicle.Add(id, session);
                }
                return session;
            });
            Model = new SerialPortsViewModel(Active, Registry, factory, new InlineDispatcher(), Substitute.For<IDomainEventHub>(), NullLogger<SerialPortsViewModel>.Instance);
        }

        public void Store(VehicleId id, string name, float value)
        {
            Registry.StoreParameter(id, new VehicleParameter(name, value, MavParamType.Int32, 0, 1), CancellationToken.None);
        }

        public void Select(VehicleId? id)
        {
            var previous = Active.Current;
            connectionLifetime.Cancel();
            connectionLifetime.Dispose();
            connectionLifetime = new CancellationTokenSource();
            var state = id is { } target ? new VehicleState(target, 0, 2, 3, 0, 4, 3,
                VehicleConnectionState.Online, DateTimeOffset.UtcNow, VehicleMode.Stabilize, false,
                null, null, null, null, null, null, null, null) : null;
            var current = new ActiveVehicleSnapshot(id, state);
            Active.Current.Returns(current);
            Active.VehicleId.Returns(id);
            Active.State.Returns(state);
            Active.IsOnline.Returns(id.HasValue);
            Active.ConnectionCancellationToken.Returns(connectionLifetime.Token);
            Active.Changed += Raise.Event<Action<ActiveVehicleChangedEventArgs>>(new ActiveVehicleChangedEventArgs(previous, current));
        }

        public void Dispose()
        {
            Model.Dispose();
            foreach (var session in sessionByVehicle.Values)
            {
                session.Dispose();
            }
            connectionLifetime.Dispose();
        }

        private static ParameterMetadata Definition(string name, string? values = null, string? bits = null, bool readOnly = false)
        {
            return new(name, name, null, null, null, null, values, bits, null, null, true, readOnly);
        }
    }

    private sealed class InlineDispatcher : IUiDispatcher
    {
        public bool CheckAccess()
        {
            return true;
        }

        public void Dispatch(Action action)
        {
            action();
        }

        public T Dispatch<T>(Func<T> action)
        {
            return action();
        }

        public Task DispatchAsync(Action action)
        {
            action();
            return Task.CompletedTask;
        }
        public Task<T> DispatchAsync<T>(Func<T> action)
        {
            return Task.FromResult(action());
        }

        public Task DispatchAsync(Func<Task> action)
        {
            return action();
        }

        public Task<T> DispatchAsync<T>(Func<Task<T>> action)
        {
            return action();
        }
    }
}
