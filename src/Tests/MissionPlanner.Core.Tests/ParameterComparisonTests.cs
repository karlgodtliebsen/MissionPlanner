using FluentAssertions;
using MissionPlanner.App.Views.ConfigTuning;
using MissionPlanner.Core.ConfigTuning;
using MissionPlanner.Core.ConfigTuning.Comparison;
using NSubstitute;
using System.Globalization;

namespace MissionPlanner.Core.Tests;

/// <summary>Verifies reusable parameter-source comparison and safe staging.</summary>
public sealed class ParameterComparisonTests
{
    private static readonly ParameterFieldMetadata Writable = new(
        "Gain", null, null, null, null, 0.1, false, false, [], []);

    /// <summary>Float wire noise below metadata precision compares equal.</summary>
    [Fact]
    public void StepPrecisionTreatsFloatExpansionAsEqual()
    {
        var comparer = new ParameterValueEquivalence();

        comparer.AreEquivalent(0.3, 0.30000001192092896, Writable).Should().BeTrue();
        comparer.AreEquivalent(0.3, 0.4, Writable).Should().BeFalse();
        comparer.AreEquivalent(double.NaN, double.NaN).Should().BeTrue();
        comparer.AreEquivalent(double.PositiveInfinity, double.NegativeInfinity).Should().BeFalse();
    }

    /// <summary>Comparison values use metadata precision instead of exposing floating-point expansion.</summary>
    [Fact]
    public void ComparisonPresentationRoundsDifferenceToMetadataPrecision()
    {
        var row = new ParameterComparisonRow(
            "ACRO_BAL_PITCH",
            "Acro Balance Pitch",
            "Live",
            1d,
            "Pending",
            1.1d,
            1.1d - 1d,
            ParameterComparisonStatus.Different,
            null,
            Writable,
            true,
            null);

        var item = new ParameterComparisonItemViewModel(row);
        var separator = CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator;

        item.LeftValueText.Should().Be("1");
        item.RightValueText.Should().Be($"1{separator}1");
        item.DifferenceText.Should().Be($"0{separator}1");
    }

    /// <summary>Missing, invalid, read-only and differing values remain explicitly classified.</summary>
    [Fact]
    public void ComparisonClassifiesUnionOfBothSources()
    {
        var service = new ParameterComparisonService(new ParameterValueEquivalence());
        var readOnly = Writable with { ReadOnly = true };
        var left = Values(("DIFF", "1"), ("LEFT", "2"), ("BAD", "3"), ("LOCKED", "4"));
        var right = Values(("DIFF", "1.5"), ("RIGHT", "8"), ("BAD", "nope"), ("LOCKED", "5"));
        var metadata = new Dictionary<string, ParameterFieldMetadata>(StringComparer.Ordinal)
        {
            ["DIFF"] = Writable,
            ["BAD"] = Writable,
            ["LOCKED"] = readOnly
        };

        var result = service.Compare(Source("Live"), left, Source("File"), right, metadata);

        result.Rows.Single(row => row.Name == "DIFF").Status.Should().Be(ParameterComparisonStatus.Different);
        result.Rows.Single(row => row.Name == "LEFT").Status.Should().Be(ParameterComparisonStatus.OnlyOnLeft);
        result.Rows.Single(row => row.Name == "RIGHT").Status.Should().Be(ParameterComparisonStatus.OnlyOnRight);
        result.Rows.Single(row => row.Name == "BAD").Status.Should().Be(ParameterComparisonStatus.InvalidRightValue);
        result.Rows.Single(row => row.Name == "LOCKED").Status.Should().Be(ParameterComparisonStatus.ReadOnly);
    }

    /// <summary>Staging changes pending state only and excludes unsafe rows.</summary>
    [Fact]
    public void StageUsesEditSessionAndNeverApplies()
    {
        var service = new ParameterComparisonService(new ParameterValueEquivalence());
        var comparison = service.Compare(
            Source("Live"), Values(("GAIN", "1")),
            Source("Profile"), Values(("GAIN", "2")),
            new Dictionary<string, ParameterFieldMetadata> { ["GAIN"] = Writable });
        var session = Substitute.For<IParameterEditSession>();
        session.TrySetPending("GAIN", 2, out Arg.Any<string?>()).Returns(call =>
        {
            call[2] = null;
            return true;
        });

        service.Stage(comparison, session, ["GAIN"]).Should().Equal("GAIN");

        session.Received(1).TrySetPending("GAIN", 2, out Arg.Any<string?>());
        session.DidNotReceiveWithAnyArgs().ApplyAsync(
            default(IReadOnlyList<string>),
            TestContext.Current.CancellationToken);
    }

    private static ParameterComparisonSource Source(string name) =>
        new(name, name, DateTimeOffset.UnixEpoch, null);

    private static IReadOnlyDictionary<string, ParameterComparisonInput> Values(params (string Name, string Value)[] values) =>
        values.ToDictionary(value => value.Name, value => new ParameterComparisonInput(value.Name, value.Value), StringComparer.Ordinal);
}
