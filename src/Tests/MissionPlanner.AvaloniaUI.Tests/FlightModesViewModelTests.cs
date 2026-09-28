using Microsoft.Extensions.Logging.Abstractions;
using MissionPlanner.App.Utilities.Dispatching;
using MissionPlanner.App.Views.InitSetup.MandatoryHardware.Sections;
using MissionPlanner.App.Views.Navigation;
using MissionPlanner.Core.Setup.MandatoryHardware;
using MissionPlanner.Core.Vehicles;
using MissionPlanner.Core.Vehicles.Abstractions;
using MissionPlanner.Core.Vehicles.Models;
using MissionPlanner.Firmware;
using MissionPlanner.Firmware.Model;
using MissionPlanner.Library.DateTime.Domain;
using MissionPlanner.Library.EventHub.Abstractions;
using MissionPlanner.MavLink.Parameters;
using MissionPlanner.Shared.Models.Vehicles.Models;
using NSubstitute;

namespace MissionPlanner.AvaloniaUI.Tests;

/// <summary>Exercises received mode parameters through the real service, picker and explicit write workflow.</summary>
public sealed class FlightModesViewModelTests
{
    [Fact]
    public async Task LoadingAcroUnknownAndMissingSlotsNeverWrites()
    {
        using var fixture = new Fixture();
        fixture.Store("FLTMODE1", 1);
        fixture.Store("FLTMODE4", 123);
        await fixture.Model.ActivateAsync();
        var acro = fixture.Model.Slots[0];
        Assert.Equal("Acro", acro.SelectedMode!.Name);
        Assert.Equal(1f, acro.ReceivedModeNumber);
        Assert.False(acro.CanApply);
        var unknown = fixture.Model.Slots[3];
        Assert.Equal("Unknown mode (123)", unknown.SelectedMode!.Name);
        Assert.Equal(123f, unknown.SelectedMode.ModeNumber);
        Assert.Contains(unknown.SelectedMode, unknown.Options);
        Assert.False(unknown.CanApply);
        Assert.DoesNotContain(acro.Options, option => option.ModeNumber == 123);
        var missing = fixture.Model.Slots[5];
        Assert.Equal("Parameter not loaded", missing.SelectedMode!.Name);
        Assert.Null(missing.ReceivedModeNumber);
        Assert.False(missing.IsLoaded);
        Assert.False(missing.CanApply);
        fixture.Model.RefreshCommand.Execute(null);
        fixture.Store("FLTMODE6", 1); // Late parameter arrival must not be treated as an edit.
        Assert.Equal("Acro", fixture.Model.Slots[5].SelectedMode!.Name);
        Assert.All(fixture.Model.Slots, slot => Assert.False(slot.CanApply));
        Assert.Empty(fixture.Writes);
    }

    [Fact]
    public async Task ApplyingAcroWritesOnlyChosenSlotAndPreservesUnknownUntilExplicitReplacement()
    {
        using var fixture = new Fixture();
        fixture.Store("FLTMODE1", 123);
        fixture.Store("FLTMODE4", 0);
        await fixture.Model.ActivateAsync();
        var changed = fixture.Model.Slots[3];
        changed.SelectedMode = changed.Options.Single(option => option.Name == "Acro");
        Assert.True(changed.CanApply);
        Assert.Empty(fixture.Writes);
        fixture.Model.RefreshCommand.Execute(null);
        Assert.True(changed.CanApply);
        await changed.ApplyCommand.ExecuteAsync(null);
        Assert.Equal(new[] { (fixture.Id, "FLTMODE4", 1f) }, fixture.Writes);
        Assert.Equal(123f, fixture.Model.Slots[0].ReceivedModeNumber);
        Assert.Equal("Unknown mode (123)", fixture.Model.Slots[0].SelectedMode!.Name);
        Assert.False(changed.CanApply);

        var unknown = fixture.Model.Slots[0];
        unknown.SelectedMode = unknown.Options.Single(option => option.Name == "Loiter");
        await unknown.ApplyCommand.ExecuteAsync(null);
        Assert.Equal((fixture.Id, "FLTMODE1", 5f), fixture.Writes[1]);
        Assert.Equal(5f, fixture.Model.Slots[0].ReceivedModeNumber);
        Assert.DoesNotContain(fixture.Model.Slots[0].Options, option => option.ModeNumber == 123);
        await fixture.Service.SetSlotAsync(fixture.Id, 1, 5, TestContext.Current.CancellationToken);
        Assert.Equal(2, fixture.Writes.Count); // Reapplying a confirmed value is a no-op.
    }

    [Fact]
    public async Task VehicleSwitchAndReconnectDiscardOldDraftWithoutWriting()
    {
        using var fixture = new Fixture();
        fixture.Store("FLTMODE1", 0);
        await fixture.Model.ActivateAsync();
        var oldRow = fixture.Model.Slots[0];
        oldRow.SelectedMode = oldRow.Options.Single(option => option.Name == "Acro");
        fixture.SwitchVehicle(new VehicleId(2, 1), FirmwareFamily.ArduPlane);
        fixture.Store("FLTMODE1", 1);
        Assert.Equal("Circle", fixture.Model.Slots[0].SelectedMode!.Name);
        Assert.Equal(4f, fixture.Model.Slots[0].Options.Single(option => option.Name == "Acro").ModeNumber);
        Assert.False(fixture.Model.Slots[0].CanApply);
        await oldRow.ApplyCommand.ExecuteAsync(null);
        Assert.Empty(fixture.Writes);
        fixture.Model.Slots[0].SelectedMode = fixture.Model.Slots[0].Options.Single(option => option.Name == "Acro");
        fixture.SetOnline(false);
        Assert.Empty(fixture.Model.Slots);
        fixture.SetOnline(true);
        Assert.Equal("Circle", fixture.Model.Slots[0].SelectedMode!.Name);
        Assert.False(fixture.Model.Slots[0].CanApply);
        await fixture.Model.DeactivateAsync();
        await fixture.Model.ActivateAsync();
        Assert.Equal("Circle", fixture.Model.Slots[0].SelectedMode!.Name);
        Assert.False(fixture.Model.Slots[0].CanApply);
        Assert.Empty(fixture.Writes);
    }

    [Theory]
    [InlineData(1000, 1)]
    [InlineData(1500, 4)]
    [InlineData(2000, 6)]
    public async Task Rc6SelectsSlotWithoutChangingTelemetryModeOrAssignments(int pwm, int selectedSlot)
    {
        using var fixture = new Fixture(pwm);
        fixture.Store($"FLTMODE{selectedSlot}", 1);
        await fixture.Model.ActivateAsync();
        Assert.Equal(selectedSlot, Assert.Single(fixture.Model.Slots, slot => slot.IsActive).Slot);
        Assert.Equal("Acro", fixture.Model.Slots[selectedSlot - 1].SelectedMode!.Name);
        Assert.Equal(VehicleMode.Stabilize, fixture.Active.State!.Mode);
        Assert.Empty(fixture.Writes);
    }

    private sealed class Fixture : IDisposable
    {
        public VehicleId Id = new(1, 1);
        public readonly IActiveVehicleContext Active = Substitute.For<IActiveVehicleContext>();
        public readonly VehicleParameterRegistry Registry = new();
        public readonly List<(VehicleId, string, float)> Writes = [];
        public readonly FlightModesSetupViewModel Model;
        public readonly FlightModeConfigurationService Service;
        private readonly DateTimeOffset now = DateTimeOffset.UtcNow;

        public Fixture(int pwm = 1500)
        {
            SetVehicle(FirmwareFamily.ArduCopter, pwm);
            var clock = Substitute.For<IDateTimeProvider>();
            clock.UtcNow.Returns(now);
            var protocol = Substitute.For<IVehicleParameterService>();
            protocol.SetParameterAsync(Arg.Any<VehicleId>(), Arg.Any<string>(), Arg.Any<float>(), Arg.Any<MavParamType>(), Arg.Any<CancellationToken>())
                .Returns(call =>
                {
                    var id = call.ArgAt<VehicleId>(0);
                    var name = call.ArgAt<string>(1);
                    var value = call.ArgAt<float>(2);
                    Writes.Add((id, name, value));
                    Registry.StoreParameter(id, new VehicleParameter(name, value, MavParamType.Int32, 0, 1), CancellationToken.None);
                    return Task.FromResult(true);
                });
            Service = new FlightModeConfigurationService(Active, Registry, protocol, new ArduPilotModeCatalog(), clock,
                NullLogger<FlightModeConfigurationService>.Instance);
            var dispatcher = Substitute.For<IUiDispatcher>();
            dispatcher.When(d => d.Dispatch(Arg.Any<Action>())).Do(call => call.Arg<Action>()!());
            Model = new FlightModesSetupViewModel(Active, Service, NullLogger<FlightModesSetupViewModel>.Instance,
                Substitute.For<INavigationService>(), Registry, dispatcher, Substitute.For<IDomainEventHub>());
            Store("FLTMODE_CH", 6);
        }

        public void Store(string name, float value) => Registry.StoreParameter(Id,
            new VehicleParameter(name, value, MavParamType.Int32, 0, 1), CancellationToken.None);

        public void SwitchVehicle(VehicleId id, FirmwareFamily family)
        {
            var previous = new ActiveVehicleSnapshot(Id, Active.State);
            Id = id;
            SetVehicle(family, 1500);
            Active.Changed += Raise.Event<Action<ActiveVehicleChangedEventArgs>>(
                new ActiveVehicleChangedEventArgs(previous, new ActiveVehicleSnapshot(Id, Active.State)));
            Store("FLTMODE_CH", 6);
        }

        public void SetOnline(bool online)
        {
            var previous = new ActiveVehicleSnapshot(Id, Active.State);
            var state = Active.State! with
            {
                Connection = Active.State!.Connection with
                {
                    State = online ? VehicleConnectionState.Online : VehicleConnectionState.Offline
                }
            };
            Active.State.Returns(state);
            Active.IsOnline.Returns(online);
            Active.Changed += Raise.Event<Action<ActiveVehicleChangedEventArgs>>(
                new ActiveVehicleChangedEventArgs(previous, new ActiveVehicleSnapshot(Id, state)));
        }

        private void SetVehicle(FirmwareFamily family, int pwm)
        {
            var type = family == FirmwareFamily.ArduPlane ? (byte)1 : (byte)2;
            var state = new VehicleState(Id, 0, type, 3, 0, 4, 3, VehicleConnectionState.Online, now,
                VehicleMode.Stabilize, false, null, null, null, null, null, null, null, null);
            state = state with
            {
                Identity = state.Identity with
                {
                    Firmware = new VehicleFirmwareIdentity(family, type, 3,
                        new FirmwareSemanticVersion(4, 7, 1, FirmwareReleaseType.Official), "test", 0, 1, 2, 3, 42, "test")
                },
                Radio = VehicleRadioState.Empty with
                {
                    ChannelsRaw = new ushort[] { 1500, 1500, 1000, 1500, 1500, (ushort)pwm },
                    ChannelCount = 6, ObservedAt = now
                }
            };
            Active.VehicleId.Returns(Id);
            Active.State.Returns(state);
            Active.IsOnline.Returns(true);
        }

        public void Dispose() => Model.Dispose();
    }
}
