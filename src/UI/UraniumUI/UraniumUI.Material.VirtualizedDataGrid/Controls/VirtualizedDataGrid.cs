using System.Collections;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Reflection;
using UraniumUI.Material.Controls;

namespace UraniumUI.Material.VirtualizedDataGrid.Controls;

/// <summary>
/// A DataGrid-compatible control that virtualizes rows through the platform CollectionView.
///
/// The header is kept outside the CollectionView, while a horizontal-only ScrollView moves
/// the header and realized rows together. Vertical scrolling is owned exclusively by the
/// CollectionView so platform virtualization remains active.
/// </summary>
public partial class VirtualizedDataGrid : Border
{
    private readonly Grid rootLayout;
    private readonly ContentView searchHost;
    private readonly ScrollView horizontalScrollView;
    private readonly Grid tableLayout;
    private readonly Grid headerSection;
    private readonly Grid headerGrid;
    private readonly Grid rowsHost;
    private readonly CollectionView rowsView;
    private readonly ContentView emptyViewHost;
    private readonly ContentView pagerHost;

    private readonly List<WeakReference<VirtualizedDataGridRowPresenter>> presenters = [];
    private readonly HashSet<object> selectedItemSet = [];
    private readonly HashSet<DataGridColumn> subscribedColumns = [];
    private readonly HashSet<IDataGridSelectionColumn> subscribedSelectionColumns = [];

    private INotifyCollectionChanged? observedItemsSource;
    private INotifyCollectionChanged? observedColumns;
    private INotifyCollectionChanged? observedSelectedItems;

    private IReadOnlyList<double> resolvedColumnWidths = Array.Empty<double>();
    private bool subscriptionsActive;
    private bool visualResourcesReleased;
    private bool settingAutoColumns;
    private bool syncingSelectionCells;
    private int deferRefreshCount;
    private bool refreshPending;
    private IList? deferredSnapshot;
    private double lastViewportWidth = -1;
    private IReadOnlyList<double> measuredAutoColumnWidths = Array.Empty<double>();
    private bool autoColumnMeasurementScheduled;

    /// <summary>
    /// Initializes a new instance of the <see cref="VirtualizedDataGrid"/> class.
    /// </summary>
    public VirtualizedDataGrid()
    {
        InitializeFactoryMethods();
        InitializeDataView();
        Padding = new Thickness(0, 10);

        headerGrid = new Grid { HorizontalOptions = LayoutOptions.Fill };

        headerSection = new Grid { HorizontalOptions = LayoutOptions.Fill, RowDefinitions = { new RowDefinition(GridLength.Auto), new RowDefinition(GridLength.Auto) } };

        headerSection.Add(headerGrid, 0, 0);

        var headerSeparator = CreateRowSeparator();
        Grid.SetRow(headerSeparator, 1);
        headerSection.Children.Add(headerSeparator);

        rowsView = new CollectionView
        {
            HorizontalOptions = LayoutOptions.Fill,
            VerticalOptions = LayoutOptions.Start,
            SelectionMode = SelectionMode.None,
            ItemSizingStrategy = ItemSizingStrategy.MeasureFirstItem,
            ItemsUpdatingScrollMode = ItemsUpdatingScrollMode.KeepScrollOffset,
            ItemsLayout = new LinearItemsLayout(ItemsLayoutOrientation.Vertical) { ItemSpacing = 0 }
        };

        AttachRowsViewLifecycle();

        emptyViewHost = new ContentView { HorizontalOptions = LayoutOptions.Fill, VerticalOptions = LayoutOptions.Fill, IsVisible = false };

        rowsHost = new Grid { HorizontalOptions = LayoutOptions.Fill, VerticalOptions = LayoutOptions.Start };
        rowsHost.Children.Add(rowsView);
        rowsHost.Children.Add(emptyViewHost);

        tableLayout = new Grid { HorizontalOptions = LayoutOptions.Start, VerticalOptions = LayoutOptions.Start, RowDefinitions = { new RowDefinition(GridLength.Auto), new RowDefinition(GridLength.Auto) } };

        tableLayout.Add(headerSection, 0, 0);
        tableLayout.Add(rowsHost, 0, 1);

        horizontalScrollView = new ScrollView { Orientation = ScrollOrientation.Horizontal, HorizontalOptions = LayoutOptions.Fill, VerticalOptions = LayoutOptions.Start, Content = tableLayout };

        searchHost = new ContentView { HorizontalOptions = LayoutOptions.Fill, VerticalOptions = LayoutOptions.Center, IsVisible = false };

        pagerHost = new ContentView { HorizontalOptions = LayoutOptions.Fill, VerticalOptions = LayoutOptions.Center, IsVisible = false };

        rootLayout = new Grid { HorizontalOptions = LayoutOptions.Fill, VerticalOptions = LayoutOptions.Start, RowDefinitions = { new RowDefinition(GridLength.Auto), new RowDefinition(GridLength.Auto), new RowDefinition(GridLength.Auto) } };
        rootLayout.Add(searchHost, 0, 0);
        rootLayout.Add(horizontalScrollView, 0, 1);
        rootLayout.Add(pagerHost, 0, 2);

        Content = rootLayout;

        ActivateSubscriptions();
        RestoreVisualResources();
        RebuildAll();
    }

    /// <summary>
    /// Gets the element type inferred from the current <see cref="ItemsSource"/>.
    /// </summary>
    public Type? CurrentType { get; protected set; }

    /// <summary>
    /// Gets a value indicating whether the grid has at least one visible column and can render rows.
    /// </summary>
    public bool ReadyToRender =>
        Columns is { Count: > 0 } && Columns.Any(column => column.IsVisible);

    /// <summary>
    /// Exposes the virtualizing host to subclasses and upstream tests.
    /// Do not wrap this CollectionView in a vertical ScrollView.
    /// </summary>
    protected CollectionView RowsView => rowsView;

    /// <summary>
    /// Exposes the explicit empty-state host to subclasses and tests.
    /// </summary>
    protected ContentView EmptyViewHost => emptyViewHost;

    /// <summary>
    /// Exposes the optional search host to subclasses and tests.
    /// </summary>
    protected ContentView SearchHost => searchHost;

    /// <summary>
    /// Exposes the optional pager host to subclasses and tests.
    /// </summary>
    protected ContentView PagerHost => pagerHost;

    /// <inheritdoc />
    protected override void OnSizeAllocated(double width, double height)
    {
        base.OnSizeAllocated(width, height);

        if (width > 0 && Math.Abs(lastViewportWidth - width) > 0.5)
        {
            lastViewportWidth = width;
            RecalculateColumnLayout();
        }
    }

    /// <inheritdoc />
    protected override void OnHandlerChanging(HandlerChangingEventArgs args)
    {
        if (args.NewHandler is null)
        {
            DeactivateSubscriptions();

            // A MAUI handler can be removed temporarily while the managed page and
            // control remain reusable. Do not assign CollectionView.ItemsSource or
            // tear down templates here.
            SuspendRowsPresentation();
        }

        base.OnHandlerChanging(args);
    }

    /// <inheritdoc />
    protected override void OnHandlerChanged()
    {
        base.OnHandlerChanged();

        if (Handler is not null)
        {
            ActivateSubscriptions();
            RestoreVisualResources();
            ResumeRowsPresentation();
            RebuildAll();
            RequestAutoColumnMeasurement();
        }
    }

    /// <summary>
    /// Temporarily freezes the visible ItemsSource to a snapshot. Mutate the underlying
    /// collection inside the returned scope, and the CollectionView is rebound once when
    /// the scope is disposed.
    ///
    /// Call this method and mutate the source on the UI thread.
    /// </summary>
    public IDisposable DeferRefresh()
    {
        if (deferRefreshCount++ == 0)
        {
            deferredSnapshot = ItemsSource is null
                ? null
                : new ArrayList(ItemsSource);

            if (!visualResourcesReleased)
            {
                RefreshDataView(false);
            }
        }

        return new RefreshDeferral(this);
    }

    /// <summary>
    /// Forces CollectionView to re-read the current source. This is rarely required for
    /// ObservableCollection sources, but is useful after changing a non-observable IList.
    /// </summary>
    public void Refresh()
    {
        if (deferRefreshCount > 0)
        {
            refreshPending = true;
            return;
        }

        if (visualResourcesReleased)
        {
            return;
        }

        InvalidateAppliedRowsSource();
        RefreshDataView(false);
        RefreshRealizedRows();
    }

    /// <summary>
    /// Rebuilds the header and all currently realized/recycled row presenters.
    /// Use this after changing a non-bindable DataGridColumn property such as a template.
    /// </summary>
    public void RefreshColumns()
    {
        RebuildAll();
    }

    /// <summary>
    /// Gets a stable snapshot of the configured columns.
    /// </summary>
    /// <returns>The current columns.</returns>
    internal IReadOnlyList<DataGridColumn> GetColumnsSnapshot()
    {
        return Columns?.ToArray() ?? Array.Empty<DataGridColumn>();
    }

    /// <summary>
    /// Gets the resolved absolute width of each configured column.
    /// </summary>
    /// <returns>The resolved column widths.</returns>
    internal IReadOnlyList<double> GetResolvedColumnWidths()
    {
        return resolvedColumnWidths;
    }

    /// <summary>
    /// Registers a realized row presenter with the grid.
    /// </summary>
    /// <param name="presenter">The row presenter to register.</param>
    internal void RegisterPresenter(VirtualizedDataGridRowPresenter presenter)
    {
        presenters.Add(new WeakReference<VirtualizedDataGridRowPresenter>(presenter));
        ApplySelectionState(presenter);
        RequestAutoColumnMeasurement();
    }

    /// <summary>
    /// Returns cell padding compatible with UraniumUI's selection column. The
    /// checkbox template already supplies its own horizontal margin.
    /// </summary>
    internal Thickness GetCellPadding(DataGridColumn column)
    {
        return column is IDataGridSelectionColumn
            ? new Thickness(0, CellPadding.Top, 0, CellPadding.Bottom)
            : CellPadding;
    }

    private Thickness GetHeaderPadding(DataGridColumn column)
    {
        return column is IDataGridSelectionColumn
            ? new Thickness(0, HeaderPadding.Top, 0, HeaderPadding.Bottom)
            : HeaderPadding;
    }

    /// <summary>
    /// Schedules an Auto-column measurement after bindings for newly realized rows
    /// have been applied.
    /// </summary>
    internal void RequestAutoColumnMeasurement()
    {
        if (autoColumnMeasurementScheduled ||
            visualResourcesReleased ||
            Handler is null)
        {
            return;
        }

        autoColumnMeasurementScheduled = true;

        if (!Dispatcher.Dispatch(UpdateMeasuredAutoColumnWidths))
        {
            autoColumnMeasurementScheduled = false;
        }
    }

    private void UpdateMeasuredAutoColumnWidths()
    {
        autoColumnMeasurementScheduled = false;

        if (visualResourcesReleased)
        {
            return;
        }

        var columns = GetColumnsSnapshot();
        var measured = new double[columns.Count];

        for (var index = 0; index < Math.Min(columns.Count, headerGrid.Children.Count); index++)
        {
            if (columns[index].IsVisible &&
                columns[index].Width.IsAuto &&
                headerGrid.Children[index] is View header)
            {
                measured[index] = header is ContentView { Content: View headerContent } headerCell
                    ? headerContent.Measure(
                        double.PositiveInfinity,
                        double.PositiveInfinity).Width +
                      headerCell.Padding.Left +
                      headerCell.Padding.Right
                    : header.Measure(
                        double.PositiveInfinity,
                        double.PositiveInfinity).Width;
            }
        }

        foreach (var presenter in GetLivePresenters())
        {
            var rowWidths = presenter.MeasureNaturalColumnWidths();

            for (var index = 0; index < Math.Min(measured.Length, rowWidths.Count); index++)
            {
                if (columns[index].Width.IsAuto)
                {
                    measured[index] = Math.Max(measured[index], rowWidths[index]);
                }
            }
        }

        // Separate header/row Grids must share absolute widths. WinUI can round the
        // measured label and its surrounding ContentView in opposite directions at
        // the device-pixel boundary, leaving the arranged content fractionally
        // narrower than its desired width (for example, wrapping "Personal" after
        // the final character). Ceiling plus one DIP preserves Auto semantics while
        // providing the same rounding tolerance as a single native Grid's Auto track.
        for (var index = 0; index < measured.Length; index++)
        {
            if (columns[index].Width.IsAuto && measured[index] > 0)
            {
                measured[index] = Math.Ceiling(measured[index]) + 1;
            }
        }

        var changed =
            measuredAutoColumnWidths.Count != measured.Length ||
            measuredAutoColumnWidths.Where((width, index) => Math.Abs(width - measured[index]) > 0.5).Any();

        if (!changed)
        {
            return;
        }

        measuredAutoColumnWidths = measured;
        RecalculateColumnLayout();
        InvalidateMeasure();
    }

    /// <summary>
    /// Determines whether an item belongs to the current selection.
    /// </summary>
    /// <param name="item">The item to test.</param>
    /// <returns><see langword="true"/> when the item is selected; otherwise, <see langword="false"/>.</returns>
    internal bool IsItemSelected(object? item)
    {
        return item is not null && selectedItemSet.Contains(item);
    }

    /// <summary>
    /// Applies the current selection state to a realized row presenter.
    /// </summary>
    /// <param name="presenter">The row presenter to update.</param>
    internal void ApplySelectionState(VirtualizedDataGridRowPresenter presenter)
    {
        syncingSelectionCells = true;

        try
        {
            presenter.ApplySelectionStateCore(IsItemSelected(presenter.BindingContext));
        }
        finally
        {
            syncingSelectionCells = false;
        }
    }

    /// <summary>
    /// Configures the selected and unselected visual states for a row view.
    /// </summary>
    /// <param name="view">The row view to configure.</param>
    internal void ConfigureSelectionVisualStates(View view)
    {
        VisualStateManager.SetVisualStateGroups(
            view,
            [
                new VisualStateGroup { Name = "VirtualizedDataGridSelectionStates", States = { new VisualState { Name = DataGridCellVisualStates.Selected, Setters = { new Setter { Property = BackgroundColorProperty, Value = SelectionColor.MultiplyAlpha(0.2f) } } }, new VisualState { Name = DataGridCellVisualStates.Unselected, Setters = { new Setter { Property = BackgroundColorProperty, Value = Colors.Transparent } } } } }
            ]);
    }

    /// <summary>
    /// Applies a comma-delimited collection of style classes to a view.
    /// </summary>
    /// <param name="view">The view to update.</param>
    /// <param name="styleClasses">The style classes to apply.</param>
    internal static void ApplyStyleClassToView(View? view, string? styleClasses)
    {
        if (view is null || string.IsNullOrWhiteSpace(styleClasses))
        {
            return;
        }

        var classes = view.StyleClass?.ToList() ?? [];

        foreach (var styleClass in styleClasses.Split(
                     ',',
                     StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (!classes.Contains(styleClass))
            {
                classes.Add(styleClass);
            }
        }

        view.StyleClass = classes;
    }

    private DataTemplate CreateRowTemplate()
    {
        return new DataTemplate(() => new VirtualizedDataGridRowPresenter(this));
    }

    private void RestoreVisualResources()
    {
        if (!visualResourcesReleased && rowsView.ItemTemplate is not null)
        {
            return;
        }

        visualResourcesReleased = false;
        rowsView.ItemTemplate = CreateRowTemplate();
        ApplySearchBar();
        ApplyEmptyView();
        ApplyPager();
    }

    private void ReleaseVisualResources()
    {
        visualResourcesReleased = true;
        SuspendRowsPresentation();

        // This method is reserved for an explicit final-release path. Ordinary
        // Shell/routed-page handler changes use SuspendRowsPresentation and retain
        // the reusable templates and logical row source.
        rowsView.ItemTemplate = null;
        rowsView.EmptyView = null;
        rowsView.EmptyViewTemplate = null;

        searchHost.Content = null;
        searchHost.IsVisible = false;
        emptyViewHost.Content = null;
        emptyViewHost.IsVisible = false;
        pagerHost.Content = null;
        pagerHost.IsVisible = false;

        headerGrid.Children.Clear();
        headerGrid.ColumnDefinitions.Clear();
        headerSection.IsVisible = false;

        presenters.Clear();
        deferredSnapshot = null;
        deferRefreshCount = 0;
        refreshPending = false;
    }

    private void RebuildAll()
    {
        if (visualResourcesReleased)
        {
            return;
        }

        RecalculateColumnLayout();
        RenderHeader();
        UpdateRowsHost();
        RefreshRealizedRows();
    }

    /// <summary>
    /// Rebuilds the header from the current columns.
    /// </summary>
    internal void RenderHeader()
    {
        if (visualResourcesReleased)
        {
            return;
        }

        headerGrid.Children.Clear();
        headerGrid.ColumnDefinitions.Clear();
        headerGrid.ColumnSpacing = Math.Max(0, ColumnSpacing);

        var columns = GetColumnsSnapshot();
        EnsureResolvedWidths(columns.Count);

        for (var index = 0; index < columns.Count; index++)
        {
            var column = columns[index];
            var width = index < resolvedColumnWidths.Count
                ? resolvedColumnWidths[index]
                : 0;

            headerGrid.ColumnDefinitions.Add(
                new ColumnDefinition { Width = new GridLength(Math.Max(0, width), GridUnitType.Absolute) });

            var titleBinding = new Binding(nameof(DataGridColumn.Title));

            var titleView =
                column.TitleView
                ?? TitleTemplate?.CreateContent() as View
                ?? LabelFactory(titleBinding)
                ?? CreateLabel(titleBinding);

            if (titleView is Label label)
            {
                label.FontAttributes = FontAttributes.Bold;
            }

            titleView.BindingContext = column;
            titleView.SetBinding(
                IsVisibleProperty,
                new Binding(nameof(DataGridColumn.IsVisible), source: column));

            ApplyStyleClassToView(titleView, column.HeaderStyleClass);

            var headerCell = new ContentView
            {
                Content = titleView,
                Padding = GetHeaderPadding(column),
                BindingContext = column,
                IsVisible = column.IsVisible
            };

            Grid.SetColumn(headerCell, index);
            headerGrid.Children.Add(headerCell);
        }

        headerSection.IsVisible = ShowHeaders && ReadyToRender;
    }

    /// <summary>
    /// Rebuilds all currently realized row presenters.
    /// </summary>
    internal void RefreshRealizedRows()
    {
        if (visualResourcesReleased)
        {
            return;
        }

        foreach (var presenter in GetLivePresenters())
        {
            presenter.RefreshFromOwner();
        }
    }

    /// <summary>
    /// Resolves column widths for the current viewport and updates realized content.
    /// </summary>
    internal void RecalculateColumnLayout()
    {
        if (visualResourcesReleased)
        {
            return;
        }

        var columns = GetColumnsSnapshot();
        var widths = new double[columns.Count];
        var viewportWidth = GetViewportWidth();

        var visibleIndices = new List<int>();
        var fixedWidth = 0d;
        var starWeight = 0d;

        for (var index = 0; index < columns.Count; index++)
        {
            var column = columns[index];

            if (!column.IsVisible)
            {
                widths[index] = 0;
                continue;
            }

            visibleIndices.Add(index);

            if (column.Width.IsAbsolute)
            {
                widths[index] = Math.Max(0, column.Width.Value);
                fixedWidth += widths[index];
            }
            else if (column.Width.IsAuto)
            {
                var measuredWidth =
                    index < measuredAutoColumnWidths.Count
                        ? measuredAutoColumnWidths[index]
                        : 0;

                widths[index] = measuredWidth > 0
                    ? measuredWidth
                    : Math.Max(0, AutoColumnWidth);
                fixedWidth += widths[index];
            }
            else
            {
                starWeight += Math.Max(0.0001, column.Width.Value);
            }
        }

        var spacing = Math.Max(0, ColumnSpacing) * Math.Max(0, visibleIndices.Count - 1);
        var availableForStars = Math.Max(0, viewportWidth - fixedWidth - spacing);
        var starUnit = starWeight > 0
            ? Math.Max(Math.Max(0, MinimumStarColumnWidth), availableForStars / starWeight)
            : 0;

        for (var index = 0; index < columns.Count; index++)
        {
            var column = columns[index];

            if (column.IsVisible && column.Width.IsStar)
            {
                widths[index] = starUnit * Math.Max(0.0001, column.Width.Value);
            }
        }

        var totalWidth = widths.Sum() + spacing;

        if (FillAvailableWidth &&
            visibleIndices.Count > 0 &&
            totalWidth < viewportWidth)
        {
            var lastVisibleIndex = visibleIndices[^1];
            widths[lastVisibleIndex] += viewportWidth - totalWidth;
            totalWidth = viewportWidth;
        }

        resolvedColumnWidths = widths;
        RenderHeader();

        foreach (var presenter in GetLivePresenters())
        {
            presenter.ApplyColumnWidths(widths);
        }
    }

    private double GetViewportWidth()
    {
        var width = lastViewportWidth > 0
            ? lastViewportWidth
            : Width > 0
                ? Width
                : WidthRequest > 0
                    ? WidthRequest
                    : 0;

        return Math.Max(0, width - Padding.Left - Padding.Right);
    }

    private void EnsureResolvedWidths(int expectedCount)
    {
        if (resolvedColumnWidths.Count == expectedCount)
        {
            return;
        }

        resolvedColumnWidths = new double[expectedCount];
    }

    private IEnumerable<VirtualizedDataGridRowPresenter> GetLivePresenters()
    {
        for (var index = presenters.Count - 1; index >= 0; index--)
        {
            if (presenters[index].TryGetTarget(out var presenter))
            {
                yield return presenter;
            }
            else
            {
                presenters.RemoveAt(index);
            }
        }
    }

    private void UpdateRowsHost()
    {
        if (visualResourcesReleased)
        {
            return;
        }

        ApplyRowsViewConfiguration();
        horizontalScrollView.HorizontalScrollBarVisibility = HorizontalScrollBarVisibility;

        ApplySearchBar();
        ApplyEmptyView();
        ApplyPager();
        RefreshDataView(false);
    }

    private void SetRowsItemsSource(IList? source)
    {
        SetDesiredRowsSource(source);
    }

    /// <summary>
    /// Applies the configured empty view or empty-view template to the rows host.
    /// </summary>
    private void ApplyEmptyView()
    {
        if (visualResourcesReleased)
        {
            return;
        }

        // Do not use CollectionView.EmptyView here. Keeping the empty state in an
        // explicit overlay makes visibility deterministic and avoids writing to the
        // child CollectionView while its native handler is detached.
        var content = EmptyView is not null
            ? EmptyView
            : EmptyViewTemplate?.CreateContent() is View templateContent
                ? templateContent
                : new BoxView { HorizontalOptions = LayoutOptions.Fill, VerticalOptions = LayoutOptions.Fill, Margin = 40 };

        if (!ReferenceEquals(emptyViewHost.Content, content))
        {
            emptyViewHost.Content = content;
        }

        UpdateEmptyViewVisibility();
    }

    private void UpdateEmptyViewVisibility()
    {
        if (visualResourcesReleased)
        {
            return;
        }

        var showEmptyView = ReadyToRender && IsEmpty;
        emptyViewHost.IsVisible = showEmptyView;

        if (CanUseRowsPlatformHost)
        {
            rowsView.IsVisible = ReadyToRender && !showEmptyView;
        }
    }

    private void SetItemSizingStrategy(ItemSizingStrategy strategy)
    {
        ApplyRowsViewConfiguration();
    }

    private void SetHorizontalScrollBarVisibility(ScrollBarVisibility visibility)
    {
        if (!visualResourcesReleased)
        {
            horizontalScrollView.HorizontalScrollBarVisibility = visibility;
        }
    }

    private void SetVerticalScrollBarVisibility(ScrollBarVisibility visibility)
    {
        ApplyRowsViewConfiguration();
    }

    private void OnItemsSourceSet(IList? oldSource, IList? newSource)
    {
        if (subscriptionsActive)
        {
            DetachItemsSource();
            AttachItemsSource(newSource);
        }

        CurrentType = ResolveItemType(newSource);

        if (UseAutoColumns)
        {
            SetAutoColumns();
        }

        if (newSource is null)
        {
            // Explicitly clear the host. The original Grid-based DataGrid returned early here,
            // leaving its old visual tree attached.
            deferredSnapshot = null;
        }

        RefreshDataView(false);
        PruneSelectionAgainstItemsSource();
    }

    private void OnColumnsSet(
        IList<DataGridColumn>? oldColumns,
        IList<DataGridColumn>? newColumns)
    {
        if (subscriptionsActive)
        {
            DetachColumns();
            AttachColumns(newColumns);
        }

        measuredAutoColumnWidths = Array.Empty<double>();
        RebuildAll();
        RequestAutoColumnMeasurement();
    }

    private void OnSelectedItemsSet(IList? oldValue, IList? newValue)
    {
        if (subscriptionsActive)
        {
            DetachSelectedItems();
            AttachSelectedItems(newValue);
        }

        RefreshSelectedItemSet();
        RefreshSelectionVisuals();
    }

    private void ActivateSubscriptions()
    {
        if (subscriptionsActive)
        {
            return;
        }

        subscriptionsActive = true;
        AttachItemsSource(ItemsSource);
        AttachColumns(Columns);
        AttachSelectedItems(SelectedItems);
    }

    private void DeactivateSubscriptions()
    {
        if (!subscriptionsActive)
        {
            return;
        }

        DetachItemsSource();
        DetachColumns();
        DetachSelectedItems();
        subscriptionsActive = false;
    }

    private void AttachItemsSource(IList? source)
    {
        if (source is INotifyCollectionChanged observable)
        {
            observedItemsSource = observable;
            observedItemsSource.CollectionChanged += ItemsSource_CollectionChanged;
        }
    }

    private void DetachItemsSource()
    {
        if (observedItemsSource is null)
        {
            return;
        }

        observedItemsSource.CollectionChanged -= ItemsSource_CollectionChanged;
        observedItemsSource = null;
    }

    private void AttachColumns(IList<DataGridColumn>? columns)
    {
        if (columns is INotifyCollectionChanged observable)
        {
            observedColumns = observable;
            observedColumns.CollectionChanged += Columns_CollectionChanged;
        }

        if (columns is null)
        {
            return;
        }

        foreach (var column in columns)
        {
            if (subscribedColumns.Add(column))
            {
                column.PropertyChanged += Column_PropertyChanged;
            }

            if (column is IDataGridSelectionColumn selectionColumn &&
                subscribedSelectionColumns.Add(selectionColumn))
            {
                selectionColumn.SelectionChanged += SelectionColumn_SelectionChanged;
            }
        }
    }

    private void DetachColumns()
    {
        observedColumns?.CollectionChanged -= Columns_CollectionChanged;
        observedColumns = null;

        foreach (var column in subscribedColumns)
        {
            column.PropertyChanged -= Column_PropertyChanged;
        }

        foreach (var selectionColumn in subscribedSelectionColumns)
        {
            selectionColumn.SelectionChanged -= SelectionColumn_SelectionChanged;
        }

        subscribedColumns.Clear();
        subscribedSelectionColumns.Clear();
    }

    private void AttachSelectedItems(IList? selectedItems)
    {
        if (selectedItems is INotifyCollectionChanged observable)
        {
            observedSelectedItems = observable;
            observedSelectedItems.CollectionChanged += SelectedItems_CollectionChanged;
        }

        RefreshSelectedItemSet();
    }

    private void DetachSelectedItems()
    {
        if (observedSelectedItems is null)
        {
            return;
        }

        observedSelectedItems.CollectionChanged -= SelectedItems_CollectionChanged;
        observedSelectedItems = null;
    }

    private void ItemsSource_CollectionChanged(
        object? sender,
        NotifyCollectionChangedEventArgs e)
    {
        if (CurrentType is null && e.NewItems is { Count: > 0 })
        {
            CurrentType = e.NewItems
                .Cast<object?>()
                .FirstOrDefault(item => item is not null)
                ?.GetType();

            if (UseAutoColumns)
            {
                SetAutoColumns();
            }
        }

        if (e.Action is NotifyCollectionChangedAction.Remove or NotifyCollectionChangedAction.Replace &&
            e.OldItems is not null)
        {
            RemoveSelectedItems(e.OldItems);
        }
        else if (e.Action == NotifyCollectionChangedAction.Reset)
        {
            PruneSelectionAgainstItemsSource();
        }

        RefreshDataView(false);
    }

    private void Columns_CollectionChanged(
        object? sender,
        NotifyCollectionChangedEventArgs e)
    {
        // Rebuild only the header and the bounded set of realized/recycled rows.
        // No operation is proportional to ItemsSource.Count.
        DetachColumns();
        AttachColumns(Columns);
        RebuildAll();
    }

    private void Column_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(DataGridColumn.Title))
        {
            RenderHeader();
            return;
        }

        RecalculateColumnLayout();
        RefreshRealizedRows();
    }

    private void SelectedItems_CollectionChanged(
        object? sender,
        NotifyCollectionChangedEventArgs e)
    {
        RefreshSelectedItemSet();
        RefreshSelectionVisuals();
    }

    private void SelectionColumn_SelectionChanged(object? sender, bool isSelected)
    {
        if (syncingSelectionCells ||
            sender is not View view ||
            view.BindingContext is null)
        {
            return;
        }

        var item = view.BindingContext;

        try
        {
            if (isSelected)
            {
                if (!SelectedItems.Contains(item))
                {
                    SelectedItems.Add(item);
                }
            }
            else
            {
                SelectedItems.Remove(item);
            }
        }
        catch (NotSupportedException)
        {
            // The caller supplied a read-only SelectedItems collection.
            return;
        }

        OnPropertyChanged(nameof(SelectedItems));
        RefreshSelectedItemSet();
        RefreshSelectionVisuals();
    }

    /// <summary>
    /// Refreshes selection visual states for all realized rows.
    /// </summary>
    internal void RefreshSelectionVisuals()
    {
        if (visualResourcesReleased)
        {
            return;
        }

        syncingSelectionCells = true;

        try
        {
            foreach (var presenter in GetLivePresenters())
            {
                ConfigureSelectionVisualStates(presenter);
                presenter.ApplySelectionStateCore(
                    IsItemSelected(presenter.BindingContext));
            }
        }
        finally
        {
            syncingSelectionCells = false;
        }
    }

    private void RefreshSelectedItemSet()
    {
        selectedItemSet.Clear();

        if (SelectedItems is null)
        {
            return;
        }

        foreach (var item in SelectedItems)
        {
            if (item is not null)
            {
                selectedItemSet.Add(item);
            }
        }
    }

    private void RemoveSelectedItems(IList removedItems)
    {
        foreach (var item in removedItems.Cast<object?>().Where(item => item is not null))
        {
            try
            {
                SelectedItems.Remove(item!);
            }
            catch (NotSupportedException)
            {
                return;
            }
        }
    }

    private void PruneSelectionAgainstItemsSource()
    {
        if (SelectedItems is null || SelectedItems.Count == 0)
        {
            return;
        }

        var availableItems = ItemsSource is null
            ? []
            : ItemsSource.Cast<object?>()
                .Where(item => item is not null)
                .Cast<object>()
                .ToHashSet();

        for (var index = SelectedItems.Count - 1; index >= 0; index--)
        {
            var selected = SelectedItems[index];

            if (selected is null || !availableItems.Contains(selected))
            {
                try
                {
                    SelectedItems.RemoveAt(index);
                }
                catch (NotSupportedException)
                {
                    return;
                }
            }
        }

        RefreshSelectedItemSet();
        RefreshSelectionVisuals();
    }

    /// <summary>
    /// Generates columns from the inferred item type when automatic columns are enabled.
    /// </summary>
    internal void SetAutoColumns()
    {
        if (!UseAutoColumns || CurrentType is null || settingAutoColumns)
        {
            return;
        }

        settingAutoColumns = true;

        try
        {
            Columns = new ObservableCollection<DataGridColumn>(
                CurrentType
                    .GetProperties(BindingFlags.Instance | BindingFlags.Public)
                    .Where(property =>
                        property.GetCustomAttribute<DataGridIgnoreAttribute>() is null)
                    .Select(property => new DataGridColumn
                    {
                        Title =
                            property.GetCustomAttribute<DisplayNameAttribute>()?.DisplayName
                            ?? property.GetCustomAttribute<DisplayAttribute>()?.Name
                            ?? property.Name,
                        ValueBinding = new Binding(property.Name)
                    }));
        }
        finally
        {
            settingAutoColumns = false;
        }
    }

    private static Type? ResolveItemType(IList? source)
    {
        if (source is null)
        {
            return null;
        }

        var sourceType = source.GetType();

        if (sourceType.IsArray)
        {
            return sourceType.GetElementType();
        }

        var enumerableType = sourceType
            .GetInterfaces()
            .Append(sourceType)
            .FirstOrDefault(type =>
                type.IsGenericType &&
                type.GetGenericTypeDefinition() == typeof(IEnumerable<>));

        return enumerableType is not null
            ? enumerableType.GetGenericArguments()[0]
            : source
                .Cast<object?>()
                .FirstOrDefault(item => item is not null)
                ?.GetType();
    }

    private void EndDeferRefresh()
    {
        if (deferRefreshCount <= 0)
        {
            return;
        }

        deferRefreshCount--;

        if (deferRefreshCount > 0)
        {
            return;
        }

        deferredSnapshot = null;

        if (!visualResourcesReleased)
        {
            RefreshDataView(false);
        }

        if (refreshPending)
        {
            refreshPending = false;
            Refresh();
        }
    }

    /// <summary>
    /// Defines the visual-state names used to represent row selection.
    /// </summary>
    public static class DataGridCellVisualStates
    {
        /// <summary>
        /// Identifies the selected visual state.
        /// </summary>
        public const string Selected = "Selected";

        /// <summary>
        /// Identifies the unselected visual state.
        /// </summary>
        public const string Unselected = "Unselected";
    }

    private sealed class RefreshDeferral(VirtualizedDataGrid owningGrid) : IDisposable
    {
        private VirtualizedDataGrid? owner = owningGrid;

        public void Dispose()
        {
            var currentOwner = Interlocked.Exchange(ref owner, null);
            currentOwner?.EndDeferRefresh();
        }
    }
}
