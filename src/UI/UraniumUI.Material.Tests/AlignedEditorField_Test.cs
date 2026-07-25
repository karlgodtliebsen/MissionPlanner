#nullable enable

using UraniumUI.Material.Controls;
using Xunit;

namespace UraniumUI.Material.Tests;

public class AlignedEditorField_Test
{
    public AlignedEditorField_Test()
    {
        ApplicationExtensions.CreateAndSetMockApplication();
    }

    [Fact]
    public void Alignment_ShouldBeForwardedToEditor()
    {
        var control = AnimationReadyHandler.Prepare(
            new AlignedEditorField { HorizontalTextAlignment = TextAlignment.Center, VerticalTextAlignment = TextAlignment.End });

        control.EditorView.HorizontalTextAlignment.ShouldBe(TextAlignment.Center);
        control.EditorView.VerticalTextAlignment.ShouldBe(TextAlignment.End);
    }
}
