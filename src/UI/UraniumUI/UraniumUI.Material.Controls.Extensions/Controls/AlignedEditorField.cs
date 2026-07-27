#nullable enable

namespace UraniumUI.Material.Controls;

/// <summary>
/// UraniumUI <see cref="EditorField"/> with bindable horizontal and vertical
/// text alignment.
/// </summary>
public class AlignedEditorField : EditorField
{
    /// <summary>
    /// Initializes a new instance of the <see cref="AlignedEditorField"/> class.
    /// </summary>
    public AlignedEditorField()
    {
        EditorView.SetBinding(
            Editor.HorizontalTextAlignmentProperty,
            new Binding(nameof(HorizontalTextAlignment), source: this));

        EditorView.SetBinding(
            Editor.VerticalTextAlignmentProperty,
            new Binding(nameof(VerticalTextAlignment), source: this));
    }

    /// <summary>
    /// Gets or sets the horizontal text alignment of the editor.
    /// </summary>
    public TextAlignment HorizontalTextAlignment
    {
        get => (TextAlignment)GetValue(HorizontalTextAlignmentProperty);
        set => SetValue(HorizontalTextAlignmentProperty, value);
    }

    /// <summary>
    /// Identifies the <see cref="HorizontalTextAlignment"/> bindable property.
    /// </summary>
    public static readonly BindableProperty HorizontalTextAlignmentProperty =
        BindableProperty.Create(
            nameof(HorizontalTextAlignment),
            typeof(TextAlignment),
            typeof(AlignedEditorField),
            Editor.HorizontalTextAlignmentProperty.DefaultValue);

    /// <summary>
    /// Gets or sets the vertical text alignment of the editor.
    /// </summary>
    public TextAlignment VerticalTextAlignment
    {
        get => (TextAlignment)GetValue(VerticalTextAlignmentProperty);
        set => SetValue(VerticalTextAlignmentProperty, value);
    }

    /// <summary>
    /// Identifies the <see cref="VerticalTextAlignment"/> bindable property.
    /// </summary>
    public static readonly BindableProperty VerticalTextAlignmentProperty =
        BindableProperty.Create(
            nameof(VerticalTextAlignment),
            typeof(TextAlignment),
            typeof(AlignedEditorField),
            Editor.VerticalTextAlignmentProperty.DefaultValue);
}
