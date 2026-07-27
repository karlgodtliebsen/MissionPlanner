namespace UraniumUI.Material.VirtualizedDataGrid.Controls;

/// <summary>
/// View factories used by <see cref="VirtualizedDataGrid"/>.
/// </summary>
public partial class VirtualizedDataGrid
{
    /// <summary>
    /// Gets or sets the factory used to create a label for a bound cell.
    /// </summary>
    public Func<BindingBase, Label> LabelFactory { get; set; } = null!;

    /// <summary>
    /// Gets or sets the factory used to create horizontal row separators.
    /// </summary>
    public Func<View> HorizontalLineFactory { get; set; } = null!;

    private void InitializeFactoryMethods()
    {
        LabelFactory = CreateLabel;
        HorizontalLineFactory = CreateHorizontalLine;
    }

    /// <summary>
    /// Creates the default label used to display a bound cell value.
    /// </summary>
    /// <param name="binding">The binding that supplies the label text.</param>
    /// <returns>The configured cell label.</returns>
    protected virtual Label CreateLabel(BindingBase binding)
    {
        // Match UraniumUI.Material.Controls.DataGrid exactly. Margin participates in
        // Auto measurement without constraining the label's text first; applying the
        // same space as parent padding can make values such as "Personal" wrap.
        var label = new Label
        {
            Margin = 20,
            HorizontalOptions = LayoutOptions.Center,
            VerticalOptions = LayoutOptions.Center
        };

        label.SetBinding(Label.TextProperty, binding);
        return label;
    }

    /// <summary>
    /// Creates the default horizontal row separator.
    /// </summary>
    /// <returns>The configured separator view.</returns>
    protected virtual View CreateHorizontalLine()
    {
        var line = new BoxView
        {
            HorizontalOptions = LayoutOptions.Fill,
            HeightRequest = 2,
            CornerRadius = 1,
            Opacity = 0.4
        };

        line.SetBinding(
            BoxView.ColorProperty,
            new Binding(nameof(LineSeparatorColor), source: this));

        return line;
    }

    /// <summary>
    /// Creates a row separator by using the configured factory.
    /// </summary>
    /// <returns>The row separator view.</returns>
    internal View CreateRowSeparator()
    {
        return HorizontalLineFactory?.Invoke() ?? CreateHorizontalLine();
    }
}
