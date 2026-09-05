#nullable enable

using Shouldly;
using UraniumUI.Material.Controls;
using UraniumUI.Material.Tests.UraniumUI.Core.Tests;
using UraniumUI.Tests.Core;
using Xunit;

namespace UraniumUI.Material.Tests;

public class NumericUpDownField_Test
{
    public NumericUpDownField_Test()
    {
        ApplicationExtensions.CreateAndSetMockApplication();
    }

    [Fact]
    public void Increment_ShouldUseStepSize()
    {
        var control = AnimationReadyHandler.Prepare(
            new NumericUpDownField { Min = 0, Max = 10, StepSize = 0.5, Value = 2 });

        control.IncrementCommand.Execute(null);

        control.Value.ShouldBe(2.5);
    }

    [Fact]
    public void Increment_ShouldClampAtMaximum()
    {
        var control = AnimationReadyHandler.Prepare(
            new NumericUpDownField { Min = 0, Max = 10, StepSize = 3, Value = 9 });

        control.IncrementCommand.Execute(null);

        control.Value.ShouldBe(10);
        control.IncrementCommand.CanExecute(null).ShouldBeFalse();
    }

    [Fact]
    public void Decrement_ShouldWrapWhenEnabled()
    {
        var control = AnimationReadyHandler.Prepare(
            new NumericUpDownField
            {
                Min = 0,
                Max = 10,
                StepSize = 1,
                Value = 0,
                IsWrapEnabled = true
            });

        control.DecrementCommand.Execute(null);

        control.Value.ShouldBe(10);
    }

    [Fact]
    public void DanishText_ShouldUpdateValue()
    {
        var control = AnimationReadyHandler.Prepare(
            new NumericUpDownField { CultureName = "da-DK", Min = 0, Max = 10 });

        control.Text = "2,5";

        control.Value.ShouldBe(2.5);
        control.IsTextValid.ShouldBeTrue();
    }

    [Fact]
    public void Alignment_ShouldBeForwardedToEntry()
    {
        var control = AnimationReadyHandler.Prepare(
            new NumericUpDownField { HorizontalTextAlignment = TextAlignment.End, VerticalTextAlignment = TextAlignment.Center });

        control.EntryView.HorizontalTextAlignment.ShouldBe(TextAlignment.End);
        control.EntryView.VerticalTextAlignment.ShouldBe(TextAlignment.Center);
    }

    [Fact]
    public void Stepper_ShouldBeInsideInputFieldAttachments()
    {
        var control = AnimationReadyHandler.Prepare(
            new NumericUpDownField());

        control.Attachments.Count.ShouldBeGreaterThan(0);
    }

    [Theory]
    [InlineData("Int8", -128, 127)]
    [InlineData("Int16", -32768, 32767)]
    [InlineData("Int32", -2147483648d, 2147483647d)]
    [InlineData("UInt8", 0, 255)]
    [InlineData("UInt16", 0, 65535)]
    [InlineData("UInt32", 0, 4294967295d)]
    public void IntegerNumericType_ShouldApplyNativeRangeAndStepRules(string numericType, double minimum, double maximum)
    {
        var control = AnimationReadyHandler.Prepare(new NumericUpDownField
        {
            CultureName = "en-US",
            NumericType = numericType,
            Value = maximum
        });

        control.Value.ShouldBe(maximum);
        control.IncrementCommand.CanExecute(null).ShouldBeFalse();
        control.AllowThousands.ShouldBeFalse();
        control.AllowSign.ShouldBe(!numericType.StartsWith("UInt", StringComparison.Ordinal));

        control.Value = minimum;
        control.DecrementCommand.CanExecute(null).ShouldBeFalse();
        control.Text = "1.5";
        control.IsTextValid.ShouldBeFalse();
    }

    [Fact]
    public void IntegerNumericType_ShouldNormalizeExternalFraction()
    {
        var control = AnimationReadyHandler.Prepare(new NumericUpDownField { NumericType = "UInt8", Value = 12.6 });

        control.Value.ShouldBe(13);
        control.Text.ShouldBe("13");
    }
}
