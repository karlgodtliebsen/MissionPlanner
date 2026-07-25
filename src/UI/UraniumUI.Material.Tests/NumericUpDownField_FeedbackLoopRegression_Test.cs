#nullable enable

using UraniumUI.Material.Controls;
using Xunit;

namespace UraniumUI.Material.Tests;

public class NumericUpDownField_FeedbackLoopRegression_Test
{
    public NumericUpDownField_FeedbackLoopRegression_Test()
    {
        ApplicationExtensions.CreateAndSetMockApplication();
    }

    [Fact]
    public void EquivalentText_ShouldNotAssignValueAgain()
    {
        var control = AnimationReadyHandler.Prepare(
            new NumericUpDownField { CultureName = "da-DK", Value = 0.1 });

        var valueChangedCount = 0;
        control.ValueChanged += (_, _) => valueChangedCount++;

        control.Text = "0,10";

        control.Value.ShouldBe(0.1);
        control.Text.ShouldBe("0,10");
        valueChangedCount.ShouldBe(0);
    }

    [Fact]
    public void EquivalentValue_ShouldNotRewriteUserText()
    {
        var control = AnimationReadyHandler.Prepare(
            new NumericUpDownField { CultureName = "da-DK", Value = 0.1 });

        control.Text = "0,10";
        control.Value = 0.1;

        control.Text.ShouldBe("0,10");
    }

    [Fact]
    public void InvalidState_ShouldOnlyChangeWhenValidityChanges()
    {
        var control = AnimationReadyHandler.Prepare(
            new NumericUpDownField());

        var propertyChanges = 0;
        control.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(NumericUpDownField.IsTextValid))
            {
                propertyChanges++;
            }
        };

        control.Text = "invalid";
        control.Text = "still invalid";

        control.IsTextValid.ShouldBeFalse();
        propertyChanges.ShouldBe(1);
    }

    [Fact]
    public void Step_ShouldCanonicalizeOnce()
    {
        var control = AnimationReadyHandler.Prepare(
            new NumericUpDownField
            {
                CultureName = "da-DK",
                Min = 0,
                Max = 10,
                StepSize = 0.1,
                Value = 0.1
            });

        control.Text = "0,10";
        control.IncrementCommand.Execute(null);

        control.Value.ShouldBe(0.2);
        control.Text.ShouldBe("0,2");
    }
}
