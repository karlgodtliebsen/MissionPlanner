using MissionPlanner.Core.ConfigTuning;
using MissionPlanner.Core.Setup.MandatoryHardware;
using MissionPlanner.Core.Vehicles;
using MissionPlanner.Core.Vehicles.Abstractions;
using MissionPlanner.Library.Factory.Domain.Abstractions;
using MissionPlanner.MavLink.Parameters;
using MissionPlanner.Shared.Models.Vehicles.Models;
using NSubstitute;

namespace MissionPlanner.Core.Tests;

/// <summary>Verifies switch observation is separate from guarded arming configuration.</summary>
public sealed class RadioArmingConfigurationTests
{
    /// <summary>Working RC5 movement does not imply it is configured as an arm switch.</summary>
    [Fact]
    public void MovementAndConfigurationAreIndependent()
    {
        var fixture = new Fixture();
        var trace = new RadioSwitchMovement();
        foreach (ushort pwm in new ushort[] { 999, 2000, 999 })
        {
            trace.Observe(MissionPlanner.Core.Vehicles.Models.VehicleRadioState.Empty with
            {
                ChannelsRaw = new ushort[] { 1500, 1500, 1000, 1500, pwm }, ChannelCount = 5
            });
        }
        Assert.Contains("999 → 2000 → 999", trace.Describe(5));
        var summary = RadioArmingConfiguration.Describe(name => fixture.Registry.GetParameter(Fixture.Id, name)?.Value);
        Assert.Contains("Flight mode channel: RC5", summary);
        Assert.Contains("Not configured", summary);
        Assert.Contains("Stick arming: Enabled", summary);
        trace.Clear();
        Assert.Null(trace.Describe(5));
        Assert.Equal(summary, RadioArmingConfiguration.Describe(name => fixture.Registry.GetParameter(Fixture.Id, name)?.Value));
    }

    /// <summary>Mode, primary, occupied, and unknown channels are never silently overwritten.</summary>
    [Theory]
    [InlineData(5, 0)]
    [InlineData(1, 0)]
    [InlineData(8, 7)]
    [InlineData(9, 0)]
    public async Task ConflictsPreventWrites(int channel, float option)
    {
        var fixture = new Fixture();
        fixture.Store("RC8_OPTION", option);
        await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Service.AssignAsync(Fixture.Id, channel, TestContext.Current.CancellationToken));
        Assert.Empty(fixture.Session.ReceivedCalls());
    }

    /// <summary>A free RC8 is assigned through the existing metadata and readback session only.</summary>
    [Fact]
    public async Task AssignsOnlyFreeChannelAndHonorsMetadata()
    {
        var fixture = new Fixture();
        var metadata = ParameterFieldMetadata.Empty with { Options = [new(0, "None"), new(153, "Arm/Disarm")] };
        fixture.Session.GetField("RC8_OPTION").Returns(new ParameterEditField("RC8_OPTION", MavParamType.Int32, 0, 0, 0, metadata, null));
        fixture.Session.TrySetPending("RC8_OPTION", 153, out Arg.Any<string?>()).Returns(true);
        var plan = new ParameterWritePlan(new(Fixture.Id, fixture.Active.State!.Identity.Firmware), DateTimeOffset.UtcNow,
            [new("RC8_OPTION", "Arm/Disarm", 0, 153, null, 153, false, false, null)]);
        fixture.Session.CreateWritePlan(Arg.Any<IReadOnlyList<string>>()).Returns(plan);
        fixture.Session.ApplyAsync(plan, null, Arg.Any<CancellationToken>()).Returns(new ParameterApplyReport(true, [], false));
        Assert.Contains("verified", await fixture.Service.AssignAsync(Fixture.Id, 8, TestContext.Current.CancellationToken));
        await fixture.Session.Received(1).ApplyAsync(plan, null, Arg.Any<CancellationToken>());
        Assert.Equal("RC8_OPTION", Assert.Single(plan.Names));
        fixture.Session.GetField("RC8_OPTION").Returns(new ParameterEditField("RC8_OPTION", MavParamType.Int32, 0, 0, 0,
            metadata with { Options = [new(0, "None")] }, null));
        await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Service.AssignAsync(Fixture.Id, 8, TestContext.Current.CancellationToken));
        await fixture.Session.Received(1).ApplyAsync(plan, null, Arg.Any<CancellationToken>());
    }

    /// <summary>Disconnect and arming invalidate assignment before metadata or writes are requested.</summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task UnsafeVehiclePreventsAssignment(bool armed)
    {
        var fixture = new Fixture();
        fixture.Active.IsOnline.Returns(armed);
        var state = fixture.Active.State!;
        fixture.Active.State.Returns(state with { Flight = state.Flight with { IsArmed = armed } });
        await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Service.AssignAsync(Fixture.Id, 8, TestContext.Current.CancellationToken));
        Assert.Empty(fixture.Session.ReceivedCalls());
    }

    private sealed class Fixture
    {
        internal static readonly VehicleId Id = new(1, 1);
        internal readonly IActiveVehicleContext Active = Substitute.For<IActiveVehicleContext>();
        internal readonly VehicleParameterRegistry Registry = new();
        internal readonly IParameterEditSession Session = Substitute.For<IParameterEditSession>();
        internal readonly RadioArmingConfiguration Service;

        internal Fixture()
        {
            Active.VehicleId.Returns(Id);
            Active.IsOnline.Returns(true);
            Active.State.Returns(VehicleLiveDiagnosticsTests.State(Id));
            var factory = Substitute.For<IDomainFactory>();
            factory.Create<IParameterEditSession, ParameterEditScope>(Arg.Any<ParameterEditScope>()).Returns(Session);
            Service = new(Active, Registry, factory);
            Store("FLTMODE_CH", 5);
            Store("ARMING_RUDDER", 2);
            Store("RC1_OPTION", 0);
            Store("RC5_OPTION", 0);
            Store("RC8_OPTION", 0);
            Store("RCMAP_ROLL", 1);
            Store("RCMAP_PITCH", 2);
            Store("RCMAP_THROTTLE", 3);
            Store("RCMAP_YAW", 4);
        }

        internal void Store(string name, float value) => Registry.StoreParameter(Id,
            new VehicleParameter(name, value, MavParamType.Int32, 0, 1), CancellationToken.None);
    }
}
