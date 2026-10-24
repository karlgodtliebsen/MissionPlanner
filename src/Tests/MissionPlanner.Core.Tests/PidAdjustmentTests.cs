using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using MissionPlanner.Core.ConfigTuning;
using MissionPlanner.Core.ConfigTuning.Tuning;
using MissionPlanner.Core.Vehicles.Abstractions;
using MissionPlanner.Core.Vehicles.Models;
using MissionPlanner.MavLink.Parameters;
using MissionPlanner.Shared.Models.Vehicles.Models;
using NSubstitute;

namespace MissionPlanner.Core.Tests;

/// <summary>Ensures reviewed P/I scaling cannot partially stage or silently overwrite another editor.</summary>
public sealed class PidAdjustmentTests
{
    [Theory]
    [InlineData("ATC_RAT_", "PSC_D_ACC_")]
    [InlineData("ATC_RAT_", "PSC_ACCZ_")]
    [InlineData("RATE_", "THR_ACCEL_")]
    public async Task PreviewPreservesRatiosAndStagesOnlyAfterExplicitAction(string rate, string vertical)
    {
        using var f = new Fixture(rate, vertical);
        var preview = await f.Assistant.PreviewAsync(true, true, 110, TestContext.Current.CancellationToken);
        Assert.Equal(6, preview.Changes.Count); Assert.False(f.Session.IsDirty);
        Assert.All(preview.Changes, change => Assert.Equal(change.ExpectedLive * 1.1, change.Value, 6));
        f.Assistant.Stage(preview);
        Assert.Equal(6, f.Session.Fields.Count(field => field.IsModified));
        Assert.DoesNotContain(preview.Changes, change => change.Name.EndsWith("_D", StringComparison.Ordinal));
        Assert.Empty(f.Writer.ReceivedCalls());
        Assert.Throws<InvalidOperationException>(() => f.Assistant.Stage(preview));
    }
    [Fact]
    public async Task DefaultScaleMakesNoChangeAndPendingConflictsAreAllOrNothing()
    {
        using var f = new Fixture("ATC_RAT_", "PSC_ACCZ_");
        var unchanged = await f.Assistant.PreviewAsync(true, false, 100, TestContext.Current.CancellationToken);
        f.Assistant.Stage(unchanged); Assert.False(f.Session.IsDirty);
        var preview = await f.Assistant.PreviewAsync(true, false, 105, TestContext.Current.CancellationToken);
        var conflict = preview.Changes[^1];
        Assert.True(f.Session.TrySetPending(conflict.Name, .9, out _));
        Assert.Throws<InvalidOperationException>(() => f.Assistant.Stage(preview));
        Assert.Equal(.9, f.Session.GetField(conflict.Name)!.PendingValue);
        Assert.Single(f.Session.Fields, field => field.IsModified);
        await Assert.ThrowsAsync<InvalidOperationException>(() => f.Assistant.PreviewAsync(true, false, 105, TestContext.Current.CancellationToken));
    }
    [Fact]
    public async Task BatchValidationRejectsInvalidMemberWithoutChangingEarlierMembers()
    {
        using var f = new Fixture("ATC_RAT_", "PSC_ACCZ_");
        var preview = await f.Assistant.PreviewAsync(true, false, 105, TestContext.Current.CancellationToken);
        var invalid = preview.Changes.ToArray(); invalid[^1] = invalid[^1] with { Value = double.NaN };
        Assert.False(f.Session.TryStageReviewed(invalid, out var error)); Assert.Contains("finite", error);
        Assert.False(f.Session.IsDirty);
        f.Active.IsOnline.Returns(false);
        Assert.Throws<InvalidOperationException>(() => f.Assistant.Stage(preview));
        Assert.Empty(f.Writer.ReceivedCalls());
    }
    private sealed class Fixture : IDisposable
    {
        internal readonly IActiveVehicleContext Active = Substitute.For<IActiveVehicleContext>();
        internal readonly IVehicleParameterService Writer = Substitute.For<IVehicleParameterService>();
        internal readonly ParameterEditSession Session;
        internal readonly PidAdjustmentAssistant Assistant;
        internal Fixture(string rate, string vertical)
        {
            var now = DateTimeOffset.UtcNow;
            var state = new VehicleState(new(1, 1), 0, 2, 3, 0, 4, 3, VehicleConnectionState.Online, now, VehicleMode.Unknown, false, null, null, null, null, null, null, null, null);
            Active.IsOnline.Returns(true); Active.VehicleId.Returns(new VehicleId(1, 1)); Active.State.Returns(state);
            var values = new Dictionary<string, VehicleParameter>();
            foreach (var (name, value) in new[] { (rate + "RLL_P", .1f), (rate + "RLL_I", .2f), (rate + "PIT_P", .15f), (rate + "PIT_I", .3f), (vertical + "P", .5f), (vertical + "I", 1f) }) values[name] = new(name, value, MavParamType.Real32, 0, 6);
            var parameters = Substitute.For<IVehicleParameterRegistry>(); parameters.GetAllParameters(new(1, 1)).Returns(values);
            var metadata = Substitute.For<IVehicleParameterMetadataService>();
            metadata.GetAllMetadataAsync(new VehicleId(1, 1), Arg.Any<CancellationToken>()).Returns(Task.FromResult<IReadOnlyDictionary<string, ParameterMetadata>>(new Dictionary<string, ParameterMetadata>()));
            Session = new(new(new(1, 1), state.Identity.Firmware), Active, parameters, Writer, metadata, Options.Create(new ParameterEditSessionOptions()), NullLogger<ParameterEditSession>.Instance);
            var sessions = Substitute.For<IParameterEditSessionFactory>(); sessions.Create(new VehicleId(1, 1)).Returns(Session);
            Assistant = new(Active, sessions, parameters);
        }
        public void Dispose() => Session.Dispose();
    }
}
