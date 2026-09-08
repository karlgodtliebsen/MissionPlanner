#nullable enable

using Shouldly;
using UraniumUI.Material.Controls;
using UraniumUI.Material.Tests.UraniumUI.Core.Tests;
using UraniumUI.Tests.Core;
using Xunit;

namespace UraniumUI.Material.Tests;

public class NumericLabel_Test
{
    public NumericLabel_Test() => ApplicationExtensions.CreateAndSetMockApplication();

    [Fact]
    public void Value_ShouldUseConfiguredCultureAndFormat()
    {
        var control = new NumericLabel
        {
            CultureName = "da-DK",
            NumberFormat = "F2",
            Value = 12.5
        };

        control.ShouldBeAssignableTo<Label>();
        control.Text.ShouldBe("12,50");
    }

    [Theory]
    [InlineData("Int8", -128, 127)]
    [InlineData("UInt8", 0, 255)]
    [InlineData("Int16", -32768, 32767)]
    [InlineData("UInt16", 0, 65535)]
    [InlineData("Int32", -2147483648d, 2147483647d)]
    [InlineData("UInt32", 0, 4294967295d)]
    public void IntegerNumericType_ShouldApplyNativeRange(string numericType, double minimum, double maximum)
    {
        var control = new NumericLabel
        {
            CultureName = "en-US",
            NumericType = numericType,
            NumberFormat = "F2"
        };

        control.Value = double.MinValue;
        control.Value.ShouldBe(minimum);
        control.Value = double.MaxValue;
        control.Value.ShouldBe(maximum);
        control.Value = 12.6;
        control.Value.ShouldBe(13);
        control.Text.ShouldBe("13");
    }

    [Fact]
    public void GeneralFormat_ShouldHideFloatingPointNoiseForDouble()
    {
        var control = new NumericLabel
        {
            CultureName = "en-US",
            NumericType = "Double",
            NumberFormat = "G7",
            Value = 0.100000001490116
        };

        control.Text.ShouldBe("0.1");
    }

    [Fact]
    public void ValueChanged_ShouldReportCoercedValue()
    {
        var control = new NumericLabel { NumericType = "UInt8" };
        NumericValueChangedEventArgs? received = null;
        control.ValueChanged += (_, args) => received = args;

        control.Value = 300;

        received.ShouldNotBeNull();
        received.OldValue.ShouldBe(0);
        received.NewValue.ShouldBe(255);
    }
}
