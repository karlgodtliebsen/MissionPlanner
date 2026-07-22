using FluentAssertions;
using Microsoft.Extensions.Logging;
using MissionPlanner.Core.Firmware;
using MissionPlanner.Core.Setup;
using MissionPlanner.Core.Vehicles;
using MissionPlanner.Core.Vehicles.Abstractions;
using MissionPlanner.Core.Vehicles.Models;
using MissionPlanner.Library.DateTime.Domain;
using MissionPlanner.MavLink.Parameters;
using NSubstitute;
using MavParamType = MissionPlanner.MavLink.Parameters.MavParamType;

namespace MissionPlanner.Core.Tests;

/// <summary>Verifies firmware flight-mode slot projection and confirmed slot writes.</summary>
public sealed class FlightModeSetupTests
{
    private static readonly VehicleId vehicleId = new(1, 1);

    /// <summary>Verifies Copter projects FLTMODE slots and resolves the live active slot from PWM.</summary>
    [Fact]
    public void CopterConfigurationProjectsSlotsAndActiveSlot()
    {
        var registry = new VehicleParameterRegistry();
        Store(registry, "FLTMODE_CH", 5);
        Store(registry, "FLTMODE1", 0);  // Stabilize
        Store(registry, "FLTMODE2", 2);  // Alt Hold
        Store(registry, "FLTMODE3", 5);  // Loiter
        Store(registry, "FLTMODE4", 6);  // RTL
        Store(registry, "FLTMODE5", 3);  // Auto
        Store(registry, "FLTMODE6", 9);  // Land
        // Channel 5 at 1400 us falls in band 3 (1361-1490).
        var now = DateTimeOffset.UtcNow;
        var context = new TestActiveVehicleContext(State(FirmwareFamily.ArduCopter, [1500, 1500, 1500, 1500, 1400], now));
        var service = CreateService(context, registry, now);

        var configuration = service.GetConfiguration(vehicleId);

        configuration.IsSupported.Should().BeTrue();
        configuration.ModeChannel.Should().Be(5);
        configuration.Slots.Should().HaveCount(6);
        configuration.Slots.Single(slot => slot.Slot == 1).SelectedModeName.Should().Be("Stabilize");
        configuration.ActiveSlot.Should().Be(3);
        configuration.Slots.Single(slot => slot.Slot == 3).IsActive.Should().BeTrue();
    }

    /// <summary>Verifies Rover uses MODE-prefixed parameters.</summary>
    [Fact]
    public void RoverUsesModeParameters()
    {
        var registry = new VehicleParameterRegistry();
        Store(registry, "MODE_CH", 8);
        Store(registry, "MODE1", 0); // Manual
        var now = DateTimeOffset.UtcNow;
        var context = new TestActiveVehicleContext(State(FirmwareFamily.Rover, [1500], now));
        var service = CreateService(context, registry, now);

        var configuration = service.GetConfiguration(vehicleId);

        configuration.IsSupported.Should().BeTrue();
        configuration.ModeChannel.Should().Be(8);
        configuration.Slots.Single(slot => slot.Slot == 1).SelectedModeName.Should().Be("Manual");
    }

    /// <summary>Verifies a family without a mode channel reports unsupported.</summary>
    [Fact]
    public void UnsupportedFamilyReturnsUnsupported()
    {
        var now = DateTimeOffset.UtcNow;
        var context = new TestActiveVehicleContext(State(FirmwareFamily.ArduSub, [1500], now));
        var service = CreateService(context, new VehicleParameterRegistry(), now);

        service.GetConfiguration(vehicleId).IsSupported.Should().BeFalse();
    }

    /// <summary>Verifies a valid slot write is confirmed and an unknown mode is rejected.</summary>
    [Fact]
    public async Task SetSlotWritesConfirmedModeAndRejectsUnknown()
    {
        var registry = new VehicleParameterRegistry();
        Store(registry, "FLTMODE_CH", 5);
        Store(registry, "FLTMODE1", 0);
        var now = DateTimeOffset.UtcNow;
        var context = new TestActiveVehicleContext(State(FirmwareFamily.ArduCopter, [1500], now));
        var service = CreateService(context, registry, now);

        var rejected = await service.SetSlotAsync(vehicleId, 1, 999, TestContext.Current.CancellationToken);
        rejected.Success.Should().BeFalse();

        var accepted = await service.SetSlotAsync(vehicleId, 1, 5, TestContext.Current.CancellationToken);
        accepted.Success.Should().BeTrue();
        registry.GetParameter(vehicleId, "FLTMODE1")!.Value.Should().Be(5);
    }

    private static FlightModeConfigurationService CreateService(TestActiveVehicleContext context, VehicleParameterRegistry registry, DateTimeOffset now)
    {
        var clock = Substitute.For<IDateTimeProvider>();
        clock.UtcNow.Returns(now);
        var parameterService = Substitute.For<IVehicleParameterService>();
        parameterService.SetParameterAsync(vehicleId, Arg.Any<string>(), Arg.Any<float>(), Arg.Any<MavParamType>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                registry.StoreParameter(vehicleId, new VehicleParameter(call.ArgAt<string>(1), call.ArgAt<float>(2), MavParamType.Int8, 0, 1), CancellationToken.None);
                return Task.FromResult(true);
            });
        return new FlightModeConfigurationService(context, registry, parameterService, new ArduPilotModeCatalog(), clock,
            Substitute.For<ILogger<FlightModeConfigurationService>>());
    }

    private static void Store(VehicleParameterRegistry registry, string name, float value) =>
        registry.StoreParameter(vehicleId, new VehicleParameter(name, value, MavParamType.Int8, 0, 1), CancellationToken.None);

    private static VehicleState State(FirmwareFamily family, ushort[] channels, DateTimeOffset observedAt)
    {
        var now = DateTimeOffset.UtcNow;
        var state = new VehicleState(vehicleId, 0, 2, 3, 0, 4, 3, VehicleConnectionState.Online, now,
            VehicleMode.Stabilize, false, null, null, null, null, null, null, null, null) with
        {
            Flight = new VehicleFlightState(0, 0, 4, VehicleMode.Stabilize, false,
                LandedState: VehicleLandedState.OnGround, ObservedAt: now)
        };
        var firmware = new VehicleFirmwareIdentity(
            family, state.VehicleType, state.Autopilot,
            new FirmwareSemanticVersion(4, 5, 0, FirmwareReleaseType.Official),
            "abcdef01", 0, 1, 2, 3, 42, "vehicle-1");
        return state with
        {
            Identity = state.Identity with { Firmware = firmware },
            Radio = VehicleRadioState.Empty with { ChannelCount = channels.Length, ChannelsRaw = channels, ObservedAt = observedAt }
        };
    }

    private sealed class TestActiveVehicleContext(VehicleState state) : IActiveVehicleContext
    {
        private readonly CancellationTokenSource lifetime = new();

        public ActiveVehicleSnapshot Current { get; } = new(state.VehicleId, state);

        public VehicleId? VehicleId => Current.VehicleId;

        public VehicleState? State => Current.State;

        public bool IsOnline => Current.IsOnline;

        public CancellationToken ConnectionCancellationToken => lifetime.Token;

        public event EventHandler<ActiveVehicleChangedEventArgs>? Changed;
    }
}
