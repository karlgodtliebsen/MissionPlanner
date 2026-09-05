#nullable enable

using Shouldly;
using UraniumUI.Material.Controls;
using UraniumUI.Material.Tests.UraniumUI.Core.Tests;
using UraniumUI.Tests.Core;
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

    [Fact]
    public void AutoSize_ShouldBeForwardedToEditor()
    {
        var control = AnimationReadyHandler.Prepare(
            new AlignedEditorField { AutoSize = EditorAutoSizeOption.Disabled });

        control.EditorView.AutoSize.ShouldBe(EditorAutoSizeOption.Disabled);

        control.AutoSize = EditorAutoSizeOption.TextChanges;

        control.EditorView.AutoSize.ShouldBe(EditorAutoSizeOption.TextChanges);
    }
}
