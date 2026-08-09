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
}
