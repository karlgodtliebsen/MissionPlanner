using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using MissionPlanner.Core.Commands;
using MissionPlanner.Core.ConfigTuning;
using MissionPlanner.Core.Diagnostics;
using MissionPlanner.Core.Setup.MandatoryHardware;
using MissionPlanner.Core.Vehicles;
using MissionPlanner.Core.Vehicles.Abstractions;
using MissionPlanner.Library.Factory.Domain.Abstractions;
using MissionPlanner.MavLink.Parameters;
using MissionPlanner.Shared.Models.Vehicles.Models;
using NSubstitute;

namespace MissionPlanner.Core.Tests;

/// <summary>Verifies semantic compass configuration through the actual parameter editor.</summary>
public sealed class CompassConfigurationTests
{
    /// <summary>Absent values remain unknown, separate from disabled and unhealthy.</summary>
    [Theory]
    [InlineData(0, 0, "Disabled")]
    [InlineData(1, 0, "Not detected")]
    [InlineData(1, 123456, "Healthy")]
    public async Task ReadsSemanticState(int enabled, int device, string health)
    {
        var f = new Fixture();
        f.Store("COMPASS_ENABLE", enabled);
        f.Store("COMPASS_DEV_ID", device);
        var state = await f.Service.ReadAsync(f.Id, TestContext.Current.CancellationToken);
        Assert.Equal(health, state.Health);
        Assert.Equal(enabled != 0, state.Current.Enabled);
        Assert.Null(state.Current.UseSecondary);
        Assert.False(state.Settings.Single(s => s.Setting == CompassSetting.SecondaryUse).CanEdit);
        Assert.Equal(0, state.Settings.Single(s => s.Setting == CompassSetting.PrimaryOrientation).Default);
        Assert.DoesNotContain(typeof(CompassConfiguration).GetProperties(), p => p.Name.Contains("COMPASS_"));
    }

    /// <summary>Yaw and use dependencies are reviewed and confirmed before disabling the subsystem.</summary>
    [Fact]
    public async Task DisablingReviewsAndVerifiesDependencies()
    {
        var f = new Fixture();
        f.Store("EK3_SRC1_YAW", 1);
        var state = await f.Service.ReadAsync(f.Id, TestContext.Current.CancellationToken);
        var changes = await f.Service.EvaluateChangesAsync(f.Id, state.Current with { Enabled = false }, TestContext.Current.CancellationToken);
        Assert.True(changes.CanApply);
        Assert.Equal(new[] { "EK3_SRC1_YAW", "COMPASS_USE", "COMPASS_ENABLE" }, changes.Changes.Select(c => c.Name));
        Assert.Contains(changes.Changes, c => c.Reason.Contains("dependency"));
        var result = await f.Service.ApplyAsync(f.Id, changes, TestContext.Current.CancellationToken);
        Assert.True(result.Success);
        Assert.True(result.RequiresReboot);
        Assert.False(result.Actual!.Current.Enabled);
        Assert.Equal(CompassYawSource.None, result.Actual.Current.YawSource);
        Assert.Equal(changes.Changes.Select(c => c.Name), f.Writes);
    }

    /// <summary>A failed write/readback stops later writes and retains confirmed actual state.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task PartialFailureKeepsActualState(bool mismatch)
    {
        var f = new Fixture();
        f.Store("COMPASS_ENABLE", 0);
        f.FailName = "COMPASS_ORIENT";
        f.Mismatch = mismatch;
        var state = await f.Service.ReadAsync(f.Id, TestContext.Current.CancellationToken);
        var changes = await f.Service.EvaluateChangesAsync(f.Id, state.Current with { Enabled = true, PrimaryOrientation = 1 }, TestContext.Current.CancellationToken);
        var result = await f.Service.ApplyAsync(f.Id, changes, TestContext.Current.CancellationToken);
        Assert.False(result.Success);
        Assert.True(result.Actual!.Current.Enabled);
        Assert.Equal(0, result.Actual.Current.PrimaryOrientation);
        Assert.Single(result.Confirmed);
        Assert.True(result.RequiresReboot);
    }

    /// <summary>Live conflicts and alternate yaw dependencies prevent writes.</summary>
    [Fact]
    public async Task RejectsConflictsAndAlternateYawDependencies()
    {
        var f = new Fixture();
        var state = await f.Service.ReadAsync(f.Id, TestContext.Current.CancellationToken);
        var changes = await f.Service.EvaluateChangesAsync(f.Id, state.Current with { PrimaryOrientation = 1 }, TestContext.Current.CancellationToken);
        f.Store("COMPASS_USE", 0);
        Assert.False((await f.Service.ApplyAsync(f.Id, changes, TestContext.Current.CancellationToken)).Success);
        Assert.Empty(f.Writes);
        f.Store("EK3_SRC2_YAW", 3);
        changes = await f.Service.EvaluateChangesAsync(f.Id, state.Current with { Enabled = false }, TestContext.Current.CancellationToken);
        Assert.Contains(changes.Errors, e => e.Contains("EK3_SRC2_YAW"));
    }

    /// <summary>Missing metadata does not fabricate enum capability or defaults.</summary>
    [Fact]
    public async Task UnsupportedAndUnknownCapabilitiesAreExplicit()
    {
        var f = new Fixture();
        f.Metadata.Clear();
        var state = await f.Service.ReadAsync(f.Id, TestContext.Current.CancellationToken);
        Assert.False(state.Settings.Single(s => s.Setting == CompassSetting.YawSource).CanEdit);
        Assert.Null(state.Settings.Single(s => s.Setting == CompassSetting.Enabled).Default);
        var vehicle = f.Active.State!;
        f.Active.State.Returns(vehicle with { Identity = vehicle.Identity with { Firmware = vehicle.Identity.Firmware with { Autopilot = 12 } } });
        state = await f.Service.ReadAsync(f.Id, TestContext.Current.CancellationToken);
        Assert.False(state.IsSupported);
        Assert.All(state.Settings, s => Assert.False(s.CanEdit));
    }

    /// <summary>Only current compass pre-arm evidence appears as an arming issue.</summary>
    [Fact]
    public async Task HistoricalFailureIsNotCurrent()
    {
        var f = new Fixture();
        f.Diagnostics.GetArming(f.Id).Returns(new VehicleArmingDiagnostic("Not ready", false, false, ["Compass inconsistent"], null, null, null));
        var state = await f.Service.ReadAsync(f.Id, TestContext.Current.CancellationToken);
        Assert.Contains("Compass inconsistent", state.ArmingImpact);
        f.Diagnostics.GetArming(f.Id).Returns(new VehicleArmingDiagnostic("Ready", false, true, [], null, null, null) { LastPreArmReason = "Compass inconsistent" });
        state = await f.Service.ReadAsync(f.Id, TestContext.Current.CancellationToken);
        Assert.Equal("No current compass arming issue", state.ArmingImpact);
        var vehicle = f.Active.State!;
        f.Active.State.Returns(vehicle with { Health = vehicle.Health with { SystemObservedAt = DateTimeOffset.UtcNow.AddMinutes(-1) } });
        Assert.Equal("Unknown", (await f.Service.ReadAsync(f.Id, TestContext.Current.CancellationToken)).Health);
    }

    /// <summary>A reviewed plan cannot cross a connection generation or arm/disarm boundary.</summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ScopeAndSafetyAreRevalidated(bool armed)
    {
        var f = new Fixture();
        var state = await f.Service.ReadAsync(f.Id, TestContext.Current.CancellationToken);
        var changes = await f.Service.EvaluateChangesAsync(f.Id, state.Current with { PrimaryOrientation = 1 }, TestContext.Current.CancellationToken);
        if (armed)
        {
            var vehicle = f.Active.State!;
            f.Active.State.Returns(vehicle with { Flight = vehicle.Flight with { IsArmed = true } });
        }
        else
        {
            using var connection = new CancellationTokenSource();
            f.Active.ConnectionCancellationToken.Returns(connection.Token);
        }
        await Assert.ThrowsAsync<InvalidOperationException>(() => f.Service.ApplyAsync(f.Id, changes, TestContext.Current.CancellationToken));
        Assert.Empty(f.Writes);
    }

    /// <summary>A disabled subsystem cannot be assigned a new compass-dependent yaw source.</summary>
    [Fact]
    public async Task RejectsContradictoryYawSelection()
    {
        var f = new Fixture();
        f.Store("COMPASS_ENABLE", 0);
        var state = await f.Service.ReadAsync(f.Id, TestContext.Current.CancellationToken);
        var changes = await f.Service.EvaluateChangesAsync(f.Id, state.Current with { YawSource = CompassYawSource.Compass }, TestContext.Current.CancellationToken);
        Assert.False(changes.CanApply);
        Assert.Contains(changes.Errors, e => e.Contains("requires a compass"));
    }

    /// <summary>Forced-external sensor mode is preserved during unrelated edits.</summary>
    [Fact]
    public async Task PreservesExternalMode()
    {
        var f = new Fixture();
        f.Store("COMPASS_EXTERNAL", 2);
        var state = await f.Service.ReadAsync(f.Id, TestContext.Current.CancellationToken);
        Assert.Equal(2, state.Current.PrimaryExternal);
        var changes = await f.Service.EvaluateChangesAsync(f.Id, state.Current with { PrimaryOrientation = 1 }, TestContext.Current.CancellationToken);
        Assert.True(changes.CanApply);
        Assert.DoesNotContain(changes.Changes, c => c.Name == "COMPASS_EXTERNAL");
    }

    /// <summary>Enabling and rotating a disabled compass writes and rereads only the reviewed fields.</summary>
    [Fact]
    public async Task EnableAndOrientIntegration()
    {
        var f = new Fixture();
        f.Store("COMPASS_ENABLE", 0);
        var state = await f.Service.ReadAsync(f.Id, TestContext.Current.CancellationToken);
        var desired = state.Current with { Enabled = true, PrimaryOrientation = 1 };
        var changes = await f.Service.EvaluateChangesAsync(f.Id, desired, TestContext.Current.CancellationToken);
        var result = await f.Service.ApplyAsync(f.Id, changes, TestContext.Current.CancellationToken);
        Assert.True(result.Success);
        Assert.Equal(desired, result.Actual!.Current);
        Assert.Equal(new[] { "COMPASS_ENABLE", "COMPASS_ORIENT" }, f.Writes);
        var clean = await f.Service.EvaluateChangesAsync(f.Id, desired, TestContext.Current.CancellationToken);
        Assert.Empty(clean.Changes);
    }

    /// <summary>Reordering is confirmed and absent stored IDs can be cleared without inventing identities.</summary>
    [Fact]
    public async Task PriorityReorderAndMissingCleanupAreReadBack()
    {
        var f = PriorityFixture();
        var expected = PriorityBaseline(f);
        var result = await f.Service.SetPrioritiesAsync(f.Id, expected, [222222, 123456], true, TestContext.Current.CancellationToken);
        Assert.True(result.Success);
        Assert.Equal(222222, f.Registry.GetParameter(f.Id, "COMPASS_PRIO1_ID")!.Value);
        Assert.Equal(123456, f.Registry.GetParameter(f.Id, "COMPASS_PRIO2_ID")!.Value);
        Assert.Equal(0, f.Registry.GetParameter(f.Id, "COMPASS_PRIO3_ID")!.Value);
        Assert.Equal(3, f.Writes.Count);
        Assert.Contains("Reboot", result.Message);
    }

    /// <summary>Stale state, unseen IDs, duplicate IDs, missing confirmation and changed inventory cannot write priorities.</summary>
    [Theory]
    [InlineData("stale")]
    [InlineData("changed")]
    [InlineData("unknown")]
    [InlineData("duplicate")]
    [InlineData("unconfirmed")]
    [InlineData("armed")]
    public async Task PriorityGuardsRejectBeforeWriting(string condition)
    {
        var f = PriorityFixture();
        var expected = PriorityBaseline(f);
        uint[] order = [222222, 123456];
        if (condition == "stale") f.Active.State.Returns(f.Active.State! with { Connection = f.Active.State!.Connection with { LastHeartbeatAt = DateTimeOffset.UtcNow.AddSeconds(-10) } });
        if (condition == "armed") f.Active.State.Returns(f.Active.State! with { Flight = f.Active.State!.Flight with { IsArmed = true } });
        if (condition == "changed") f.Store("COMPASS_DEV_ID2", 444444);
        if (condition == "unknown") order = [444444];
        if (condition == "duplicate") order = [123456, 123456];
        Assert.False((await f.Service.SetPrioritiesAsync(f.Id, expected, order, condition != "unconfirmed", TestContext.Current.CancellationToken)).Success);
        Assert.Empty(f.Writes);
    }

    /// <summary>A failed priority write stops subsequent writes and reports a partial order.</summary>
    [Fact]
    public async Task PriorityPartialFailureDoesNotContinue()
    {
        var f = PriorityFixture();
        f.FailName = "COMPASS_PRIO2_ID";
        var result = await f.Service.SetPrioritiesAsync(f.Id, PriorityBaseline(f), [222222, 123456], true, TestContext.Current.CancellationToken);
        Assert.False(result.Success);
        Assert.Equal(new[] { "COMPASS_PRIO1_ID", "COMPASS_PRIO2_ID" }, f.Writes);
        Assert.Contains("partial", result.Message);
    }

    private static Fixture PriorityFixture()
    {
        var f = new Fixture();
        f.Active.State.Returns(f.Active.State! with { Connection = f.Active.State!.Connection with { LastHeartbeatAt = DateTimeOffset.UtcNow } });
        f.Store("COMPASS_DEV_ID2", 222222);
        f.Store("COMPASS_PRIO1_ID", 123456);
        f.Store("COMPASS_PRIO2_ID", 333333);
        f.Store("COMPASS_PRIO3_ID", 222222);
        return f;
    }

    private static Dictionary<string, float> PriorityBaseline(Fixture f) => f.Registry.GetAllParameters(f.Id)
        .Where(pair => pair.Key.StartsWith("COMPASS_DEV_ID", StringComparison.Ordinal) || pair.Key.StartsWith("COMPASS_PRIO", StringComparison.Ordinal))
        .ToDictionary(pair => pair.Key, pair => pair.Value.Value);

    internal sealed class Fixture
    {
        internal readonly VehicleId Id = new(1, 1);
        internal readonly IActiveVehicleContext Active = Substitute.For<IActiveVehicleContext>();
        internal readonly VehicleParameterRegistry Registry = new();
        internal readonly Dictionary<string, ParameterMetadata> Metadata = new();
        internal readonly List<string> Writes = [];
        internal readonly CompassConfigurationService Service;
        internal readonly IVehicleLiveDiagnostics Diagnostics = Substitute.For<IVehicleLiveDiagnostics>();
        internal string? FailName;
        internal bool Mismatch;
        internal Fixture()
        {
            Active.VehicleId.Returns(Id);
            Active.IsOnline.Returns(true);
            var state = VehicleLiveDiagnosticsTests.State(Id);
            Active.State.Returns(state with { Health = state.Health with { SensorsPresent = 4, SensorsEnabled = 4, SensorsHealthy = 4, SystemObservedAt = DateTimeOffset.UtcNow } });
            foreach (var pair in new[] { ("COMPASS_ENABLE", 1), ("COMPASS_USE", 1), ("COMPASS_ORIENT", 0), ("COMPASS_DEV_ID", 123456), ("EK3_SRC1_YAW", 0) })
            {
                Store(pair.Item1, pair.Item2);
                Metadata[pair.Item1] = new(pair.Item1, null, null, null, null, null, pair.Item1 == "EK3_SRC1_YAW" ? "0:None,1:Compass,2:GPS,3:GPS with compass fallback" : "0:None,1:Yaw 45", null, null, null, pair.Item1 == "COMPASS_ENABLE", false) { DefaultValue = 0 };
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
                return true;
            });
            var factory = Substitute.For<IDomainFactory>();
            factory.Create<IParameterEditSession, ParameterEditScope>(Arg.Any<ParameterEditScope>()).Returns(call =>
                new ParameterEditSession(call.Arg<ParameterEditScope>()!, Active, Registry, parameters, metadata,
                    Options.Create(new ParameterEditSessionOptions { ReadbackTimeout = TimeSpan.FromMilliseconds(30) }), NullLogger<ParameterEditSession>.Instance));
            Diagnostics.GetArming(Id).Returns(new VehicleArmingDiagnostic("Unknown", false, null, [], null, null, null));
            Service = new(Active, Registry, metadata, parameters, NullLogger<CompassConfigurationService>.Instance,
                factory, Diagnostics, new VehicleOperationGate(), TimeProvider.System);
        }
        internal void Store(string name, float value) => Registry.StoreParameter(Id,
            new VehicleParameter(name, value, MavParamType.Int32, 0, 1), CancellationToken.None);
    }
}
