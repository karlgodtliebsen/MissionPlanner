#nullable enable

using Shouldly;
using UraniumUI.Material.Controls;
using UraniumUI.Material.Tests.UraniumUI.Core.Tests;
using UraniumUI.Tests.Core;
using Xunit;

namespace UraniumUI.Material.Tests;

public class NumericField_Test
{
    public NumericField_Test() => ApplicationExtensions.CreateAndSetMockApplication();

    [Fact]
    public void DanishDecimalText_ShouldUpdateValueWithoutReformattingText()
    {
        var control = AnimationReadyHandler.Prepare(new NumericField
        {
            CultureName = "da-DK",
            NumberFormat = "F7"
        });

        control.Text = "2,5";

        control.Value.ShouldBe(2.5);
        control.Text.ShouldBe("2,5");
        control.IsTextValid.ShouldBeTrue();
    }

    [Fact]
    public void NonNumericCharacter_ShouldBeRejected()
    {
        var control = AnimationReadyHandler.Prepare(new NumericField { CultureName = "en-US" });
        control.Text = "12.5";

        control.Text = "12.5x";

        control.Text.ShouldBe("12.5");
        control.Value.ShouldBe(12.5);
    }

    [Fact]
    public void IncompleteDecimalText_ShouldRemainEditable()
    {
        var control = AnimationReadyHandler.Prepare(new NumericField { CultureName = "en-US", Value = 12 });

        control.Text = "12.";

        control.Text.ShouldBe("12.");
        control.IsTextValid.ShouldBeTrue();
    }

    [Fact]
    public void LocaleThousandsSeparator_ShouldBeAccepted()
    {
        var control = AnimationReadyHandler.Prepare(new NumericField { CultureName = "en-US" });

        control.Text = "1,234.5";

        control.Value.ShouldBe(1234.5);
        control.Text.ShouldBe("1,234.5");
    }

    [Fact]
    public void ExternalValue_ShouldUseConfiguredFormat()
    {
        var control = AnimationReadyHandler.Prepare(new NumericField
        {
            CultureName = "en-US",
            NumberFormat = "F7",
            Value = 1.25
        });

        control.Text.ShouldBe("1.2500000");
    }

    [Theory]
    [InlineData("Int8", -128, 127)]
    [InlineData("Int16", -32768, 32767)]
    [InlineData("Int32", -2147483648d, 2147483647d)]
    [InlineData("UInt8", 0, 255)]
    [InlineData("UInt16", 0, 65535)]
    [InlineData("UInt32", 0, 4294967295d)]
    public void IntegerNumericType_ShouldApplyNativeRangeAndInputRules(string numericType, double minimum, double maximum)
    {
        var control = AnimationReadyHandler.Prepare(new NumericField { CultureName = "en-US", NumericType = numericType });

        control.Value = double.MinValue;
        control.Value.ShouldBe(minimum);
        control.Value = double.MaxValue;
        control.Value.ShouldBe(maximum);
        control.AllowThousands.ShouldBeFalse();
        control.AllowSign.ShouldBe(!numericType.StartsWith("UInt", StringComparison.Ordinal));

        control.Text = "1.5";
        control.Text.ShouldBe(maximum.ToString("G15", System.Globalization.CultureInfo.GetCultureInfo("en-US")));
    }

    [Fact]
    public void DoubleNumericType_ShouldRetainFloatingPointInputRules()
    {
        var control = AnimationReadyHandler.Prepare(new NumericField { CultureName = "en-US", NumericType = "Double" });

        control.Text = "-1,234.5";

        control.Value.ShouldBe(-1234.5);
        control.AllowSign.ShouldBeTrue();
        control.AllowThousands.ShouldBeTrue();
    }
}
