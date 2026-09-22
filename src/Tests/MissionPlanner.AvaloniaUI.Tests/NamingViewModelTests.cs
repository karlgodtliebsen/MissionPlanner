using Microsoft.Extensions.Logging.Abstractions;
using MissionPlanner.App.Utilities.Dispatching;
using MissionPlanner.App.Views.InitSetup.OptionalHardware.Sections;
using MissionPlanner.Core.Vehicles;
using MissionPlanner.Core.Vehicles.Abstractions;
using MissionPlanner.Library.EventHub.Abstractions;
using MissionPlanner.MavLink.Parameters;
using MissionPlanner.Shared.Models.Vehicles.Models;
using NSubstitute;

namespace MissionPlanner.AvaloniaUI.Tests;

/// <summary>Verifies live identifier loading, confirmed writes and active-vehicle lifetime handling.</summary>
public sealed class NamingViewModelTests
{
    /// <summary>Activation explicitly reads both values; unchanged values are never written.</summary>
    [Fact]
    public async Task ActivationLoadsFreshValuesAndApplyStartsDisabled()
    {
        using var fixture = new Fixture();
        await fixture.Model.ActivateAsync();
        Assert.Equal("1", fixture.Model.MavSystemId);
        Assert.Equal("42", fixture.Model.BoardSerialNumber);
        Assert.Equal(new[] { "MAV_SYSID", "BRD_SERIAL_NUM" }, fixture.Reads);
        Assert.True(fixture.Model.CanEdit);
        Assert.False(fixture.Model.ApplyCommand.CanExecute(null));
        await fixture.Model.ApplyCommand.ExecuteAsync(null);
        Assert.Empty(fixture.Writes);
    }

    /// <summary>The disconnected view explains availability without sending requests.</summary>
    [Fact]
    public async Task DisconnectedShowsMessageAndDisablesEditing()
    {
        using var fixture = new Fixture();
        fixture.Active.IsOnline.Returns(false);
        await fixture.Model.ActivateAsync();
        Assert.Contains("Connect a vehicle", fixture.Model.StatusMessage);
        Assert.False(fixture.Model.CanEdit);
        Assert.Empty(fixture.Reads);
        Assert.False(fixture.Model.ApplyCommand.CanExecute(null));
    }

    /// <summary>Only changed values are sent, with the loaded types, and the system ID is last.</summary>
    [Theory]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task ApplyWritesChangedValuesOnly(bool system, bool serial)
    {
        using var fixture = new Fixture();
        await fixture.Model.ActivateAsync();
        if (system)
        {
            fixture.Model.MavSystemId = "9";
        }
        if (serial)
        {
            fixture.Model.BoardSerialNumber = "1234";
        }
        await fixture.Model.ApplyCommand.ExecuteAsync(null);
        Assert.Equal((system ? 1 : 0) + (serial ? 1 : 0), fixture.Writes.Count);
        if (system && serial)
        {
            Assert.Equal(new[] { "BRD_SERIAL_NUM", "MAV_SYSID" }, fixture.Writes.Select(w => w.Name));
        }
        Assert.All(fixture.Writes, write =>
        {
            Assert.Equal(new VehicleId(1, 1), write.Id);
            Assert.Equal(write.Name == "MAV_SYSID" ? MavParamType.Int16 : MavParamType.Int32, write.Type);
        });
        Assert.Contains("confirmed", fixture.Model.StatusMessage);
        Assert.False(fixture.Model.ApplyCommand.CanExecute(null));
    }

    /// <summary>Invalid text cannot cause either parameter to be written.</summary>
    [Theory]
    [InlineData("0", "42")]
    [InlineData("256", "42")]
    [InlineData("1.5", "42")]
    [InlineData("1", "8388608")]
    [InlineData("1", "-8388609")]
    [InlineData("1", "NaN")]
    public async Task InvalidValuesAreRejectedBeforeAnyWrite(string system, string serial)
    {
        using var fixture = new Fixture();
        await fixture.Model.ActivateAsync();
        fixture.Model.MavSystemId = system;
        fixture.Model.BoardSerialNumber = serial;
        await fixture.Model.ApplyCommand.ExecuteAsync(null);
        Assert.NotNull(fixture.Model.ErrorMessage);
        Assert.Empty(fixture.Writes);
    }

    /// <summary>Missing firmware parameters produce a useful error, not editable default values.</summary>
    [Fact]
    public async Task FailedReadLeavesEditingDisabled()
    {
        using var fixture = new Fixture();
        fixture.Parameters.RequestParameterAsync(Arg.Any<VehicleId>(), "MAV_SYSID", Arg.Any<CancellationToken>()).Returns(false);
        await fixture.Model.ActivateAsync();
        Assert.Contains("MAV_SYSID", fixture.Model.ErrorMessage);
        Assert.False(fixture.Model.CanEdit);
    }

    /// <summary>Confirmed earlier changes are not resent when retrying a later failed write.</summary>
    [Fact]
    public async Task PartialFailureRetainsConfirmedValues()
    {
        using var fixture = new Fixture();
        await fixture.Model.ActivateAsync();
        fixture.Model.BoardSerialNumber = "100";
        fixture.Model.MavSystemId = "8";
        fixture.RejectSystem = true;
        await fixture.Model.ApplyCommand.ExecuteAsync(null);
        Assert.Contains("MAV_SYSID", fixture.Model.ErrorMessage);
        fixture.RejectSystem = false;
        fixture.Writes.Clear();
        await fixture.Model.ApplyCommand.ExecuteAsync(null);
        Assert.Equal("MAV_SYSID", Assert.Single(fixture.Writes).Name);
        Assert.Contains("confirmed", fixture.Model.StatusMessage);
    }

    /// <summary>A sent but unconfirmed value is never reported as saved.</summary>
    [Fact]
    public async Task SentWithoutReadbackReportsUnconfirmed()
    {
        using var fixture = new Fixture();
        await fixture.Model.ActivateAsync();
        fixture.Model.BoardSerialNumber = "100";
        fixture.ConfirmWrites = false;
        await fixture.Model.ApplyCommand.ExecuteAsync(null);
        Assert.Contains("not confirmed", fixture.Model.ErrorMessage);
        Assert.True(fixture.Model.ApplyCommand.CanExecute(null));
    }

    /// <summary>Late responses after leaving the page cannot repopulate it or enable Apply.</summary>
    [Fact]
    public async Task DeactivationCancelsPendingRead()
    {
        using var fixture = new Fixture();
        fixture.ConfirmReads = false;
        var loading = fixture.Model.ActivateAsync();
        await fixture.Model.DeactivateAsync();
        fixture.Store("MAV_SYSID", 99);
        await loading.WaitAsync(TimeSpan.FromSeconds(1), TestContext.Current.CancellationToken);
        Assert.False(fixture.Model.CanEdit);
        Assert.Empty(fixture.Model.MavSystemId);
        Assert.Single(fixture.Reads);
    }

    /// <summary>Disconnect cancels outstanding responses and clears the former target's fields.</summary>
    [Fact]
    public async Task DisconnectClearsFields()
    {
        using var fixture = new Fixture();
        await fixture.Model.ActivateAsync();
        fixture.Active.IsOnline.Returns(false);
        fixture.Active.VehicleId.Returns((VehicleId?)null);
        fixture.Active.Changed += Raise.Event<Action<ActiveVehicleChangedEventArgs>>(new ActiveVehicleChangedEventArgs(new(null, null), new(null, null)));
        Assert.False(fixture.Model.CanEdit);
        Assert.Empty(fixture.Model.MavSystemId);
        Assert.Contains("Connect a vehicle", fixture.Model.StatusMessage);
    }

    /// <summary>A changed system ID reconnects to the saved endpoint and verifies both values.</summary>
    [Fact]
    public async Task SystemIdChangeReconnectsAndClosesOverlayAfterReadback()
    {
        using var fixture = new Fixture();
        var target = new VehicleReconnectTarget(Guid.NewGuid(), "Serial", "COM11", 0, 57600);
        fixture.Connections.CaptureReconnectTarget().Returns(target);
        var overlay = Substitute.For<IDisposable>();
        string? initialMessage = null;
        fixture.Dialogs.DisplayProgressCancellableAsync(Arg.Any<Func<string>>(),
            Arg.Any<MissionPlanner.App.Utilities.Dialogs.DialogOptions>(), Arg.Any<CancellationToken>())
            .Returns(call => { initialMessage = call.Arg<Func<string>>()!(); return overlay; });
        fixture.Connections.ReconnectAsync(target, Arg.Any<IProgress<string>>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                Assert.False(call.Arg<CancellationToken>().IsCancellationRequested);
                fixture.Active.VehicleId.Returns(new VehicleId(9, 1));
                fixture.Active.Changed += Raise.Event<Action<ActiveVehicleChangedEventArgs>>(new ActiveVehicleChangedEventArgs(new(null, null), new(null, null)));
                return new VehicleConnectionResult(true, new(9, 1), null);
            });
        await fixture.Model.ActivateAsync();
        fixture.Model.MavSystemId = "9";
        await fixture.Model.ApplyCommand.ExecuteAsync(null);
        Assert.Contains("COM11", initialMessage);
        Assert.Contains("confirmed", fixture.Model.StatusMessage);
        Assert.Equal("9", fixture.Model.MavSystemId);
        Assert.True(fixture.Model.CanEdit);
        Assert.Equal(4, fixture.Reads.Count);
        overlay.Received(1).Dispose();
        Assert.Single(fixture.Writes);
    }

    /// <summary>Loss of the old connection cancels its readback, but not the reconnect operation.</summary>
    [Fact]
    public async Task LostAckRecoversWithIndependentLifetime()
    {
        using var fixture = new Fixture();
        using var oldConnection = new CancellationTokenSource();
        fixture.Active.ConnectionCancellationToken.Returns(oldConnection.Token);
        var target = new VehicleReconnectTarget(Guid.NewGuid(), "Serial", "COM11", 0, 115200);
        fixture.Connections.CaptureReconnectTarget().Returns(target);
        var overlay = Substitute.For<IDisposable>();
        fixture.Dialogs.DisplayProgressCancellableAsync(Arg.Any<Func<string>>(),
            Arg.Any<MissionPlanner.App.Utilities.Dialogs.DialogOptions>(), Arg.Any<CancellationToken>()).Returns(overlay);
        fixture.Parameters.SetParameterAsync(Arg.Any<VehicleId>(), "MAV_SYSID", 9, Arg.Any<MavParamType>(), Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                fixture.CurrentSystem = 9;
                fixture.Active.IsOnline.Returns(false);
                oldConnection.Cancel();
                fixture.Active.Changed += Raise.Event<Action<ActiveVehicleChangedEventArgs>>(new ActiveVehicleChangedEventArgs(new(null, null), new(null, null)));
                return true;
            });
        fixture.Connections.ReconnectAsync(target, Arg.Any<IProgress<string>>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                Assert.False(call.Arg<CancellationToken>().IsCancellationRequested);
                fixture.Active.IsOnline.Returns(true);
                fixture.Active.VehicleId.Returns(new VehicleId(9, 1));
                fixture.Active.ConnectionCancellationToken.Returns(CancellationToken.None);
                return new VehicleConnectionResult(true, new(9, 1), null);
            });
        await fixture.Model.ActivateAsync();
        fixture.Model.MavSystemId = "9";
        await fixture.Model.ApplyCommand.ExecuteAsync(null);
        Assert.Contains("confirmed", fixture.Model.StatusMessage);
        Assert.True(fixture.Model.CanEdit);
        overlay.Received(1).Dispose();
    }

    /// <summary>Failed reconnects close progress, retain a manual retry and do not claim saved values.</summary>
    [Fact]
    public async Task FailedReconnectAllowsManualRetry()
    {
        using var fixture = new Fixture();
        var target = new VehicleReconnectTarget(Guid.NewGuid(), "Serial", "COM11", 0, 115200);
        fixture.Connections.CaptureReconnectTarget().Returns(target);
        var overlay = Substitute.For<IDisposable>();
        fixture.Dialogs.DisplayProgressCancellableAsync(Arg.Any<Func<string>>(),
            Arg.Any<MissionPlanner.App.Utilities.Dialogs.DialogOptions>(), Arg.Any<CancellationToken>()).Returns(overlay);
        fixture.Connections.ReconnectAsync(target, Arg.Any<IProgress<string>>(), Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                fixture.Active.IsOnline.Returns(false);
                return new VehicleConnectionResult(false, null, null, "Port still busy");
            });
        await fixture.Model.ActivateAsync();
        fixture.Model.MavSystemId = "9";
        await fixture.Model.ApplyCommand.ExecuteAsync(null);
        Assert.Contains("Port still busy", fixture.Model.ErrorMessage);
        Assert.True(fixture.Model.ReconnectCommand.CanExecute(null));
        Assert.False(fixture.Model.CanEdit);
        overlay.Received(1).Dispose();
        fixture.Connections.ReconnectAsync(target, Arg.Any<IProgress<string>>(), Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                fixture.Active.IsOnline.Returns(true);
                return new VehicleConnectionResult(true, new(1, 1), null);
            });
        await fixture.Model.ReconnectCommand.ExecuteAsync(null);
        Assert.Contains("confirmed", fixture.Model.StatusMessage);
        overlay.Received(2).Dispose();
    }

    /// <summary>Closing progress cancels recovery before any connection attempt.</summary>
    [Fact]
    public async Task OverlayCancellationClosesWithoutConnecting()
    {
        using var fixture = new Fixture();
        fixture.Connections.CaptureReconnectTarget().Returns(new VehicleReconnectTarget(Guid.NewGuid(), "Serial", "COM11", 0, 115200));
        var overlay = Substitute.For<IDisposable>();
        fixture.Dialogs.DisplayProgressCancellableAsync(Arg.Any<Func<string>>(),
            Arg.Any<MissionPlanner.App.Utilities.Dialogs.DialogOptions>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                call.Arg<MissionPlanner.App.Utilities.Dialogs.DialogOptions>()!.RequestCancellation!();
                return overlay;
            });
        await fixture.Model.ActivateAsync();
        fixture.Model.MavSystemId = "9";
        await fixture.Model.ApplyCommand.ExecuteAsync(null);
        Assert.Contains("cancelled", fixture.Model.ErrorMessage);
        Assert.True(fixture.Model.ReconnectCommand.CanExecute(null));
        Assert.DoesNotContain(fixture.Connections.ReceivedCalls(), call => call.GetMethodInfo().Name == "ReconnectAsync");
        overlay.Received(1).Dispose();
    }

    private sealed class Fixture : IDisposable
    {
        internal readonly IActiveVehicleContext Active = Substitute.For<IActiveVehicleContext>();
        internal readonly IVehicleParameterService Parameters = Substitute.For<IVehicleParameterService>();
        internal readonly VehicleParameterRegistry Registry = new();
        internal readonly List<string> Reads = [];
        internal readonly List<(VehicleId Id, string Name, float Value, MavParamType Type)> Writes = [];
        internal readonly IVehicleConnectionService Connections = Substitute.For<IVehicleConnectionService>();
        internal readonly MissionPlanner.App.Utilities.Dialogs.IDialogService Dialogs = Substitute.For<MissionPlanner.App.Utilities.Dialogs.IDialogService>();
        internal readonly NamingViewModel Model;
        internal float CurrentSystem = 1;
        internal float CurrentSerial = 42;
        internal bool ConfirmReads = true;
        internal bool ConfirmWrites = true;
        internal bool RejectSystem;

        internal Fixture()
        {
            Active.IsOnline.Returns(true);
            Active.VehicleId.Returns(new VehicleId(1, 1));
            Parameters.RequestParameterAsync(Arg.Any<VehicleId>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
                .Returns(call =>
                {
                    var name = call.Arg<string>()!;
                    Reads.Add(name);
                    if (ConfirmReads)
                    {
                        Store(name, name == "MAV_SYSID" ? CurrentSystem : CurrentSerial);
                    }
                    return true;
                });
            Parameters.SetParameterAsync(Arg.Any<VehicleId>(), Arg.Any<string>(), Arg.Any<float>(), Arg.Any<MavParamType>(), Arg.Any<CancellationToken>())
                .Returns(call =>
                {
                    var name = call.Arg<string>()!;
                    var value = call.Arg<float>();
                    Writes.Add((call.Arg<VehicleId>(), name, value, call.Arg<MavParamType>()));
                    if (RejectSystem && name == "MAV_SYSID")
                    {
                        return false;
                    }
                    if (ConfirmWrites)
                    {
                        if (name == "MAV_SYSID")
                        {
                            CurrentSystem = value;
                        }
                        else
                        {
                            CurrentSerial = value;
                        }
                        Store(name, value);
                    }
                    return true;
                });
            Model = new(Active, Parameters, Registry, new InlineDispatcher(), Substitute.For<IDomainEventHub>(), NullLogger<NamingViewModel>.Instance, Connections, Dialogs);
        }

        internal void Store(string name, float value)
        {
            Registry.StoreParameter(Active.VehicleId!.Value, new(name, value, name == "MAV_SYSID" ? MavParamType.Int16 : MavParamType.Int32, 0, 2),
                TestContext.Current.CancellationToken);
        }

        public void Dispose() => Model.Dispose();
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
