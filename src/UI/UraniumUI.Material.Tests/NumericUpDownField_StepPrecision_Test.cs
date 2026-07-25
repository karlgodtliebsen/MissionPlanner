#nullable enable

using UraniumUI.Material.Controls;
using Xunit;

namespace UraniumUI.Material.Tests;

public class NumericUpDownField_StepPrecision_Test
{
    public NumericUpDownField_StepPrecision_Test()
    {
        ApplicationExtensions.CreateAndSetMockApplication();
    }

    [Fact]
    public void Float32Noise_ShouldDisplayUsingStepPrecision()
    {
        var control = AnimationReadyHandler.Prepare(
            new NumericUpDownField { CultureName = "da-DK", StepSize = 0.1, Value = 1.300000071526 });

        control.Text.ShouldBe("1,3");
    }

    /// <summary>
    /// Verifies that a late-bound step size reformats an already-bound value.
    /// </summary>
    [Fact]
    public void StepSizeAppliedAfterValue_ShouldReformatUsingNewPrecision()
    {
        var control = AnimationReadyHandler.Prepare(
            new NumericUpDownField { CultureName = "da-DK" });

        // Bindable properties are not guaranteed to arrive in source declaration order.
        control.Value = 1.1000000149;
        control.StepSize = 0.1;

        control.Text.ShouldBe("1,1");
    }

    [Fact]
    public void Increment_ShouldSnapNoisyCurrentValueBeforeAddingStep()
    {
        var control = AnimationReadyHandler.Prepare(
            new NumericUpDownField
            {
                CultureName = "da-DK",
                Min = 0,
                Max = 3,
                StepSize = 0.1,
                Value = 1.300000071526
            });

        control.IncrementCommand.Execute(null);

        control.Value.ShouldBe(1.4);
        control.Text.ShouldBe("1,4");
    }

    [Fact]
    public void RepeatedTenths_ShouldNotAccumulateVisibleError()
    {
        var control = AnimationReadyHandler.Prepare(
            new NumericUpDownField
            {
                CultureName = "da-DK",
                Min = 0,
                Max = 3,
                StepSize = 0.1,
                Value = 0.1
            });

        control.IncrementCommand.Execute(null);
        control.IncrementCommand.Execute(null);
        control.IncrementCommand.Execute(null);
        control.IncrementCommand.Execute(null);

        control.Value.ShouldBe(0.5);
        control.Text.ShouldBe("0,5");
    }

    [Fact]
    public void Float32BindingRoundTrip_ShouldRemainVisuallyStable()
    {
        var control = AnimationReadyHandler.Prepare(
            new NumericUpDownField
            {
                CultureName = "da-DK",
                Min = 0,
                Max = 3,
                StepSize = 0.1,
                Value = 1.3
            });

        // Simulate storing the value in a MAVLink float32-backed ViewModel and
        // receiving it through the two-way binding again.
        control.Value = (double)(float)control.Value;

        control.Text.ShouldBe("1,3");

        control.IncrementCommand.Execute(null);

        control.Text.ShouldBe("1,4");
    }

    [Fact]
    public void ExplicitDecimalPlaces_ShouldOverrideStepPrecision()
    {
        var control = AnimationReadyHandler.Prepare(
            new NumericUpDownField { CultureName = "da-DK", StepSize = 0.1, DecimalPlaces = 3, Value = 1.3 });

        control.Text.ShouldBe("1,300");
    }

    [Fact]
    public void StepPrecision_CanBeDisabled()
    {
        var control = AnimationReadyHandler.Prepare(
            new NumericUpDownField
            {
                CultureName = "da-DK",
                StepSize = 0.1,
                UseStepSizePrecision = false,
                NumberFormat = "G15",
                Value = 1.300000071526
            });

        control.Text.ShouldContain("1,300000071526");
    }
}
