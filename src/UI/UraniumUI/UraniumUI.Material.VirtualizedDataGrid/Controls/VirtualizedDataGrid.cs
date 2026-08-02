using System.Collections;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Reflection;
using UraniumUI.Material.Controls;

namespace UraniumUI.Material.VirtualizedDataGrid.Controls;

/// <summary>
/// A DataGrid-compatible control that virtualizes rows through a bounded pool
/// of presenters inside a lightweight scrolling extent.
///
/// The header is kept outside the vertical rows viewport, while a horizontal-only
/// ScrollView moves the header and realized rows together.
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
    private readonly VirtualizedDataGridRowsHost rowsView;
    private readonly ContentView emptyViewHost;
    private readonly ContentView pagerHost;

    private readonly List<WeakReference<VirtualizedDataGridRowPresenter>> presenters = [];
    private readonly HashSet<object> selectedItemSet = [];
    private readonly HashSet<DataGridColumn> subscribedColumns = [];
    private readonly HashSet<IDataGridSelectionColumn> subscribedSelectionColumns = [];

    private INotifyCollectionChanged? observedItemsSource;
    private INotifyCollectionChanged? observedColumns;
    private INotifyCollectionChanged? observedSelectedItems;

    private IReadOnlyList<double> resolvedColumnWidths = [];
    private bool subscriptionsActive;
    private bool visualResourcesReleased;
    private bool settingAutoColumns;
    private bool syncingSelectionCells;
    private int deferRefreshCount;
    private bool refreshPending;
    private IList? deferredSnapshot;
    private double lastViewportWidth = -1;
    private IReadOnlyList<double> measuredAutoColumnWidths = [];
    private bool autoColumnMeasurementScheduled;
    private bool autoColumnWidthsFrozen;
    private int autoColumnMeasurementGeneration;

    /// <summary>
    /// Initializes a new instance of the <see cref="VirtualizedDataGrid"/> class.
    /// </summary>
    public VirtualizedDataGrid()
    {
        InitializeFactoryMethods();
        InitializeDataView();
        Padding = new Thickness(5, 10);

        headerGrid = new Grid { HorizontalOptions = LayoutOptions.Fill };

        headerSection = new Grid { HorizontalOptions = LayoutOptions.Fill, RowDefinitions = { new RowDefinition(GridLength.Auto), new RowDefinition(GridLength.Auto) } };

        headerSection.Add(headerGrid, 0, 0);

        var headerSeparator = CreateRowSeparator();
        Grid.SetRow(headerSeparator, 1);
        headerSection.Children.Add(headerSeparator);

        rowsView = new VirtualizedDataGridRowsHost(this);

        AttachRowsViewLifecycle();

        emptyViewHost = new ContentView { HorizontalOptions = LayoutOptions.Fill, VerticalOptions = LayoutOptions.Fill, IsVisible = false };

        rowsHost = new Grid { HorizontalOptions = LayoutOptions.Fill, VerticalOptions = LayoutOptions.Fill };
        rowsHost.Children.Add(rowsView);
        rowsHost.Children.Add(emptyViewHost);

        tableLayout = new Grid { HorizontalOptions = LayoutOptions.Start, VerticalOptions = LayoutOptions.Fill, RowDefinitions = { new RowDefinition(GridLength.Auto), new RowDefinition(GridLength.Star) } };

        tableLayout.Add(headerSection, 0, 0);
        tableLayout.Add(rowsHost, 0, 1);

        horizontalScrollView = new ScrollView { Orientation = ScrollOrientation.Horizontal, HorizontalOptions = LayoutOptions.Fill, VerticalOptions = LayoutOptions.Fill, Content = tableLayout };

        searchHost = new ContentView { HorizontalOptions = LayoutOptions.Fill, VerticalOptions = LayoutOptions.Center, IsVisible = false };

        pagerHost = new ContentView { HorizontalOptions = LayoutOptions.Fill, VerticalOptions = LayoutOptions.Center, IsVisible = false };

        rootLayout = new Grid { HorizontalOptions = LayoutOptions.Fill, VerticalOptions = LayoutOptions.Fill, RowDefinitions = { new RowDefinition(GridLength.Auto), new RowDefinition(GridLength.Star), new RowDefinition(GridLength.Auto) } };
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
    public Type? CurrentType { get; private set; }

    /// <summary>
    /// Gets a value indicating whether the grid has at least one visible column and can render rows.
    /// </summary>
    public bool ReadyToRender =>
        Columns is { Count: > 0 } && Columns.Any(column => column.IsVisible);

    /// <summary>
    /// Exposes the vertical scrolling viewport to subclasses and tests.
    /// </summary>
    protected ScrollView RowsView => rowsView;

    /// <summary>Creates an unhosted row presenter for derived controls and tests.</summary>
    protected Grid CreateRowPresenter() =>
        new VirtualizedDataGridRowPresenter(this);

    /// <summary>Gets the number of rows currently realized in the viewport.</summary>
    protected int RealizedRowCount => rowsView.RealizedCount;

    /// <summary>Gets the calculated vertical scrolling extent.</summary>
    protected double RowsExtentHeight => rowsView.ExtentHeight;

    /// <summary>Forces an immediate viewport calculation.</summary>
    protected void UpdateRowsViewport() => rowsView.UpdateViewportNow();

    /// <summary>Calculates a viewport for an explicit vertical offset.</summary>
    protected void UpdateRowsViewport(double offset, double viewportHeight) =>
        rowsView.UpdateViewport(offset, viewportHeight);

    /// <summary>Gets the logical row indices currently represented by the pool.</summary>
    protected IReadOnlyCollection<int> RealizedRowIndices => rowsView.RealizedIndices;

    /// <summary>
    /// Gets the latest logical row source selected by filtering and paging.
    /// The rows host only realizes the bounded viewport subset.
    /// </summary>
    protected IList? DisplayedItemsSource => desiredRowsSource;

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

        if (double.IsFinite(width) &&
            width > 0 &&
            Math.Abs(lastViewportWidth - width) > 0.5)
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
            // control remain reusable. Detach only the bounded presenter pool.
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
    /// collection inside the returned scope, and the rows host is rebound once when
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
    /// Forces the rows host to re-read the current source. This is rarely required for
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
        ResetAutoColumnMeasurement();
        RebuildAll();
        RequestAutoColumnMeasurement();
    }

    /// <summary>
    /// Gets a stable snapshot of the configured columns.
    /// </summary>
    /// <returns>The current columns.</returns>
    internal IReadOnlyList<DataGridColumn> GetColumnsSnapshot()
    {
        return Columns?.ToArray() ?? [];
    }

    /// <summary>
    /// Gets the resolved absolute width of each configured column.
    /// </summary>
    /// <returns>The resolved column widths.</returns>
    protected internal IReadOnlyList<double> GetResolvedColumnWidths()
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
        Diagnostics.PresenterCreated();
        ApplySelectionState(presenter);
        RequestRowAutoColumnMeasurement();
    }

    internal void ReportRowHeight(int index, double height) =>
        rowsView.ReportMeasuredHeight(index, height);

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
    private void RequestAutoColumnMeasurement()
    {
        if (autoColumnMeasurementScheduled ||
            autoColumnWidthsFrozen ||
            visualResourcesReleased ||
            Handler is null)
        {
            return;
        }

        autoColumnMeasurementScheduled = true;
        var generation = autoColumnMeasurementGeneration;

        if (!Dispatcher.Dispatch(() =>
            {
                if (generation != autoColumnMeasurementGeneration)
                {
                    return;
                }

                UpdateMeasuredAutoColumnWidths();
            }))
        {
            autoColumnMeasurementScheduled = false;
        }
    }

    /// <summary>
    /// Schedules content measurement only when a realized row can contribute to
    /// an Auto column's width. Templated Auto columns deliberately use the stable
    /// <see cref="AutoColumnWidth"/> fallback, so recycling those rows must not
    /// trigger a redundant header/presenter measurement pass.
    /// </summary>
    internal void RequestRowAutoColumnMeasurement()
    {
        if (!HasContentMeasuredAutoColumn())
        {
            return;
        }

        RequestAutoColumnMeasurement();
    }

    /// <summary>
    /// Gets whether any visible Auto column uses the generated cell whose natural
    /// width can be measured safely.
    /// </summary>
    public bool HasContentMeasuredAutoColumn()
    {
        return CellItemTemplate is not null
            ? false
            : Columns is not null &&
              Columns.Any(column =>
                  column.IsVisible &&
                  column.Width.IsAuto &&
                  column.CellItemTemplate is null);
    }

    private void UpdateMeasuredAutoColumnWidths()
    {
        autoColumnMeasurementScheduled = false;

        if (visualResourcesReleased || autoColumnWidthsFrozen)
        {
            return;
        }

        var diagnosticsStarted = Diagnostics.StartTiming();

        var columns = GetColumnsSnapshot();
        var livePresenters = GetLivePresenters().ToList();
        if (HasContentMeasuredAutoColumn() &&
            !livePresenters.Any(presenter => presenter.HasBoundItem))
        {
            Diagnostics.RecordAutoColumnMeasurement(diagnosticsStarted);
            return;
        }

        var measured = new double[columns.Count];

        // Keep widths already learned for this column set. Recycled row
        // presenters continuously receive new BindingContexts while scrolling and
        // filtering. Recomputing from only the currently realized rows makes columns
        // alternately shrink and grow, which triggers another layout/recycle pass and
        // produces a long-running visual "crawl".
        for (var index = 0;
             index < Math.Min(measured.Length, measuredAutoColumnWidths.Count);
             index++)
        {
            measured[index] = measuredAutoColumnWidths[index];
        }

        // A custom template may contain Fill-sized controls whose desired width is
        // derived from the column width they were previously assigned. Feeding that
        // value back into an Auto column creates unbounded horizontal growth. Custom
        // templates therefore use the documented stable AutoColumnWidth fallback;
        // content-based refinement is reserved for the grid's generated labels.
        for (var index = 0; index < columns.Count; index++)
        {
            if (columns[index].Width.IsAuto &&
                (columns[index].CellItemTemplate is not null ||
                 CellItemTemplate is not null))
            {
                measured[index] = Math.Max(
                    measured[index],
                    Math.Max(0, AutoColumnWidth));
            }
        }

        for (var index = 0; index < Math.Min(columns.Count, headerGrid.Children.Count); index++)
        {
            if (columns[index].IsVisible &&
                columns[index].Width.IsAuto &&
                headerGrid.Children[index] is View header)
            {
                var headerWidth = header is ContentView { Content: View headerContent } headerCell
                    ? headerContent.Measure(
                          double.PositiveInfinity,
                          double.PositiveInfinity).Width +
                      headerCell.Padding.Left +
                      headerCell.Padding.Right
                    : header.Measure(
                        double.PositiveInfinity,
                        double.PositiveInfinity).Width;

                measured[index] = Math.Max(
                    measured[index],
                    NormalizeMeasuredAutoWidth(headerWidth));
            }
        }

        foreach (var presenter in livePresenters.Take(1))
        {
            var rowWidths = presenter.MeasureNaturalColumnWidths();

            for (var index = 0; index < Math.Min(measured.Length, rowWidths.Count); index++)
            {
                if (columns[index].Width.IsAuto &&
                    columns[index].CellItemTemplate is null &&
                    CellItemTemplate is null)
                {
                    measured[index] = Math.Max(
                        measured[index],
                        NormalizeMeasuredAutoWidth(rowWidths[index]));
                }
            }
        }

        var changed =
            measuredAutoColumnWidths.Count != measured.Length ||
            measuredAutoColumnWidths.Where((width, index) => Math.Abs(width - measured[index]) > 0.5).Any();

        autoColumnWidthsFrozen = true;

        if (!changed)
        {
            Diagnostics.RecordAutoColumnMeasurement(diagnosticsStarted);
            return;
        }

        measuredAutoColumnWidths = measured;
        RecalculateColumnLayout();
        InvalidateMeasure();
        Diagnostics.RecordAutoColumnMeasurement(diagnosticsStarted);
    }

    private static double NormalizeMeasuredAutoWidth(double width)
    {
        if (width <= 0)
        {
            return 0;
        }

        // Separate header/row Grids must share absolute widths. WinUI can round the
        // measured content and its Grid track in opposite directions at a device-pixel
        // boundary. Apply this allowance to each new candidate—not to an already
        // normalized stored width—so repeated measurements cannot grow by one DIP.
        return Math.Ceiling(width) + 1;
    }

    /// <summary>
    /// Determines whether an item belongs to the current selection.
    /// </summary>
    /// <param name="item">The item to test.</param>
    /// <returns><see langword="true"/> when the item is selected; otherwise, <see langword="false"/>.</returns>
    private bool IsItemSelected(object? item)
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

    private void RestoreVisualResources()
    {
        if (!visualResourcesReleased)
        {
            return;
        }

        visualResourcesReleased = false;
        ApplySearchBar();
        ApplyEmptyView();
        ApplyPager();
    }

    private void ReleaseVisualResources()
    {
        visualResourcesReleased = true;
        SuspendRowsPresentation();

        rowsView.SetItemsSource(null);

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

    private void ReleaseRealizedRows()
    {
        var diagnosticsStarted = Diagnostics.StartTiming();
        var released = rowsView.ReleaseRows();

        presenters.Clear();
        autoColumnMeasurementScheduled = false;
        autoColumnMeasurementGeneration++;
        Diagnostics.RecordRealizedRowsRelease(
            diagnosticsStarted,
            released.Presenters,
            released.Cells);
    }

    private void ResetAutoColumnMeasurement()
    {
        measuredAutoColumnWidths = Array.Empty<double>();
        autoColumnWidthsFrozen = false;
        autoColumnMeasurementScheduled = false;
        autoColumnMeasurementGeneration++;
    }

    private void RebuildAll()
    {
        if (visualResourcesReleased)
        {
            return;
        }

        var diagnosticsStarted = Diagnostics.StartTiming();

        try
        {
            RecalculateColumnLayout();
            RenderHeader();
            UpdateRowsHost();
            RefreshRealizedRows();
        }
        finally
        {
            Diagnostics.RecordRebuild(diagnosticsStarted);
        }
    }

    /// <summary>
    /// Rebuilds the header from the current columns.
    /// </summary>
    private void RenderHeader()
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

            var headerCell = new ContentView { Content = titleView, Padding = GetHeaderPadding(column), BindingContext = column, IsVisible = column.IsVisible };

            Grid.SetColumn(headerCell, index);
            headerGrid.Children.Add(headerCell);
        }

        headerSection.IsVisible = ShowHeaders && ReadyToRender;
    }

    /// <summary>
    /// Rebuilds all currently realized row presenters.
    /// </summary>
    private void RefreshRealizedRows()
    {
        if (visualResourcesReleased)
        {
            return;
        }

        var diagnosticsStarted = Diagnostics.StartTiming();

        try
        {
            foreach (var presenter in GetLivePresenters())
            {
                presenter.RefreshFromOwner();
            }
        }
        finally
        {
            Diagnostics.RecordRealizedRowsRefresh(diagnosticsStarted);
        }
    }

    /// <summary>
    /// Resolves column widths for the current viewport and updates realized content.
    /// </summary>
    private void RecalculateColumnLayout()
    {
        if (visualResourcesReleased)
        {
            return;
        }

        var diagnosticsStarted = Diagnostics.StartTiming();

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
                widths[index] = NormalizeWidth(column.Width.Value);
                fixedWidth += widths[index];
            }
            else if (column.Width.IsAuto)
            {
                var measuredWidth =
                    index < measuredAutoColumnWidths.Count
                        ? measuredAutoColumnWidths[index]
                        : 0;

                widths[index] = double.IsFinite(measuredWidth) && measuredWidth > 0
                    ? measuredWidth
                    : NormalizeWidth(AutoColumnWidth);
                fixedWidth += widths[index];
            }
            else
            {
                starWeight += GetStarWeight(column.Width.Value);
            }
        }

        var spacing = NormalizeWidth(ColumnSpacing) * Math.Max(0, visibleIndices.Count - 1);
        var availableForStars = Math.Max(0, viewportWidth - fixedWidth - spacing);
        var starUnit = starWeight > 0
            ? Math.Max(NormalizeWidth(MinimumStarColumnWidth), availableForStars / starWeight)
            : 0;

        for (var index = 0; index < columns.Count; index++)
        {
            var column = columns[index];

            if (column.IsVisible && column.Width.IsStar)
            {
                widths[index] = starUnit * GetStarWeight(column.Width.Value);
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
        rowsView.ApplyColumnWidth(totalWidth);

        foreach (var presenter in GetLivePresenters())
        {
            presenter.ApplyColumnWidths(widths);
        }

        Diagnostics.RecordColumnLayout(diagnosticsStarted);
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

        if (!double.IsFinite(width) || width <= 0)
        {
            return 0;
        }

        return NormalizeWidth(width - Padding.Left - Padding.Right);
    }

    private static double NormalizeWidth(double width) =>
        double.IsFinite(width) && width > 0 ? width : 0;

    private static double GetStarWeight(double weight) =>
        double.IsFinite(weight) && weight > 0 ? weight : 0.0001;

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

        rowsView.IsVisible = ReadyToRender && !showEmptyView;
    }

    private void SetItemSizingStrategy(ItemSizingStrategy strategy)
    {
        ApplyRowsViewConfiguration();
        rowsView.RefreshRows();
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
        if (Diagnostics.IsEnabled)
        {
            Diagnostics.ItemsSourceChangeCount++;
        }
        if (subscriptionsActive)
        {
            DetachItemsSource();
            AttachItemsSource(newSource);
        }

        CurrentType = ResolveItemType(newSource);
        ResetAutoColumnMeasurement();

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

        if (!HasContentMeasuredAutoColumn())
        {
            RequestAutoColumnMeasurement();
        }
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

        ResetAutoColumnMeasurement();
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
        if (Diagnostics.IsEnabled)
        {
            Diagnostics.ItemsSourceCollectionChangeCount++;
        }
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
        ResetAutoColumnMeasurement();
        RebuildAll();
        RequestAutoColumnMeasurement();
    }

    private void Column_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        ResetAutoColumnMeasurement();

        if (e.PropertyName == nameof(DataGridColumn.Title))
        {
            RenderHeader();
            RequestAutoColumnMeasurement();
            return;
        }

        RecalculateColumnLayout();
        RefreshRealizedRows();
        RequestAutoColumnMeasurement();
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
