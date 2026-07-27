using System.Collections;
using System.Collections.ObjectModel;
using InputKit.Shared;
using UraniumUI.Material.Controls;

namespace UraniumUI.Material.VirtualizedDataGrid.Controls;

/// <summary>
/// Bindable properties for <see cref="VirtualizedDataGrid"/>.
/// </summary>
public partial class VirtualizedDataGrid
{
    /// <summary>
    /// Gets or sets the list of items displayed as rows.
    /// </summary>
    public IList? ItemsSource
    {
        get => (IList?)GetValue(ItemsSourceProperty);
        set => SetValue(ItemsSourceProperty, value);
    }

    /// <summary>
    /// Identifies the <see cref="ItemsSource"/> bindable property.
    /// </summary>
    public static readonly BindableProperty ItemsSourceProperty = BindableProperty.Create(
        nameof(ItemsSource),
        typeof(IList),
        typeof(VirtualizedDataGrid),
        null,
        propertyChanged: static (bindable, oldValue, newValue) =>
            ((VirtualizedDataGrid)bindable).OnItemsSourceSet((IList?)oldValue, (IList?)newValue));

    /// <summary>
    /// Gets or sets the fallback template used to render cell content.
    /// </summary>
    public DataTemplate? CellItemTemplate
    {
        get => (DataTemplate?)GetValue(CellItemTemplateProperty);
        set => SetValue(CellItemTemplateProperty, value);
    }

    /// <summary>
    /// Identifies the <see cref="CellItemTemplate"/> bindable property.
    /// </summary>
    public static readonly BindableProperty CellItemTemplateProperty = BindableProperty.Create(
        nameof(CellItemTemplate),
        typeof(DataTemplate),
        typeof(VirtualizedDataGrid),
        null,
        propertyChanged: static (bindable, _, _) =>
            ((VirtualizedDataGrid)bindable).RefreshRealizedRows());

    /// <summary>
    /// Gets or sets the template used to render column headers.
    /// </summary>
    public DataTemplate? TitleTemplate
    {
        get => (DataTemplate?)GetValue(TitleTemplateProperty);
        set => SetValue(TitleTemplateProperty, value);
    }

    /// <summary>
    /// Identifies the <see cref="TitleTemplate"/> bindable property.
    /// </summary>
    public static readonly BindableProperty TitleTemplateProperty = BindableProperty.Create(
        nameof(TitleTemplate),
        typeof(DataTemplate),
        typeof(VirtualizedDataGrid),
        null,
        propertyChanged: static (bindable, _, _) =>
            ((VirtualizedDataGrid)bindable).RenderHeader());

    /// <summary>
    /// Gets or sets a value indicating whether column headers are displayed.
    /// </summary>
    public bool ShowHeaders
    {
        get => (bool)GetValue(ShowHeadersProperty);
        set => SetValue(ShowHeadersProperty, value);
    }

    /// <summary>
    /// Identifies the <see cref="ShowHeaders"/> bindable property.
    /// </summary>
    public static readonly BindableProperty ShowHeadersProperty = BindableProperty.Create(
        nameof(ShowHeaders),
        typeof(bool),
        typeof(VirtualizedDataGrid),
        true,
        propertyChanged: static (bindable, _, _) =>
            ((VirtualizedDataGrid)bindable).RenderHeader());

    /// <summary>
    /// Gets or sets the color of row separator lines.
    /// </summary>
    public Color LineSeparatorColor
    {
        get => (Color)GetValue(LineSeparatorColorProperty);
        set => SetValue(LineSeparatorColorProperty, value);
    }

    /// <summary>
    /// Identifies the <see cref="LineSeparatorColor"/> bindable property.
    /// </summary>
    public static readonly BindableProperty LineSeparatorColorProperty = BindableProperty.Create(
        nameof(LineSeparatorColor),
        typeof(Color),
        typeof(VirtualizedDataGrid),
        Colors.Gray);

    /// <summary>
    /// Gets or sets a value indicating whether separators are displayed between rows.
    /// </summary>
    public bool ShowRowSeparators
    {
        get => (bool)GetValue(ShowRowSeparatorsProperty);
        set => SetValue(ShowRowSeparatorsProperty, value);
    }

    /// <summary>
    /// Identifies the <see cref="ShowRowSeparators"/> bindable property.
    /// </summary>
    public static readonly BindableProperty ShowRowSeparatorsProperty = BindableProperty.Create(
        nameof(ShowRowSeparators),
        typeof(bool),
        typeof(VirtualizedDataGrid),
        true,
        propertyChanged: static (bindable, _, _) =>
            ((VirtualizedDataGrid)bindable).RefreshRealizedRows());

    /// <summary>
    /// Gets or sets a value indicating whether columns are generated from the item type.
    /// </summary>
    public bool UseAutoColumns
    {
        get => (bool)GetValue(UseAutoColumnsProperty);
        set => SetValue(UseAutoColumnsProperty, value);
    }

    /// <summary>
    /// Identifies the <see cref="UseAutoColumns"/> bindable property.
    /// </summary>
    public static readonly BindableProperty UseAutoColumnsProperty = BindableProperty.Create(nameof(UseAutoColumns), typeof(bool), typeof(VirtualizedDataGrid), false,
        propertyChanged: static (bindable, _, _) => ((VirtualizedDataGrid)bindable).SetAutoColumns());

    /// <summary>
    /// Gets or sets the two-way collection of selected row items.
    /// </summary>
    public IList SelectedItems
    {
        get => (IList)GetValue(SelectedItemsProperty);
        set => SetValue(SelectedItemsProperty, value);
    }

    /// <summary>
    /// Identifies the <see cref="SelectedItems"/> bindable property.
    /// </summary>
    // Use defaultValueCreator so selections are never shared between DataGrid instances.
    public static readonly BindableProperty SelectedItemsProperty = BindableProperty.Create(
        nameof(SelectedItems),
        typeof(IList),
        typeof(VirtualizedDataGrid),
        defaultValueCreator: static _ => new ObservableCollection<object>(),
        defaultBindingMode: BindingMode.TwoWay,
        propertyChanged: static (bindable, oldValue, newValue) =>
            ((VirtualizedDataGrid)bindable).OnSelectedItemsSet((IList?)oldValue, (IList?)newValue));

    /// <summary>
    /// Gets or sets the accent color used to highlight selected rows.
    /// </summary>
    public Color SelectionColor
    {
        get => (Color)GetValue(SelectionColorProperty);
        set => SetValue(SelectionColorProperty, value);
    }

    /// <summary>
    /// Identifies the <see cref="SelectionColor"/> bindable property.
    /// </summary>
    public static readonly BindableProperty SelectionColorProperty = BindableProperty.Create(
        nameof(SelectionColor),
        typeof(Color),
        typeof(VirtualizedDataGrid),
        InputKitOptions.GetAccentColor(),
        propertyChanged: static (bindable, _, _) =>
            ((VirtualizedDataGrid)bindable).RefreshSelectionVisuals());

    /// <summary>
    /// Gets or sets the view displayed when the item source is empty.
    /// </summary>
    public View? EmptyView
    {
        get => (View?)GetValue(EmptyViewProperty);
        set => SetValue(EmptyViewProperty, value);
    }

    /// <summary>
    /// Identifies the <see cref="EmptyView"/> bindable property.
    /// </summary>
    public static readonly BindableProperty EmptyViewProperty = BindableProperty.Create(
        nameof(EmptyView),
        typeof(View),
        typeof(VirtualizedDataGrid),
        null,
        propertyChanged: static (bindable, _, _) =>
            ((VirtualizedDataGrid)bindable).ApplyEmptyView());

    /// <summary>
    /// Gets or sets the template displayed when the item source is empty.
    /// </summary>
    public DataTemplate? EmptyViewTemplate
    {
        get => (DataTemplate?)GetValue(EmptyViewTemplateProperty);
        set => SetValue(EmptyViewTemplateProperty, value);
    }

    /// <summary>
    /// Identifies the <see cref="EmptyViewTemplate"/> bindable property.
    /// </summary>
    public static readonly BindableProperty EmptyViewTemplateProperty = BindableProperty.Create(
        nameof(EmptyViewTemplate),
        typeof(DataTemplate),
        typeof(VirtualizedDataGrid),
        null,
        propertyChanged: static (bindable, _, _) =>
            ((VirtualizedDataGrid)bindable).ApplyEmptyView());

    /// <summary>
    /// Gets or sets the columns displayed by the grid.
    /// </summary>
    public IList<DataGridColumn> Columns
    {
        get => (IList<DataGridColumn>)GetValue(ColumnsProperty);
        set => SetValue(ColumnsProperty, value);
    }

    /// <summary>
    /// Identifies the <see cref="Columns"/> bindable property.
    /// </summary>
    public static readonly BindableProperty ColumnsProperty = BindableProperty.Create(
        nameof(Columns),
        typeof(IList<DataGridColumn>),
        typeof(VirtualizedDataGrid),
        defaultValueCreator: static _ => new ObservableCollection<DataGridColumn>(),
        propertyChanged: static (bindable, oldValue, newValue) =>
            ((VirtualizedDataGrid)bindable).OnColumnsSet(
                (IList<DataGridColumn>?)oldValue,
                (IList<DataGridColumn>?)newValue));

    /// <summary>
    /// Gets or sets the padding applied to each data cell.
    /// </summary>
    public Thickness CellPadding
    {
        get => (Thickness)GetValue(CellPaddingProperty);
        set => SetValue(CellPaddingProperty, value);
    }

    /// <summary>
    /// Identifies the <see cref="CellPadding"/> bindable property.
    /// </summary>
    public static readonly BindableProperty CellPaddingProperty = BindableProperty.Create(
        nameof(CellPadding),
        typeof(Thickness),
        typeof(VirtualizedDataGrid),
        new Thickness(20, 10),
        propertyChanged: static (bindable, _, _) =>
            ((VirtualizedDataGrid)bindable).RefreshRealizedRows());

    /// <summary>
    /// Gets or sets the padding applied to each header cell.
    /// </summary>
    public Thickness HeaderPadding
    {
        get => (Thickness)GetValue(HeaderPaddingProperty);
        set => SetValue(HeaderPaddingProperty, value);
    }

    /// <summary>
    /// Identifies the <see cref="HeaderPadding"/> bindable property.
    /// </summary>
    public static readonly BindableProperty HeaderPaddingProperty = BindableProperty.Create(
        nameof(HeaderPadding),
        typeof(Thickness),
        typeof(VirtualizedDataGrid),
        new Thickness(20, 10),
        propertyChanged: static (bindable, _, _) =>
            ((VirtualizedDataGrid)bindable).RenderHeader());

    /// <summary>
    /// Gets or sets the fixed row height, or a non-positive value to use automatic height.
    /// </summary>
    public double RowHeight
    {
        get => (double)GetValue(RowHeightProperty);
        set => SetValue(RowHeightProperty, value);
    }

    /// <summary>
    /// Optional fixed row height. A value less than or equal to zero enables automatic height.
    /// Uniform rows provide the best virtualization performance.
    /// </summary>
    public static readonly BindableProperty RowHeightProperty = BindableProperty.Create(
        nameof(RowHeight),
        typeof(double),
        typeof(VirtualizedDataGrid),
        -1d,
        propertyChanged: static (bindable, _, _) =>
            ((VirtualizedDataGrid)bindable).RefreshRealizedRows());

    /// <summary>
    /// Gets or sets the spacing between columns.
    /// </summary>
    public double ColumnSpacing
    {
        get => (double)GetValue(ColumnSpacingProperty);
        set => SetValue(ColumnSpacingProperty, value);
    }

    /// <summary>
    /// Identifies the <see cref="ColumnSpacing"/> bindable property.
    /// </summary>
    public static readonly BindableProperty ColumnSpacingProperty = BindableProperty.Create(
        nameof(ColumnSpacing),
        typeof(double),
        typeof(VirtualizedDataGrid),
        0d,
        propertyChanged: static (bindable, _, _) =>
            ((VirtualizedDataGrid)bindable).RecalculateColumnLayout());

    /// <summary>
    /// Gets or sets the stable width assigned to columns whose width is automatic.
    /// </summary>
    public double AutoColumnWidth
    {
        get => (double)GetValue(AutoColumnWidthProperty);
        set => SetValue(AutoColumnWidthProperty, value);
    }

    /// <summary>
    /// Width used for DataGrid columns declared as Auto. Cross-row Auto measurement would
    /// defeat virtualization, so Auto is resolved to this stable width.
    /// </summary>
    public static readonly BindableProperty AutoColumnWidthProperty = BindableProperty.Create(
        nameof(AutoColumnWidth),
        typeof(double),
        typeof(VirtualizedDataGrid),
        160d,
        propertyChanged: static (bindable, _, _) =>
            ((VirtualizedDataGrid)bindable).RecalculateColumnLayout());

    /// <summary>
    /// Gets or sets the minimum width used when resolving star-sized columns.
    /// </summary>
    public double MinimumStarColumnWidth
    {
        get => (double)GetValue(MinimumStarColumnWidthProperty);
        set => SetValue(MinimumStarColumnWidthProperty, value);
    }

    /// <summary>
    /// Identifies the <see cref="MinimumStarColumnWidth"/> bindable property.
    /// </summary>
    public static readonly BindableProperty MinimumStarColumnWidthProperty = BindableProperty.Create(
        nameof(MinimumStarColumnWidth),
        typeof(double),
        typeof(VirtualizedDataGrid),
        100d,
        propertyChanged: static (bindable, _, _) =>
            ((VirtualizedDataGrid)bindable).RecalculateColumnLayout());

    /// <summary>
    /// Gets or sets a value indicating whether the final visible column fills unused viewport width.
    /// </summary>
    public bool FillAvailableWidth
    {
        get => (bool)GetValue(FillAvailableWidthProperty);
        set => SetValue(FillAvailableWidthProperty, value);
    }

    /// <summary>
    /// Identifies the <see cref="FillAvailableWidth"/> bindable property.
    /// </summary>
    public static readonly BindableProperty FillAvailableWidthProperty = BindableProperty.Create(
        nameof(FillAvailableWidth),
        typeof(bool),
        typeof(VirtualizedDataGrid),
        true,
        propertyChanged: static (bindable, _, _) =>
            ((VirtualizedDataGrid)bindable).RecalculateColumnLayout());

    /// <summary>
    /// Gets or sets the item sizing strategy used by the virtualizing rows host.
    /// </summary>
    public ItemSizingStrategy ItemSizingStrategy
    {
        get => (ItemSizingStrategy)GetValue(ItemSizingStrategyProperty);
        set => SetValue(ItemSizingStrategyProperty, value);
    }

    /// <summary>
    /// Identifies the <see cref="ItemSizingStrategy"/> bindable property.
    /// </summary>
    public static readonly BindableProperty ItemSizingStrategyProperty = BindableProperty.Create(
        nameof(ItemSizingStrategy),
        typeof(ItemSizingStrategy),
        typeof(VirtualizedDataGrid),
        ItemSizingStrategy.MeasureFirstItem,
        propertyChanged: static (bindable, _, newValue) =>
            ((VirtualizedDataGrid)bindable).SetItemSizingStrategy((ItemSizingStrategy)newValue));

    /// <summary>
    /// Gets or sets the visibility behavior of the horizontal scroll bar.
    /// </summary>
    public ScrollBarVisibility HorizontalScrollBarVisibility
    {
        get => (ScrollBarVisibility)GetValue(HorizontalScrollBarVisibilityProperty);
        set => SetValue(HorizontalScrollBarVisibilityProperty, value);
    }

    /// <summary>
    /// Identifies the <see cref="HorizontalScrollBarVisibility"/> bindable property.
    /// </summary>
    public static readonly BindableProperty HorizontalScrollBarVisibilityProperty = BindableProperty.Create(
        nameof(HorizontalScrollBarVisibility),
        typeof(ScrollBarVisibility),
        typeof(VirtualizedDataGrid),
        ScrollBarVisibility.Default,
        propertyChanged: static (bindable, _, newValue) =>
            ((VirtualizedDataGrid)bindable).SetHorizontalScrollBarVisibility((ScrollBarVisibility)newValue));

    /// <summary>
    /// Gets or sets the visibility behavior of the vertical scroll bar.
    /// </summary>
    public ScrollBarVisibility VerticalScrollBarVisibility
    {
        get => (ScrollBarVisibility)GetValue(VerticalScrollBarVisibilityProperty);
        set => SetValue(VerticalScrollBarVisibilityProperty, value);
    }

    /// <summary>
    /// Identifies the <see cref="VerticalScrollBarVisibility"/> bindable property.
    /// </summary>
    public static readonly BindableProperty VerticalScrollBarVisibilityProperty = BindableProperty.Create(
        nameof(VerticalScrollBarVisibility),
        typeof(ScrollBarVisibility),
        typeof(VirtualizedDataGrid),
        ScrollBarVisibility.Default,
        propertyChanged: static (bindable, _, newValue) =>
            ((VirtualizedDataGrid)bindable).SetVerticalScrollBarVisibility((ScrollBarVisibility)newValue));
}
