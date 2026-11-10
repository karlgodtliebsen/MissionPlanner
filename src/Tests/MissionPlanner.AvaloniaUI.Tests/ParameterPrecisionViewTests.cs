using System.Globalization;
using MissionPlanner.App.Models;
using MissionPlanner.App.Presentation.Documents;
using MissionPlanner.App.Views.ConfigTuning.Sections.ParametersEditor;
using MissionPlanner.Core.ConfigTuning;
using MissionPlanner.Core.ConfigTuning.Comparison;
using MissionPlanner.MavLink.Parameters;
using NSubstitute;

namespace MissionPlanner.AvaloniaUI.Tests;

public sealed class ParameterPrecisionViewTests
{
    [Fact]
    public void ComparisonRetainsCalibrationPrecisionRegardlessOfSuggestedStep()
    {
        var metadata = ParameterFieldMetadata.Empty with { Increment = .01 };
        var row = new ParameterComparisonRow("AHRS_TRIM_X", "Trim", "Live", -.0157665f, "Import", .03558625f,
            (double).03558625f - -.0157665f, ParameterComparisonStatus.Different, "rad", metadata, true, null);
        var model = new ParameterComparisonItemViewModel(row);
        Assert.Equal("-0.0157665", Invariant(model.LeftValueText!));
        Assert.Equal("0.03558625", Invariant(model.RightValueText!));
        Assert.Equal("0.05135275", Invariant(model.DifferenceText!));
        Assert.True(model.CanWrite);
    }

    [Fact]
    public void MotorSpinPresetListAllowsACustomNumericValue()
    {
        var metadata = ParameterFieldMetadata.Empty with
        {
            Minimum = 0, Maximum = .2f, Increment = .01f,
            Options = [new(0, "Low"), new(.1f, "Default"), new(.2f, "High")]
        };
        var session = Substitute.For<IParameterEditSession>();
        var field = new ParameterEditField("MOT_SPIN_ARM", MavParamType.Real32, .11f, .11f, .16f, metadata, null);
        var model = new ParameterItemViewModel(session, field);
        Assert.Equal("Custom (0.16)", model.SelectedValue);
        Assert.Equal("0.16", Invariant(model.ValueText));
        Assert.Equal((double).16f, model.Value);
        Assert.Null(model.ValidationError);
        Assert.Empty(session.ReceivedCalls());
        model.SetField(field with { PendingValue = .160001f });
        Assert.Equal("Custom (0.160001)", model.SelectedValue);
        Assert.Empty(session.ReceivedCalls());
    }

    [Theory]
    [InlineData(.3f, "ACRO_RP_EXPO = 0.3")]
    [InlineData(.135f, "ACRO_RP_EXPO = 0.135")]
    [InlineData(.0036f, "ACRO_RP_EXPO = 0.0036")]
    public void CopyableDocumentsPreserveWireValuesWithoutDoubleExpansion(float value, string expected)
    {
        var assignment = UserDocumentBuilder.ParameterAssignment("ACRO_RP_EXPO", value);
        Assert.Equal(expected, assignment);
        Assert.Equal(value, float.Parse(assignment.Split('=')[1], CultureInfo.InvariantCulture));
    }

    private static string Invariant(string value) => value.Replace(CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator, ".");
}
