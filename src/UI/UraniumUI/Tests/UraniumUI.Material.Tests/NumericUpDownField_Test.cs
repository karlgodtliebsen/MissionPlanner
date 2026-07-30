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
}
