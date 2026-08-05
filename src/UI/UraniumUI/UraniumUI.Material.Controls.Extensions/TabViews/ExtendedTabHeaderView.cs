using System.Diagnostics;
using UraniumUI.Resources;

namespace UraniumUI.Material.TabViews;

/// <summary>
/// Provides reusable rich-header chrome while derived views supply only the inner header content.
/// </summary>
[ContentProperty(nameof(HeaderContent))]
public class ExtendedTabHeaderView : ContentView
{
    private readonly Border border;
    private readonly BoxView selectionIndicator;
    private readonly ContentView contentHost;

    /// <summary>Identifies the selected-header state.</summary>
    public static readonly BindableProperty IsHeaderSelectedProperty = BindableProperty.Create(
        nameof(IsHeaderSelected), typeof(bool), typeof(ExtendedTabHeaderView), false,
        propertyChanged: OnIsHeaderSelectedChanged);

    /// <summary>Identifies the content supplied by a derived header view.</summary>
    public static readonly BindableProperty HeaderContentProperty = BindableProperty.Create(
        nameof(HeaderContent), typeof(View), typeof(ExtendedTabHeaderView),
        propertyChanged: OnHeaderContentChanged);

    /// <summary>Identifies the selection-indicator color.</summary>
    public static readonly BindableProperty SelectionColorProperty = BindableProperty.Create(
        nameof(SelectionColor), typeof(Color), typeof(ExtendedTabHeaderView), null,
        propertyChanged: OnSelectionAppearanceChanged);

    /// <summary>Identifies the selected border thickness.</summary>
    public static readonly BindableProperty SelectedStrokeThicknessProperty = BindableProperty.Create(
        nameof(SelectedStrokeThickness), typeof(double), typeof(ExtendedTabHeaderView), 1d,
        propertyChanged: OnSelectionAppearanceChanged);

    /// <summary>Identifies the comma-separated style classes applied to the internal border.</summary>
    public static readonly BindableProperty BorderStyleClassProperty = BindableProperty.Create(
        nameof(BorderStyleClass), typeof(string), typeof(ExtendedTabHeaderView),
        "", propertyChanged: OnBorderStyleClassChanged);

    /// <summary>Gets whether this header represents the selected tab.</summary>
    public bool IsHeaderSelected
    {
        get => (bool)GetValue(IsHeaderSelectedProperty);
        internal set => SetValue(IsHeaderSelectedProperty, value);
    }

    /// <summary>Gets or sets the content rendered to the right of the selection marker.</summary>
    public View? HeaderContent
    {
        get => (View?)GetValue(HeaderContentProperty);
        set => SetValue(HeaderContentProperty, value);
    }

    /// <summary>Gets or sets the selected marker and border color.</summary>
    public Color? SelectionColor
    {
        get => (Color?)GetValue(SelectionColorProperty);
        set => SetValue(SelectionColorProperty, value);
    }

    /// <summary>Gets or sets the border thickness used while selected.</summary>
    public double SelectedStrokeThickness
    {
        get => (double)GetValue(SelectedStrokeThicknessProperty);
        set => SetValue(SelectedStrokeThicknessProperty, value);
    }

    /// <summary>Gets or sets the comma-separated style classes applied to the internal border.</summary>
    public string BorderStyleClass
    {
        get => (string)GetValue(BorderStyleClassProperty);
        set => SetValue(BorderStyleClassProperty, value);
    }

    /// <summary>Initializes the reusable header chrome.</summary>
    public ExtendedTabHeaderView()
    {
        selectionIndicator = new BoxView { WidthRequest = 4, IsVisible = false };
        contentHost = new ContentView();
        var layout = new Grid { ColumnDefinitions = { new ColumnDefinition(new GridLength(4)), new ColumnDefinition(GridLength.Star) } };
        layout.Add(selectionIndicator, 0);
        layout.Add(contentHost, 1);
        border = new Border { Content = layout, StrokeThickness = 1 /*StyleClass = ["SurfaceContainer", "Rounded", "Elevation1"]*/ };
        ApplyBorderStyleClass();
        Content = border;
        UpdateSelectionVisuals();
    }

    private static void OnHeaderContentChanged(BindableObject bindable, object? oldValue, object? newValue)
    {
        ((ExtendedTabHeaderView)bindable).contentHost.Content = newValue as View;
    }

    private static void OnIsHeaderSelectedChanged(BindableObject bindable, object oldValue, object newValue)
    {
        var header = (ExtendedTabHeaderView)bindable;
        header.UpdateSelectionVisuals();
        WriteSelectionDiagnostic(header, (bool)newValue);
    }

    private static void OnSelectionAppearanceChanged(BindableObject bindable, object oldValue, object newValue)
    {
        ((ExtendedTabHeaderView)bindable).UpdateSelectionVisuals();
    }

    private static void OnBorderStyleClassChanged(BindableObject bindable, object oldValue, object newValue)
    {
        ((ExtendedTabHeaderView)bindable).ApplyBorderStyleClass();
    }

    private void ApplyBorderStyleClass()
    {
        border.StyleClass = string.IsNullOrWhiteSpace(BorderStyleClass)
            ? []
            : BorderStyleClass.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }

    private void UpdateSelectionVisuals()
    {
        var primaryLight = SelectionColor ?? ColorResource.GetColor("Primary", Colors.Green);
        var primaryDark = SelectionColor ?? ColorResource.GetColor("PrimaryDark", Colors.LightGreen);
        var surfaceLight = ColorResource.GetColor("Surface", Colors.White);
        var surfaceDark = ColorResource.GetColor("SurfaceDark", Colors.Black);
        var outlineLight = ColorResource.GetColor("OutlineVariant", Colors.Gray);
        var outlineDark = ColorResource.GetColor("OutlineVariantDark", Colors.DarkGray);

        selectionIndicator.SetAppThemeColor(
            BoxView.ColorProperty,
            primaryLight.WithAlpha(.2f),
            primaryDark.WithAlpha(.2f));
        selectionIndicator.IsVisible = IsHeaderSelected;
        border.SetAppThemeColor(
            BackgroundColorProperty,
            IsHeaderSelected ? primaryLight.WithAlpha(.2f) : surfaceLight,
            IsHeaderSelected ? primaryDark.WithAlpha(.2f) : surfaceDark);
        border.SetAppTheme(
            Border.StrokeProperty,
            new SolidColorBrush(IsHeaderSelected ? primaryLight : outlineLight),
            new SolidColorBrush(IsHeaderSelected ? primaryDark : outlineDark));
        border.StrokeThickness = IsHeaderSelected ? SelectedStrokeThickness : 1;
        contentHost.Opacity = IsHeaderSelected ? 1 : .5;
    }

    [Conditional("DEBUG")]
    private static void WriteSelectionDiagnostic(ExtendedTabHeaderView header, bool selected)
    {
        var context = header.BindingContext;
        var title = context?.GetType().GetProperty("Title")?.GetValue(context)?.ToString();
        Debug.WriteLine(
            $"ExtendedTabView header selection: header={header.GetType().Name}, title={title ?? "<none>"}, " +
            $"data={context?.GetType().Name ?? "<null>"}, selected={selected}");
    }
}
