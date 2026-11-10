using System.Globalization;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using MissionPlanner.Core.ConfigTuning;
using MissionPlanner.Core.ConfigTuning.Comparison;
using MissionPlanner.Core.Vehicles;
using MissionPlanner.Core.Vehicles.Abstractions;
using MissionPlanner.MavLink.Parameters;
using MissionPlanner.Shared.Models.Vehicles.Models;
using NSubstitute;

namespace MissionPlanner.Core.Tests;

public sealed class ParameterPrecisionTests
{
    [Fact]
    public async Task ImportedCalibrationAndSpinValuesCanBeReviewedWrittenAndReadBackWithoutRounding()
    {
        using var fixture = new Fixture();
        await fixture.Session.LoadAsync(cancellationToken: TestContext.Current.CancellationToken);
        var imported = new Dictionary<string, double>
        {
            ["AHRS_TRIM_X"] = .03558625,
            ["AHRS_TRIM_Y"] = .03484156,
            ["MOT_SPIN_ARM"] = .16,
            ["MOT_SPIN_MIN"] = .17
        };
        var source = new ParameterComparisonSource("Import", "Test", DateTimeOffset.UtcNow, null);
        var comparer = new ParameterComparisonService(ParameterValueEquivalence.Default);
        var result = comparer.Compare(source,
            fixture.Session.Fields.ToDictionary(field => field.Name, field => new ParameterComparisonInput(field.Name, ParameterValueText.Format(field.LiveValue))),
            source, imported.ToDictionary(pair => pair.Key, pair => new ParameterComparisonInput(pair.Key, pair.Value.ToString("R", CultureInfo.InvariantCulture))),
            fixture.Session.Fields.ToDictionary(field => field.Name, field => field.Metadata));
        Assert.Equal(4, comparer.Stage(result, fixture.Session, imported.Keys.ToArray()).Count);
        Assert.Empty(fixture.Writes);
        var plan = fixture.Session.CreateWritePlan();
        Assert.Equal(4, plan.Entries.Count);
        Assert.Empty(plan.Skipped);
        Assert.All(plan.Entries, entry => Assert.Equal(imported[entry.Name], entry.PendingValue));
        var report = await fixture.Session.ApplyAsync(plan, cancellationToken: TestContext.Current.CancellationToken);
        Assert.True(report.Success);
        Assert.Equal(4, fixture.Writes.Count);
        Assert.All(fixture.Writes, write => Assert.Equal((float)imported[write.Name], write.Value));
        Assert.False(fixture.Session.IsDirty);
        foreach (var (name, value) in imported)
        {
            Assert.True(fixture.Session.TrySetPending(name, value, out var error), error);
            Assert.False(fixture.Session.GetField(name)!.IsModified);
        }
    }

    [Theory]
    [InlineData("LOWER_BOUND", .1)]
    [InlineData("UPPER_BOUND", .3)]
    [InlineData("MOT_SPIN_ARM", .1)]
    public async Task DecimalEditsAtFloatMetadataBoundariesAreValid(string name, double value)
    {
        using var fixture = new Fixture();
        await fixture.Session.LoadAsync(cancellationToken: TestContext.Current.CancellationToken);
        Assert.True(fixture.Session.TrySetPending(name, value, out var error), error);
    }

    [Theory]
    [InlineData("AHRS_TRIM_X", .18, "at most")]
    [InlineData("LOWER_BOUND", .09999, "at least 0.1")]
    [InlineData("UPPER_BOUND", .30001, "at most 0.3")]
    [InlineData("MOT_SPIN_ARM", .21, "at most")]
    [InlineData("DEVICE_ID", 42, "read-only")]
    [InlineData("RSSI_ANA_PIN", 0, "Select one")]
    [InlineData("COUNT", 2.5, "whole-number")]
    [InlineData("COUNT", 128, "8-bit")]
    [InlineData("FLAGS", 4, "Unknown bitmask")]
    [InlineData("AHRS_TRIM_X", double.NaN, "finite")]
    [InlineData("AHRS_TRIM_X", double.PositiveInfinity, "finite")]
    public async Task InvalidValuesStillCannotBeWritten(string name, double value, string message)
    {
        using var fixture = new Fixture();
        await fixture.Session.LoadAsync(cancellationToken: TestContext.Current.CancellationToken);
        Assert.False(fixture.Session.TrySetPending(name, value, out var error));
        Assert.Contains(message, error);
        var plan = fixture.Session.CreateWritePlan([name]);
        Assert.Empty(plan.Entries);
        Assert.Single(plan.Skipped);
        Assert.Empty(fixture.Writes);
    }

    [Theory]
    [InlineData(.03558625, .03484156)]
    [InlineData(.03558625, .03558725)]
    [InlineData(0, .0000001)]
    public void ChangesSmallerThanTheEditorStepAreStillDifferences(double left, double right)
    {
        var metadata = ParameterFieldMetadata.Empty with { Minimum = -.1745, Maximum = .1745, Increment = .01 };
        Assert.False(ParameterValueEquivalence.Default.AreEquivalent(left, right, metadata));
        Assert.True(new ParameterEditField("TRIM", MavParamType.Real32, left, left, right, metadata, null).IsModified);
    }

    [Fact]
    public async Task ReadbackOfADifferentValueInsideTheSameEditorStepIsNotConfirmed()
    {
        using var fixture = new Fixture();
        await fixture.Session.LoadAsync(cancellationToken: TestContext.Current.CancellationToken);
        Assert.True(fixture.Session.TrySetPending("AHRS_TRIM_X", .03558625, out _));
        fixture.Readback = .03558725f;
        var report = await fixture.Session.ApplyAsync(["AHRS_TRIM_X"], TestContext.Current.CancellationToken);
        Assert.False(report.Success);
        Assert.Equal(ParameterWriteOutcome.ReadbackFailed, Assert.Single(report.Results).Outcome);
        Assert.True(fixture.Session.IsDirty);
    }

    [Theory]
    [InlineData(.3f, "0.3")]
    [InlineData(.1f, "0.1")]
    [InlineData(.135f, "0.135")]
    [InlineData(.0036f, "0.0036")]
    [InlineData(.03558625f, "0.03558625")]
    public void ReceivedFloatsHaveConciseLosslessText(float value, string expected)
    {
        var text = ParameterValueText.Format(value);
        Assert.Equal(expected, text);
        Assert.Equal(value, float.Parse(text, CultureInfo.InvariantCulture));
    }

    [Fact]
    public void ExplicitPrecisionIntegersAndSmallDifferencesArePreserved()
    {
        Assert.Equal("16777217", ParameterValueText.Format(16777217));
        Assert.Equal("0.035586251234", ParameterValueText.Format(.035586251234));
        Assert.Equal("0.05", ParameterValueText.FormatDifference(.11f, .16f));
        Assert.NotEqual("0", ParameterValueText.FormatDifference(0, 1e-35));
    }

    private sealed class Fixture : IDisposable
    {
        private readonly VehicleId id = new(1, 1);
        private readonly VehicleParameterRegistry registry = new();
        public ParameterEditSession Session { get; }
        public List<(string Name, float Value)> Writes { get; } = [];
        public float? Readback { get; set; }

        public Fixture()
        {
            var active = Substitute.For<IActiveVehicleContext>();
            active.IsOnline.Returns(true);
            active.VehicleId.Returns(id);
            var definitions = new Dictionary<string, ParameterMetadata>();
            void Add(string name, float live, string? range = null, string? values = null, string? increment = null,
                bool readOnly = false, MavParamType type = MavParamType.Real32, string? bits = null)
            {
                registry.StoreParameter(id, new(name, live, type, 0, 10), CancellationToken.None);
                definitions[name] = new(name, name, null, null, null, range, values, bits, increment, null, false, readOnly);
            }
            Add("AHRS_TRIM_X", -.0157665f, "-0.1745 +0.1745", increment: "0.01");
            Add("AHRS_TRIM_Y", .0035094f, "-0.1745 +0.1745", increment: "0.01");
            Add("MOT_SPIN_ARM", .11f, "0.0 0.2", "0.0:Low,0.1:Default,0.2:High", "0.01");
            Add("MOT_SPIN_MIN", .12f, "0.0 0.25", "0.0:Low,0.15:Default,0.25:High", "0.01");
            Add("LOWER_BOUND", .2f, "0.1 1", increment: "0.1");
            Add("UPPER_BOUND", .2f, "0 0.3", increment: "0.1");
            Add("DEVICE_ID", 123, readOnly: true, type: MavParamType.Int32);
            Add("RSSI_ANA_PIN", 15, values: "-1:Disabled,15:Pin 15", type: MavParamType.Int8);
            Add("COUNT", 1, type: MavParamType.Int8);
            Add("FLAGS", 0, type: MavParamType.Int32, bits: "0:One,1:Two");
            var metadata = Substitute.For<IVehicleParameterMetadataService>();
            metadata.GetAllMetadataAsync(id, Arg.Any<CancellationToken>()).Returns(definitions);
            var protocol = Substitute.For<IVehicleParameterService>();
            protocol.SetParameterAsync(id, Arg.Any<string>(), Arg.Any<float>(), Arg.Any<MavParamType>(), Arg.Any<CancellationToken>()).Returns(call =>
            {
                var name = call.Arg<string>();
                var value = call.Arg<float>();
                Writes.Add((name, value));
                registry.StoreParameter(id, registry.GetParameter(id, name)! with { Value = Readback ?? value }, CancellationToken.None);
                return true;
            });
            Session = new(new(id, null!), active, registry, protocol, metadata,
                Options.Create(new ParameterEditSessionOptions { ReadbackTimeout = TimeSpan.FromMilliseconds(60) }), NullLogger<ParameterEditSession>.Instance);
        }
        public void Dispose() => Session.Dispose();
    }
}
