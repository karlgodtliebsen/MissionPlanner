using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using MissionPlanner.Core.Commands;
using MissionPlanner.Core.ConfigTuning;
using MissionPlanner.Core.Diagnostics;
using MissionPlanner.Core.Setup.MandatoryHardware;
using MissionPlanner.Core.Setup.Arming;
using MissionPlanner.Core.Vehicles;
using MissionPlanner.Core.Vehicles.Abstractions;
using MissionPlanner.Library.Factory.Domain.Abstractions;
using MissionPlanner.MavLink.Parameters;
using MissionPlanner.Shared.Models.Vehicles.Models;
using NSubstitute;

namespace MissionPlanner.Core.Tests;

/// <summary>Exercises semantic arming configuration through the actual verified parameter editor.</summary>
public sealed class ArmingConfigurationTests
{
    /// <summary>Special All semantics remain distinct from metadata-backed custom masks.</summary>
    [Theory]
    [InlineData(0, PreArmCheckMode.Disabled)]
    [InlineData(1, PreArmCheckMode.All)]
    [InlineData(3, PreArmCheckMode.All)]
    [InlineData(6, PreArmCheckMode.Custom)]
    public async Task ReadsChecks(int mask, PreArmCheckMode expected)
    {
        var f = new Fixture();
        f.Store("ARMING_CHECK", mask);
        var state = await f.Service.ReadAsync(f.Id, TestContext.Current.CancellationToken);
        Assert.Equal(expected, state.Current.Checks);
        Assert.Equal(StickArmingMode.ArmAndDisarm, state.Current.Stick);
        Assert.Equal(8, state.Current.ArmSwitch);
        Assert.Equal(2, state.Settings[0].Bits.Count);
    }

    /// <summary>RC moves clear the old assignment and confirm the new one.</summary>
    [Theory]
    [InlineData(10)]
    [InlineData(0)]
    public async Task MovesOrClearsSwitch(int channel)
    {
        var f = new Fixture();
        var state = await f.Service.ReadAsync(f.Id, TestContext.Current.CancellationToken);
        var plan = await f.Service.EvaluateChangesAsync(f.Id, state.Current with { ArmSwitch = channel }, TestContext.Current.CancellationToken);
        Assert.True(plan.CanApply);
        Assert.Equal("RC8_OPTION", plan.Changes[0].Name);
        var result = await f.Service.ApplyAsync(f.Id, plan, TestContext.Current.CancellationToken);
        Assert.True(result.Success, result.Message);
        Assert.Equal(channel, result.Actual!.Current.ArmSwitch);
    }

    /// <summary>Primary/flight-mode channels and occupied channels cannot be overwritten.</summary>
    [Theory]
    [InlineData(1)]
    [InlineData(5)]
    [InlineData(10)]
    public async Task RejectsConflicts(int channel)
    {
        var f = new Fixture();
        f.Store("RC10_OPTION", 32);
        var state = await f.Service.ReadAsync(f.Id, TestContext.Current.CancellationToken);
        var plan = await f.Service.EvaluateChangesAsync(f.Id, state.Current with { ArmSwitch = channel }, TestContext.Current.CancellationToken);
        Assert.False(plan.CanApply);
        Assert.NotEmpty(plan.Errors);
        Assert.Empty(f.Writes);
    }

    /// <summary>Multiple assignments are reported without collapsing them.</summary>
    [Fact]
    public async Task MultipleAssignmentsBlockSimpleReassignment()
    {
        var f = new Fixture(); f.Store("RC10_OPTION", 153);
        var state = await f.Service.ReadAsync(f.Id, TestContext.Current.CancellationToken);
        Assert.Equal(new[] { 8, 10 }, state.ArmSwitches);
        Assert.Null(state.Current.ArmSwitch);
        Assert.False((await f.Service.EvaluateChangesAsync(f.Id, state.Current with { ArmSwitch = 0 }, TestContext.Current.CancellationToken)).CanApply);
    }

    /// <summary>Only metadata-derived custom bits can be selected, without the All bit.</summary>
    [Theory]
    [InlineData(6, true)]
    [InlineData(7, false)]
    [InlineData(16, false)]
    [InlineData(0, false)]
    public async Task CustomMaskValidation(int mask, bool valid)
    {
        var f = new Fixture(); var state = await f.Service.ReadAsync(f.Id, TestContext.Current.CancellationToken);
        var plan = await f.Service.EvaluateChangesAsync(f.Id, state.Current with { Checks = PreArmCheckMode.Custom, CustomChecks = mask }, TestContext.Current.CancellationToken);
        Assert.Equal(valid, plan.CanApply);
    }

    /// <summary>Unverified writes stop the plan and preserve confirmed readback.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task PartialFailure(bool mismatch)
    {
        var f = new Fixture(); f.FailName = "ARMING_RUDDER"; f.Mismatch = mismatch;
        var state = await f.Service.ReadAsync(f.Id, TestContext.Current.CancellationToken);
        var plan = await f.Service.EvaluateChangesAsync(f.Id, state.Current with { Checks = PreArmCheckMode.Disabled, Stick = StickArmingMode.Disabled }, TestContext.Current.CancellationToken);
        Assert.Contains(plan.Warnings, w => w.Contains("disabled"));
        var result = await f.Service.ApplyAsync(f.Id, plan, TestContext.Current.CancellationToken);
        Assert.False(result.Success); Assert.Single(result.Confirmed); Assert.True(result.RequiresReboot);
        Assert.Equal(PreArmCheckMode.Disabled, result.Actual!.Current.Checks);
        Assert.Equal(StickArmingMode.ArmAndDisarm, result.Actual.Current.Stick);
    }

    /// <summary>Changed values and armed state invalidate the reviewed plan.</summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task RejectsStaleOrArmedReview(bool armed)
    {
        var f = new Fixture(); var state = await f.Service.ReadAsync(f.Id, TestContext.Current.CancellationToken);
        var plan = await f.Service.EvaluateChangesAsync(f.Id, state.Current with { Checks = PreArmCheckMode.Disabled }, TestContext.Current.CancellationToken);
        if (armed) { f.Active.State.Returns(f.Active.State! with { Flight = f.Active.State!.Flight with { IsArmed = true } }); }
        else { f.Store("ARMING_RUDDER", 0); }
        Assert.False((await f.Service.ApplyAsync(f.Id, plan, TestContext.Current.CancellationToken)).Success);
        Assert.Empty(f.Writes);
    }

    /// <summary>Partial downloads export no assignments and enable no editors.</summary>
    [Fact]
    public async Task LoadingThenComplete()
    {
        var f = new Fixture();
        var complete = f.Loads.Get(f.Id)!;
        f.Loads.Get(f.Id).Returns(complete with { State = MissionPlanner.Core.Vehicles.Models.ParameterLoadState.Downloading });
        var state = await f.Service.ReadAsync(f.Id, TestContext.Current.CancellationToken);
        Assert.False(state.ParametersReady); Assert.Empty(state.Evidence);
        Assert.All(state.Settings, s => Assert.False(s.CanEdit));
        f.Loads.Get(f.Id).Returns(complete);
        Assert.True((await f.Service.ReadAsync(f.Id, TestContext.Current.CancellationToken)).ParametersReady);
    }

    /// <summary>Missing capability and defaults remain unknown, not false or zero.</summary>
    [Fact]
    public async Task MetadataAndMissingParameters()
    {
        var f = new Fixture(); f.Metadata.Clear();
        var state = await f.Service.ReadAsync(f.Id, TestContext.Current.CancellationToken);
        Assert.All(state.Settings, s => Assert.Null(s.Default));
        Assert.Empty(state.Settings[0].Bits);
        Assert.False(state.Settings[1].CanEdit);
        f.Registry.ClearParameters(f.Id);
        state = await f.Service.ReadAsync(f.Id, TestContext.Current.CancellationToken);
        Assert.Null(state.Current.RequireLocation); Assert.Null(state.Current.Requirement);
        Assert.False(state.AssignmentsKnown);
    }

    /// <summary>Firmware families use their reported metadata rather than assumed Copter values.</summary>
    [Theory]
    [InlineData(MissionPlanner.Firmware.FirmwareFamily.ArduCopter)]
    [InlineData(MissionPlanner.Firmware.FirmwareFamily.ArduPlane)]
    [InlineData(MissionPlanner.Firmware.FirmwareFamily.Rover)]
    public async Task FirmwareFamilies(MissionPlanner.Firmware.FirmwareFamily family)
    {
        var f = new Fixture();
        var vehicle = f.Active.State!;
        f.Active.State.Returns(vehicle with { Identity = vehicle.Identity with { Firmware = vehicle.Identity.Firmware with { Family = family } } });
        var state = await f.Service.ReadAsync(f.Id, TestContext.Current.CancellationToken);
        Assert.True(state.IsSupported);
        Assert.Equal(1, state.Current.Requirement);
    }

    /// <summary>All three stick modes are represented semantically.</summary>
    [Theory]
    [InlineData(0, StickArmingMode.Disabled)]
    [InlineData(1, StickArmingMode.ArmOnly)]
    [InlineData(2, StickArmingMode.ArmAndDisarm)]
    public async Task StickModes(int value, StickArmingMode expected)
    {
        var f = new Fixture(); f.Store("ARMING_RUDDER", value);
        Assert.Equal(expected, (await f.Service.ReadAsync(f.Id, TestContext.Current.CancellationToken)).Current.Stick);
    }

    /// <summary>A reconnected session cannot reuse a review from the old connection.</summary>
    [Fact]
    public async Task ReconnectInvalidatesReview()
    {
        var f = new Fixture(); var state = await f.Service.ReadAsync(f.Id, TestContext.Current.CancellationToken);
        var plan = await f.Service.EvaluateChangesAsync(f.Id, state.Current with { Checks = PreArmCheckMode.Disabled }, TestContext.Current.CancellationToken);
        using var connection = new CancellationTokenSource();
        f.Active.ConnectionCancellationToken.Returns(connection.Token);
        Assert.False((await f.Service.ApplyAsync(f.Id, plan, TestContext.Current.CancellationToken)).Success);
        Assert.Empty(f.Writes);
    }

    /// <summary>A connection lost during a write prevents every subsequent write.</summary>
    [Fact]
    public async Task ConnectionBoundaryDuringApply()
    {
        var f = new Fixture(); var state = await f.Service.ReadAsync(f.Id, TestContext.Current.CancellationToken);
        var plan = await f.Service.EvaluateChangesAsync(f.Id, state.Current with { Checks = PreArmCheckMode.Disabled, Stick = StickArmingMode.Disabled }, TestContext.Current.CancellationToken);
        f.AfterWrite = () => f.Active.IsOnline.Returns(false);
        Assert.False((await f.Service.ApplyAsync(f.Id, plan, TestContext.Current.CancellationToken)).Success);
        Assert.Single(f.Writes);
    }

    /// <summary>Firmware metadata can forbid the otherwise familiar auxiliary function.</summary>
    [Fact]
    public async Task FirmwareDoesNotAdvertiseArmOption()
    {
        var f = new Fixture();
        f.Metadata["RC10_OPTION"] = f.Metadata["RC10_OPTION"] with { Values = "0:None,32:Interlock" };
        var state = await f.Service.ReadAsync(f.Id, TestContext.Current.CancellationToken);
        var plan = await f.Service.EvaluateChangesAsync(f.Id, state.Current with { ArmSwitch = 10 }, TestContext.Current.CancellationToken);
        Assert.False(plan.CanApply); Assert.Contains(plan.Errors, e => e.Contains("RC10_OPTION"));
    }

    internal sealed class Fixture
    {
        internal readonly VehicleId Id = new(1, 1);
        internal readonly IActiveVehicleContext Active = Substitute.For<IActiveVehicleContext>();
        internal readonly VehicleParameterRegistry Registry = new();
        internal readonly Dictionary<string, ParameterMetadata> Metadata = new();
        internal readonly List<string> Writes = [];
        internal readonly ArmingConfigurationService Service;
        internal readonly IVehicleParameterLoadStatusContext Loads = Substitute.For<IVehicleParameterLoadStatusContext>();
        internal readonly IVehicleLiveDiagnostics Diagnostics = Substitute.For<IVehicleLiveDiagnostics>();
        internal string? FailName;
        internal bool Mismatch;
        internal Action? AfterWrite;
        internal Fixture()
        {
            Active.VehicleId.Returns(Id);
            Active.IsOnline.Returns(true);
            var state = VehicleLiveDiagnosticsTests.State(Id);
            Active.State.Returns(state with { Health = state.Health with { SensorsPresent = 4, SensorsEnabled = 4, SensorsHealthy = 4, SystemObservedAt = DateTimeOffset.UtcNow } });
            foreach (var pair in new[] { ("ARMING_CHECK", 1), ("ARMING_RUDDER", 2), ("ARMING_NEED_LOC", 0), ("ARMING_REQUIRE", 1), ("FLTMODE_CH", 5), ("RCMAP_ROLL", 1), ("RCMAP_PITCH", 2), ("RCMAP_THROTTLE", 3), ("RCMAP_YAW", 4) })
            {
                Store(pair.Item1, pair.Item2);
                Metadata[pair.Item1] = new(pair.Item1, null, null, null, null, null, "0:Disabled,1:Arm only,2:Arm and disarm", "0:All,1:Barometer,2:Compass", null, null, true, false) { DefaultValue = 1 };
            }
            for (var c = 1; c <= 16; c++)
            {
                var name = $"RC{c}_OPTION";
                Store(name, c == 8 ? 153 : 0);
                Metadata[name] = new(name, null, null, null, null, null, "0:None,153:Arm/Disarm", null, null, null, false, false);
            }
            var metadata = Substitute.For<IVehicleParameterMetadataService>();
            metadata.GetAllMetadataAsync(Id, Arg.Any<CancellationToken>()).Returns(Metadata);
            var parameters = Substitute.For<IVehicleParameterService>();
            parameters.SetParameterAsync(Id, Arg.Any<string>(), Arg.Any<float>(), Arg.Any<MavParamType>(), Arg.Any<CancellationToken>()).Returns(call =>
            {
                var name = call.Arg<string>()!;
                Writes.Add(name);
                if (name == FailName)
                {
                    return Mismatch;
                }
                Store(name, call.Arg<float>());
                AfterWrite?.Invoke();
                return true;
            });
            var factory = Substitute.For<IDomainFactory>();
            factory.Create<IParameterEditSession, ParameterEditScope>(Arg.Any<ParameterEditScope>()).Returns(call =>
                new ParameterEditSession(call.Arg<ParameterEditScope>()!, Active, Registry, parameters, metadata,
                    Options.Create(new ParameterEditSessionOptions { ReadbackTimeout = TimeSpan.FromMilliseconds(30) }), NullLogger<ParameterEditSession>.Instance));
            Diagnostics.GetArming(Id).Returns(new VehicleArmingDiagnostic("Unknown", false, null, [], null, null, null));
            var loads = Loads;
            loads.Get(Id).Returns(new MissionPlanner.Core.Vehicles.Models.ParameterLoadStatus(Id, MissionPlanner.Core.Vehicles.Models.ParameterLoadState.Completed, 30, 30, 100, "Loaded", DateTimeOffset.UtcNow));
            Service = new(Active, Registry, metadata, loads, new RadioArmingConfiguration(Active, Registry, factory), factory, new VehicleOperationGate());
        }
        internal void Store(string name, float value) => Registry.StoreParameter(Id,
            new VehicleParameter(name, value, MavParamType.Int32, 0, 1), CancellationToken.None);
    }
}
