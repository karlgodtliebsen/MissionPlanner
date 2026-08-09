using System.Collections;
using System.Collections.ObjectModel;
using System.Windows.Input;
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
    /// Gets or sets the selected row when <see cref="SelectionMode"/> is
    /// <see cref="Microsoft.Maui.Controls.SelectionMode.Single"/>.
    /// </summary>
    public object? SelectedItem
    {
        get => GetValue(SelectedItemProperty);
        set => SetValue(SelectedItemProperty, value);
    }

    /// <summary>Identifies the <see cref="SelectedItem"/> bindable property.</summary>
    public static readonly BindableProperty SelectedItemProperty = BindableProperty.Create(
        nameof(SelectedItem),
        typeof(object),
        typeof(VirtualizedDataGrid),
        null,
        BindingMode.TwoWay,
        propertyChanged: static (bindable, oldValue, newValue) =>
            ((VirtualizedDataGrid)bindable).OnSelectedItemSet(oldValue, newValue));

    /// <summary>
    /// Gets or sets whether selection is disabled, limited to one row, or allows
    /// multiple rows. The default is <see cref="Microsoft.Maui.Controls.SelectionMode.Multiple"/>
    /// for compatibility with <see cref="SelectedItems"/>.
    /// </summary>
    public SelectionMode SelectionMode
    {
        get => (SelectionMode)GetValue(SelectionModeProperty);
        set => SetValue(SelectionModeProperty, value);
    }

    /// <summary>Identifies the <see cref="SelectionMode"/> bindable property.</summary>
    public static readonly BindableProperty SelectionModeProperty = BindableProperty.Create(
        nameof(SelectionMode),
        typeof(SelectionMode),
        typeof(VirtualizedDataGrid),
        SelectionMode.Multiple,
        propertyChanged: static (bindable, _, _) =>
            ((VirtualizedDataGrid)bindable).OnSelectionModeChanged());

    /// <summary>
    /// Gets or sets the command invoked after the effective selection changes.
    /// The command parameter is a <see cref="VirtualizedDataGridSelectionChangedEventArgs"/>.
    /// </summary>
    public ICommand? SelectionChangedCommand
    {
        get => (ICommand?)GetValue(SelectionChangedCommandProperty);
        set => SetValue(SelectionChangedCommandProperty, value);
    }

    /// <summary>Identifies the <see cref="SelectionChangedCommand"/> bindable property.</summary>
    public static readonly BindableProperty SelectionChangedCommandProperty = BindableProperty.Create(
        nameof(SelectionChangedCommand),
        typeof(ICommand),
        typeof(VirtualizedDataGrid));

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
        new Thickness(0),
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
        new Thickness(0),
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
    /// Gets or sets the estimated height used for rows that have not yet been
    /// realized and measured.
    /// </summary>
    public double EstimatedRowHeight
    {
        get => (double)GetValue(EstimatedRowHeightProperty);
        set => SetValue(EstimatedRowHeightProperty, value);
    }

    /// <summary>Identifies the <see cref="EstimatedRowHeight"/> property.</summary>
    public static readonly BindableProperty EstimatedRowHeightProperty = BindableProperty.Create(
        nameof(EstimatedRowHeight),
        typeof(double),
        typeof(VirtualizedDataGrid),
        80d,
        validateValue: static (_, value) => (double)value > 0,
        propertyChanged: static (bindable, _, _) =>
            ((VirtualizedDataGrid)bindable).RefreshRealizedRows());

    /// <summary>
    /// Gets or sets the number of rows retained above and below the visible
    /// viewport to make fast scrolling seamless.
    /// </summary>
    public int OverscanRowCount
    {
        get => (int)GetValue(OverscanRowCountProperty);
        set => SetValue(OverscanRowCountProperty, value);
    }

    /// <summary>Identifies the <see cref="OverscanRowCount"/> property.</summary>
    public static readonly BindableProperty OverscanRowCountProperty = BindableProperty.Create(
        nameof(OverscanRowCount),
        typeof(int),
        typeof(VirtualizedDataGrid),
        2,
        validateValue: static (_, value) => (int)value >= 0,
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
    /// Gets or sets the fallback width used by automatic columns before content is realized.
    /// </summary>
    public double AutoColumnWidth
    {
        get => (double)GetValue(AutoColumnWidthProperty);
        set => SetValue(AutoColumnWidthProperty, value);
    }

    /// <summary>
    /// Initial width used for DataGrid columns declared as Auto. For generated label
    /// cells, the width is refined from realized content. Custom templates retain this
    /// stable width because Fill-sized template content cannot be measured intrinsically
    /// without creating a width feedback loop.
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
        false,
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
