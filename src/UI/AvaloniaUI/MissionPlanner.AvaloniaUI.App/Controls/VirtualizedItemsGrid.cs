using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Globalization;
using System.Linq;
using System.Reflection;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Templates;
using Avalonia.Layout;
using Avalonia.Media;

namespace MissionPlanner.AvaloniaUI.App.Controls;

/// <summary>
/// A lightweight, table-shaped shell around Avalonia ItemsRepeater.
///
/// Design goals:
/// - row virtualization remains owned by ItemsRepeater;
/// - header and row templates share one authoritative column geometry;
/// - optional client-side search and paging;
/// - reusable empty content;
/// - checkbox-based single/multiple selection;
/// - synchronized sticky header while horizontally scrolling;
/// - no DataGrid column/container overhead.
///
/// For large remote data sets, leave ShowPagination/SearchText bound to the
/// ViewModel and provide the already filtered/paged ItemsSource instead.
/// </summary>
public sealed class VirtualizedItemsGrid : TemplatedControl
{
    private readonly ObservableCollection<object> _viewItems = new();
    private readonly ObservableCollection<object> _selectedItems = new();
    private readonly ReadOnlyObservableCollection<object> _readonlySelectedItems;
    private readonly List<WeakReference<CheckBox>> _selectionCheckBoxes = new();

    private INotifyCollectionChanged? _observableSource;
    private ItemsRepeater? _repeater;
    private ScrollViewer? _headerScrollViewer;
    private ScrollViewer? _bodyScrollViewer;
    private Button? _firstPageButton;
    private Button? _previousPageButton;
    private Button? _nextPageButton;
    private Button? _lastPageButton;
    private CheckBox? _selectAllCheckBox;
    private Button? _clearSearchButton;
    private bool _updatingSelectionUi;
    private List<object> _filteredItems = new();

    private int _totalItemCount;
    private int _filteredItemCount;
    private int _pageCount;
    private bool _isEmpty = true;
    private bool _isSelectionEnabled;
    private bool _isMultiSelectionEnabled;
    private object? _selectedItem;
    private double _resolvedColumnsWidth;

    private static readonly ConcurrentDictionary<(Type Type, string Path), PropertyInfo?> SearchPropertyCache = new();

    public VirtualizedItemsGrid()
    {
        _readonlySelectedItems = new ReadOnlyObservableCollection<object>(_selectedItems);
    }

    static VirtualizedItemsGrid()
    {
        ItemsSourceProperty.Changed.AddClassHandler<VirtualizedItemsGrid>((grid, _) => grid.OnItemsSourceChanged());
        SearchTextProperty.Changed.AddClassHandler<VirtualizedItemsGrid>((grid, _) => grid.RefreshView(resetPage: true));
        SearchMemberPathsProperty.Changed.AddClassHandler<VirtualizedItemsGrid>((grid, _) => grid.RefreshView(resetPage: true));
        ShowPaginationProperty.Changed.AddClassHandler<VirtualizedItemsGrid>((grid, _) => grid.RefreshView(resetPage: true));
        PageSizeProperty.Changed.AddClassHandler<VirtualizedItemsGrid>((grid, _) => grid.RefreshView(resetPage: true));
        CurrentPageProperty.Changed.AddClassHandler<VirtualizedItemsGrid>((grid, _) => grid.RefreshView(resetPage: false));
        RowTemplateProperty.Changed.AddClassHandler<VirtualizedItemsGrid>((grid, _) => grid.ConfigureRepeaterTemplate());
        RowMinHeightProperty.Changed.AddClassHandler<VirtualizedItemsGrid>((grid, _) => grid.ConfigureRepeaterTemplate());
        ColumnWidthsProperty.Changed.AddClassHandler<VirtualizedItemsGrid>((grid, _) => grid.UpdateResolvedColumnsWidth());
        ColumnSpacingProperty.Changed.AddClassHandler<VirtualizedItemsGrid>((grid, _) => grid.UpdateResolvedColumnsWidth());
        MinimumStarColumnWidthProperty.Changed.AddClassHandler<VirtualizedItemsGrid>((grid, _) => grid.UpdateResolvedColumnsWidth());
        SelectionModeProperty.Changed.AddClassHandler<VirtualizedItemsGrid>((grid, _) => grid.OnSelectionModeChanged());
        SelectionColumnWidthProperty.Changed.AddClassHandler<VirtualizedItemsGrid>((grid, _) =>
        {
            grid.ConfigureRepeaterTemplate();
            grid.UpdateResolvedColumnsWidth();
        });
    }

    #region Styled properties

    public static readonly StyledProperty<IEnumerable?> ItemsSourceProperty =
        AvaloniaProperty.Register<VirtualizedItemsGrid, IEnumerable?>(nameof(ItemsSource));

    public IEnumerable? ItemsSource
    {
        get => GetValue(ItemsSourceProperty);
        set => SetValue(ItemsSourceProperty, value);
    }

    public static readonly StyledProperty<IDataTemplate?> HeaderTemplateProperty =
        AvaloniaProperty.Register<VirtualizedItemsGrid, IDataTemplate?>(nameof(HeaderTemplate));

    public IDataTemplate? HeaderTemplate
    {
        get => GetValue(HeaderTemplateProperty);
        set => SetValue(HeaderTemplateProperty, value);
    }

    public static readonly StyledProperty<IDataTemplate?> RowTemplateProperty =
        AvaloniaProperty.Register<VirtualizedItemsGrid, IDataTemplate?>(nameof(RowTemplate));

    public IDataTemplate? RowTemplate
    {
        get => GetValue(RowTemplateProperty);
        set => SetValue(RowTemplateProperty, value);
    }

    public static readonly StyledProperty<object?> EmptyViewProperty =
        AvaloniaProperty.Register<VirtualizedItemsGrid, object?>(nameof(EmptyView));

    public object? EmptyView
    {
        get => GetValue(EmptyViewProperty);
        set => SetValue(EmptyViewProperty, value);
    }

    /// <summary>
    /// Comma-separated grid lengths, e.g. "180,120,90,*" or "180,120,2*".
    /// Pixel and star lengths are supported. Auto is intentionally not supported:
    /// independent virtualized rows cannot share an Auto measurement reliably.
    /// </summary>
    public static readonly StyledProperty<string> ColumnWidthsProperty =
        AvaloniaProperty.Register<VirtualizedItemsGrid, string>(nameof(ColumnWidths), string.Empty);

    public string ColumnWidths
    {
        get => GetValue(ColumnWidthsProperty);
        set => SetValue(ColumnWidthsProperty, value);
    }

    public static readonly StyledProperty<double> ColumnSpacingProperty =
        AvaloniaProperty.Register<VirtualizedItemsGrid, double>(nameof(ColumnSpacing), 5d);

    public double ColumnSpacing
    {
        get => GetValue(ColumnSpacingProperty);
        set => SetValue(ColumnSpacingProperty, value);
    }

    /// <summary>
    /// Minimum width assigned to one star-weight unit when the viewport is too
    /// narrow to display all configured columns without horizontal scrolling.
    /// </summary>
    public static readonly StyledProperty<double> MinimumStarColumnWidthProperty =
        AvaloniaProperty.Register<VirtualizedItemsGrid, double>(
            nameof(MinimumStarColumnWidth),
            240d,
            coerce: (_, value) => Math.Max(0d, value));

    public double MinimumStarColumnWidth
    {
        get => GetValue(MinimumStarColumnWidthProperty);
        set => SetValue(MinimumStarColumnWidthProperty, value);
    }

    public static readonly StyledProperty<double> RowMinHeightProperty =
        AvaloniaProperty.Register<VirtualizedItemsGrid, double>(nameof(RowMinHeight), 32d);

    public double RowMinHeight
    {
        get => GetValue(RowMinHeightProperty);
        set => SetValue(RowMinHeightProperty, value);
    }

    public static readonly StyledProperty<bool> ShowHeaderProperty =
        AvaloniaProperty.Register<VirtualizedItemsGrid, bool>(nameof(ShowHeader), true);

    public bool ShowHeader
    {
        get => GetValue(ShowHeaderProperty);
        set => SetValue(ShowHeaderProperty, value);
    }

    public static readonly StyledProperty<bool> ShowSearchBarProperty =
        AvaloniaProperty.Register<VirtualizedItemsGrid, bool>(nameof(ShowSearchBar), false);

    public bool ShowSearchBar
    {
        get => GetValue(ShowSearchBarProperty);
        set => SetValue(ShowSearchBarProperty, value);
    }

    public static readonly StyledProperty<string?> SearchTextProperty =
        AvaloniaProperty.Register<VirtualizedItemsGrid, string?>(nameof(SearchText));

    public string? SearchText
    {
        get => GetValue(SearchTextProperty);
        set => SetValue(SearchTextProperty, value);
    }

    public static readonly StyledProperty<string> SearchWatermarkProperty =
        AvaloniaProperty.Register<VirtualizedItemsGrid, string>(nameof(SearchWatermark), "Search…");

    public string SearchWatermark
    {
        get => GetValue(SearchWatermarkProperty);
        set => SetValue(SearchWatermarkProperty, value);
    }

    /// <summary>
    /// Comma-separated property paths searched case-insensitively.
    /// Example: "Name,DisplayName,Description,Value".
    /// If empty, item.ToString() is searched.
    /// </summary>
    public static readonly StyledProperty<string> SearchMemberPathsProperty =
        AvaloniaProperty.Register<VirtualizedItemsGrid, string>(nameof(SearchMemberPaths), string.Empty);

    public string SearchMemberPaths
    {
        get => GetValue(SearchMemberPathsProperty);
        set => SetValue(SearchMemberPathsProperty, value);
    }

    public static readonly StyledProperty<bool> ShowPaginationProperty =
        AvaloniaProperty.Register<VirtualizedItemsGrid, bool>(nameof(ShowPagination), false);

    public bool ShowPagination
    {
        get => GetValue(ShowPaginationProperty);
        set => SetValue(ShowPaginationProperty, value);
    }

    public static readonly StyledProperty<int> PageSizeProperty =
        AvaloniaProperty.Register<VirtualizedItemsGrid, int>(nameof(PageSize), 100, coerce: (_, value) => Math.Max(1, value));

    public int PageSize
    {
        get => GetValue(PageSizeProperty);
        set => SetValue(PageSizeProperty, value);
    }

    /// <summary>One-based current page.</summary>
    public static readonly StyledProperty<int> CurrentPageProperty =
        AvaloniaProperty.Register<VirtualizedItemsGrid, int>(nameof(CurrentPage), 1, coerce: (_, value) => Math.Max(1, value));

    public int CurrentPage
    {
        get => GetValue(CurrentPageProperty);
        set => SetValue(CurrentPageProperty, value);
    }

    public static readonly StyledProperty<VirtualizedItemsGridSelectionMode> SelectionModeProperty =
        AvaloniaProperty.Register<VirtualizedItemsGrid, VirtualizedItemsGridSelectionMode>(
            nameof(SelectionMode), VirtualizedItemsGridSelectionMode.None);

    public VirtualizedItemsGridSelectionMode SelectionMode
    {
        get => GetValue(SelectionModeProperty);
        set => SetValue(SelectionModeProperty, value);
    }

    public static readonly StyledProperty<VirtualizedItemsGridSelectAllScope> SelectAllScopeProperty =
        AvaloniaProperty.Register<VirtualizedItemsGrid, VirtualizedItemsGridSelectAllScope>(
            nameof(SelectAllScope), VirtualizedItemsGridSelectAllScope.CurrentPage);

    public VirtualizedItemsGridSelectAllScope SelectAllScope
    {
        get => GetValue(SelectAllScopeProperty);
        set => SetValue(SelectAllScopeProperty, value);
    }

    public static readonly StyledProperty<double> SelectionColumnWidthProperty =
        AvaloniaProperty.Register<VirtualizedItemsGrid, double>(nameof(SelectionColumnWidth), 36d);

    public double SelectionColumnWidth
    {
        get => GetValue(SelectionColumnWidthProperty);
        set => SetValue(SelectionColumnWidthProperty, value);
    }

    #endregion

    #region Read-only direct properties

    public static readonly DirectProperty<VirtualizedItemsGrid, int> TotalItemCountProperty =
        AvaloniaProperty.RegisterDirect<VirtualizedItemsGrid, int>(nameof(TotalItemCount), grid => grid.TotalItemCount);

    public int TotalItemCount
    {
        get => _totalItemCount;
        private set => SetAndRaise(TotalItemCountProperty, ref _totalItemCount, value);
    }

    public static readonly DirectProperty<VirtualizedItemsGrid, int> FilteredItemCountProperty =
        AvaloniaProperty.RegisterDirect<VirtualizedItemsGrid, int>(nameof(FilteredItemCount), grid => grid.FilteredItemCount);

    public int FilteredItemCount
    {
        get => _filteredItemCount;
        private set => SetAndRaise(FilteredItemCountProperty, ref _filteredItemCount, value);
    }

    public static readonly DirectProperty<VirtualizedItemsGrid, int> PageCountProperty =
        AvaloniaProperty.RegisterDirect<VirtualizedItemsGrid, int>(nameof(PageCount), grid => grid.PageCount);

    public int PageCount
    {
        get => _pageCount;
        private set => SetAndRaise(PageCountProperty, ref _pageCount, value);
    }

    public static readonly DirectProperty<VirtualizedItemsGrid, bool> IsEmptyProperty =
        AvaloniaProperty.RegisterDirect<VirtualizedItemsGrid, bool>(nameof(IsEmpty), grid => grid.IsEmpty);

    public bool IsEmpty
    {
        get => _isEmpty;
        private set => SetAndRaise(IsEmptyProperty, ref _isEmpty, value);
    }

    public static readonly DirectProperty<VirtualizedItemsGrid, bool> IsSelectionEnabledProperty =
        AvaloniaProperty.RegisterDirect<VirtualizedItemsGrid, bool>(nameof(IsSelectionEnabled), grid => grid.IsSelectionEnabled);

    public bool IsSelectionEnabled
    {
        get => _isSelectionEnabled;
        private set => SetAndRaise(IsSelectionEnabledProperty, ref _isSelectionEnabled, value);
    }

    public static readonly DirectProperty<VirtualizedItemsGrid, bool> IsMultiSelectionEnabledProperty =
        AvaloniaProperty.RegisterDirect<VirtualizedItemsGrid, bool>(nameof(IsMultiSelectionEnabled), grid => grid.IsMultiSelectionEnabled);

    public bool IsMultiSelectionEnabled
    {
        get => _isMultiSelectionEnabled;
        private set => SetAndRaise(IsMultiSelectionEnabledProperty, ref _isMultiSelectionEnabled, value);
    }

    /// <summary>
    /// Resolved finite width shared by the header row and every realized data row.
    /// This makes star columns resolve against exactly the same available width and
    /// prevents header/body drift when ItemsRepeater realizes data.
    /// </summary>
    public static readonly DirectProperty<VirtualizedItemsGrid, double> ResolvedColumnsWidthProperty =
        AvaloniaProperty.RegisterDirect<VirtualizedItemsGrid, double>(
            nameof(ResolvedColumnsWidth),
            grid => grid.ResolvedColumnsWidth);

    public double ResolvedColumnsWidth
    {
        get => _resolvedColumnsWidth;
        private set => SetAndRaise(ResolvedColumnsWidthProperty, ref _resolvedColumnsWidth, value);
    }

    public ReadOnlyObservableCollection<object> SelectedItems => _readonlySelectedItems;

    public static readonly DirectProperty<VirtualizedItemsGrid, object?> SelectedItemProperty =
        AvaloniaProperty.RegisterDirect<VirtualizedItemsGrid, object?>(nameof(SelectedItem), grid => grid.SelectedItem);

    public object? SelectedItem
    {
        get => _selectedItem;
        private set => SetAndRaise(SelectedItemProperty, ref _selectedItem, value);
    }

    #endregion

    /// <summary>
    /// Optional search override. Return true when item matches searchText.
    /// If null, SearchMemberPaths (or ToString) is used.
    /// </summary>
    public Func<object, string, bool>? SearchFilter { get; set; }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        DetachTemplatePartHandlers();
        base.OnApplyTemplate(e);

        _repeater = e.NameScope.Find<ItemsRepeater>("PART_Repeater");
        _headerScrollViewer = e.NameScope.Find<ScrollViewer>("PART_HeaderScrollViewer");
        _bodyScrollViewer = e.NameScope.Find<ScrollViewer>("PART_BodyScrollViewer");
        _firstPageButton = e.NameScope.Find<Button>("PART_FirstPageButton");
        _previousPageButton = e.NameScope.Find<Button>("PART_PreviousPageButton");
        _nextPageButton = e.NameScope.Find<Button>("PART_NextPageButton");
        _lastPageButton = e.NameScope.Find<Button>("PART_LastPageButton");
        _selectAllCheckBox = e.NameScope.Find<CheckBox>("PART_SelectAllCheckBox");
        _clearSearchButton = e.NameScope.Find<Button>("PART_ClearSearchButton");

        AttachTemplatePartHandlers();
        ConfigureRepeaterTemplate();
        RefreshView(resetPage: false);
        UpdateSelectionModeState();
        UpdateSelectionUi();
        UpdateResolvedColumnsWidth();
    }

    public void Refresh() => RefreshView(resetPage: false);

    public void ClearSelection()
    {
        if (_selectedItems.Count == 0)
        {
            return;
        }

        _selectedItems.Clear();
        SynchronizeSelectedItem();
        UpdateSelectionUi();
    }

    public bool IsSelected(object item) => _selectedItems.Contains(item);

    public void Select(object item)
    {
        if (SelectionMode == VirtualizedItemsGridSelectionMode.None)
        {
            return;
        }

        if (SelectionMode == VirtualizedItemsGridSelectionMode.Single)
        {
            if (_selectedItems.Count == 1 && Equals(_selectedItems[0], item))
            {
                return;
            }

            _selectedItems.Clear();
            _selectedItems.Add(item);
        }
        else if (!_selectedItems.Contains(item))
        {
            _selectedItems.Add(item);
        }

        SynchronizeSelectedItem();
        UpdateSelectionUi();
    }

    public void Deselect(object item)
    {
        if (_selectedItems.Remove(item))
        {
            SynchronizeSelectedItem();
            UpdateSelectionUi();
        }
    }

    internal IReadOnlyList<GridLength> GetParsedColumnWidths()
    {
        if (string.IsNullOrWhiteSpace(ColumnWidths))
        {
            return Array.Empty<GridLength>();
        }

        return ColumnWidths
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(ParseGridLength)
            .ToArray();
    }

    private static GridLength ParseGridLength(string token)
    {
        if (token.Equals("Auto", StringComparison.OrdinalIgnoreCase))
        {
            throw new NotSupportedException(
                "VirtualizedItemsGrid ColumnWidths does not support Auto. " +
                "Use an explicit pixel width or a star width so header and virtualized rows share deterministic geometry.");
        }

        if (token.EndsWith('*'))
        {
            var factorText = token[..^1].Trim();
            var factor = string.IsNullOrWhiteSpace(factorText)
                ? 1d
                : double.Parse(factorText, NumberStyles.Float, CultureInfo.InvariantCulture);
            return new GridLength(factor, GridUnitType.Star);
        }

        var pixels = double.Parse(token, NumberStyles.Float, CultureInfo.InvariantCulture);
        return new GridLength(pixels, GridUnitType.Pixel);
    }

    private void OnItemsSourceChanged()
    {
        if (_observableSource is not null)
        {
            _observableSource.CollectionChanged -= OnSourceCollectionChanged;
        }

        _observableSource = ItemsSource as INotifyCollectionChanged;
        if (_observableSource is not null)
        {
            _observableSource.CollectionChanged += OnSourceCollectionChanged;
        }

        // Drop selections for items no longer present.
        var current = ItemsSource?.Cast<object>().ToHashSet() ?? new HashSet<object>();
        for (var i = _selectedItems.Count - 1; i >= 0; --i)
        {
            if (!current.Contains(_selectedItems[i]))
            {
                _selectedItems.RemoveAt(i);
            }
        }

        SynchronizeSelectedItem();
        RefreshView(resetPage: true);
    }

    private void OnSourceCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        => RefreshView(resetPage: false);

    private void OnSelectionModeChanged()
    {
        if (SelectionMode == VirtualizedItemsGridSelectionMode.None)
        {
            _selectedItems.Clear();
        }
        else if (SelectionMode == VirtualizedItemsGridSelectionMode.Single && _selectedItems.Count > 1)
        {
            var first = _selectedItems[0];
            _selectedItems.Clear();
            _selectedItems.Add(first);
        }

        SynchronizeSelectedItem();
        UpdateSelectionModeState();
        ConfigureRepeaterTemplate();
        UpdateSelectionUi();
        UpdateResolvedColumnsWidth();
    }

    private void UpdateSelectionModeState()
    {
        IsSelectionEnabled = SelectionMode != VirtualizedItemsGridSelectionMode.None;
        IsMultiSelectionEnabled = SelectionMode == VirtualizedItemsGridSelectionMode.Multiple;
    }

    private void RefreshView(bool resetPage)
    {
        var allItems = ItemsSource?.Cast<object>().ToList() ?? new List<object>();
        TotalItemCount = allItems.Count;

        var search = SearchText?.Trim();
        if (string.IsNullOrEmpty(search))
        {
            _filteredItems = allItems;
        }
        else
        {
            _filteredItems = allItems.Where(item => MatchesSearch(item, search)).ToList();
        }

        FilteredItemCount = _filteredItems.Count;
        PageCount = ShowPagination && FilteredItemCount > 0
            ? (int)Math.Ceiling(FilteredItemCount / (double)PageSize)
            : FilteredItemCount > 0 ? 1 : 0;

        if (resetPage)
        {
            SetCurrentValue(CurrentPageProperty, 1);
        }

        var effectivePage = PageCount == 0 ? 1 : Math.Clamp(CurrentPage, 1, PageCount);
        if (effectivePage != CurrentPage)
        {
            SetCurrentValue(CurrentPageProperty, effectivePage);
        }

        IEnumerable<object> pageItems = _filteredItems;
        if (ShowPagination && PageCount > 0)
        {
            pageItems = _filteredItems
                .Skip((effectivePage - 1) * PageSize)
                .Take(PageSize);
        }

        _viewItems.Clear();
        foreach (var item in pageItems)
        {
            _viewItems.Add(item);
        }

        IsEmpty = _viewItems.Count == 0;

        if (_repeater is not null && !ReferenceEquals(_repeater.ItemsSource, _viewItems))
        {
            _repeater.ItemsSource = _viewItems;
        }

        if (_bodyScrollViewer is not null)
        {
            _bodyScrollViewer.Offset = new Vector(_bodyScrollViewer.Offset.X, 0);
        }

        UpdatePaginationButtons();
        UpdateSelectionUi();
    }

    private bool MatchesSearch(object item, string searchText)
    {
        if (SearchFilter is not null)
        {
            return SearchFilter(item, searchText);
        }

        var paths = SearchMemberPaths
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        if (paths.Length == 0)
        {
            return Contains(item.ToString(), searchText);
        }

        foreach (var path in paths)
        {
            var value = ReadPropertyPath(item, path);
            if (Contains(value?.ToString(), searchText))
            {
                return true;
            }
        }

        return false;
    }

    private static bool Contains(string? value, string searchText)
        => value?.Contains(searchText, StringComparison.CurrentCultureIgnoreCase) == true;

    private static object? ReadPropertyPath(object? instance, string path)
    {
        if (instance is null)
        {
            return null;
        }

        object? current = instance;
        foreach (var segment in path.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (current is null)
            {
                return null;
            }

            var type = current.GetType();
            var property = SearchPropertyCache.GetOrAdd(
                (type, segment),
                static key => key.Type.GetProperty(
                    key.Path,
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.IgnoreCase));

            if (property is null)
            {
                return null;
            }

            current = property.GetValue(current);
        }

        return current;
    }

    private void ConfigureRepeaterTemplate()
    {
        if (_repeater is null)
        {
            return;
        }

        _selectionCheckBoxes.Clear();
        _repeater.ItemsSource = _viewItems;
        _repeater.ItemTemplate = new FuncDataTemplate<object>((item, _) => BuildRowContainer(item));
    }

    private Control BuildRowContainer(object item)
    {
        var outer = new Grid
        {
            MinHeight = RowMinHeight,
            HorizontalAlignment = HorizontalAlignment.Left
        };
        outer.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
        outer.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(1, GridUnitType.Star)));

        if (SelectionMode != VirtualizedItemsGridSelectionMode.None)
        {
            var checkBox = new CheckBox
            {
                Width = SelectionColumnWidth,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                IsChecked = _selectedItems.Contains(item)
            };

            checkBox.Click += (_, _) => OnRowSelectionClicked(item, checkBox);
            _selectionCheckBoxes.Add(new WeakReference<CheckBox>(checkBox));
            Grid.SetColumn(checkBox, 0);
            outer.Children.Add(checkBox);
        }

        var content = new ContentPresenter
        {
            Content = item,
            ContentTemplate = RowTemplate,
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Stretch
        };
        Grid.SetColumn(content, 1);
        outer.Children.Add(content);

        return outer;
    }

    private void OnRowSelectionClicked(object item, CheckBox checkBox)
    {
        if (_updatingSelectionUi)
        {
            return;
        }

        if (checkBox.IsChecked == true)
        {
            Select(item);
        }
        else
        {
            Deselect(item);
        }
    }

    private void UpdateSelectionUi()
    {
        _updatingSelectionUi = true;
        try
        {
            for (var i = _selectionCheckBoxes.Count - 1; i >= 0; --i)
            {
                if (!_selectionCheckBoxes[i].TryGetTarget(out var checkBox))
                {
                    _selectionCheckBoxes.RemoveAt(i);
                    continue;
                }

                var row = checkBox.Parent as Grid;
                var presenter = row?.Children.OfType<ContentPresenter>().FirstOrDefault();
                if (presenter?.Content is { } item)
                {
                    checkBox.IsChecked = _selectedItems.Contains(item);
                }
            }

            if (_selectAllCheckBox is not null)
            {
                var scope = GetSelectAllScopeItems();
                var selectedCount = scope.Count(item => _selectedItems.Contains(item));
                _selectAllCheckBox.IsChecked = scope.Count == 0
                    ? false
                    : selectedCount == 0
                        ? false
                        : selectedCount == scope.Count
                            ? true
                            : null;
            }
        }
        finally
        {
            _updatingSelectionUi = false;
        }
    }

    private IReadOnlyList<object> GetSelectAllScopeItems()
        => SelectAllScope == VirtualizedItemsGridSelectAllScope.FilteredItems
            ? _filteredItems
            : _viewItems;

    private void OnSelectAllClicked(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (_updatingSelectionUi || SelectionMode != VirtualizedItemsGridSelectionMode.Multiple)
        {
            return;
        }

        var scope = GetSelectAllScopeItems();
        var select = _selectAllCheckBox?.IsChecked == true;

        foreach (var item in scope)
        {
            if (select)
            {
                if (!_selectedItems.Contains(item))
                {
                    _selectedItems.Add(item);
                }
            }
            else
            {
                _selectedItems.Remove(item);
            }
        }

        SynchronizeSelectedItem();
        UpdateSelectionUi();
    }

    private void SynchronizeSelectedItem()
        => SelectedItem = _selectedItems.Count == 0 ? null : _selectedItems[0];

    private void UpdateResolvedColumnsWidth()
    {
        var columns = GetParsedColumnWidths();
        if (columns.Count == 0)
        {
            ResolvedColumnsWidth = 0d;
            return;
        }

        var pixelWidth = 0d;
        var starWeight = 0d;

        foreach (var column in columns)
        {
            switch (column.GridUnitType)
            {
                case GridUnitType.Pixel:
                    pixelWidth += column.Value;
                    break;

                case GridUnitType.Star:
                    starWeight += column.Value;
                    break;

                case GridUnitType.Auto:
                    // ParseGridLength rejects Auto. Keep this guard here in case
                    // a future parser implementation changes that contract.
                    throw new NotSupportedException(
                        "VirtualizedItemsGrid does not support Auto column widths.");
            }
        }

        var spacingWidth = Math.Max(0, columns.Count - 1) * ColumnSpacing;
        var naturalWidth = pixelWidth + spacingWidth;

        if (starWeight > 0d)
        {
            naturalWidth += MinimumStarColumnWidth * starWeight;
        }

        // ScrollViewer.Viewport is the authoritative width after layout and
        // excludes the vertical scrollbar. Bounds is a useful first-layout fallback.
        var viewportWidth = _bodyScrollViewer?.Viewport.Width ?? 0d;
        if (!double.IsFinite(viewportWidth) || viewportWidth <= 0d)
        {
            viewportWidth = _bodyScrollViewer?.Bounds.Width ?? Bounds.Width;
        }

        if (!double.IsFinite(viewportWidth) || viewportWidth < 0d)
        {
            viewportWidth = 0d;
        }

        var selectionWidth = IsSelectionEnabled ? SelectionColumnWidth : 0d;
        var availableColumnsWidth = Math.Max(0d, viewportWidth - selectionWidth);

        // Pixel-only tables retain their natural width. A table containing star
        // columns fills the viewport, but never shrinks below its configured
        // minimum star width; horizontal scrolling then takes over.
        var resolvedWidth = starWeight > 0d
            ? Math.Max(naturalWidth, availableColumnsWidth)
            : naturalWidth;

        // Avoid churning layout for sub-pixel viewport fluctuations.
        if (Math.Abs(ResolvedColumnsWidth - resolvedWidth) > 0.5d)
        {
            ResolvedColumnsWidth = resolvedWidth;
        }
    }

    private void AttachTemplatePartHandlers()
    {
        if (_bodyScrollViewer is not null)
        {
            _bodyScrollViewer.ScrollChanged += BodyScrollViewerOnScrollChanged;
            _bodyScrollViewer.SizeChanged += BodyScrollViewerOnSizeChanged;
        }

        if (_firstPageButton is not null) _firstPageButton.Click += FirstPageButtonOnClick;
        if (_previousPageButton is not null) _previousPageButton.Click += PreviousPageButtonOnClick;
        if (_nextPageButton is not null) _nextPageButton.Click += NextPageButtonOnClick;
        if (_lastPageButton is not null) _lastPageButton.Click += LastPageButtonOnClick;
        if (_selectAllCheckBox is not null) _selectAllCheckBox.Click += OnSelectAllClicked;
        if (_clearSearchButton is not null) _clearSearchButton.Click += ClearSearchButtonOnClick;
    }

    private void DetachTemplatePartHandlers()
    {
        if (_bodyScrollViewer is not null)
        {
            _bodyScrollViewer.ScrollChanged -= BodyScrollViewerOnScrollChanged;
            _bodyScrollViewer.SizeChanged -= BodyScrollViewerOnSizeChanged;
        }

        if (_firstPageButton is not null) _firstPageButton.Click -= FirstPageButtonOnClick;
        if (_previousPageButton is not null) _previousPageButton.Click -= PreviousPageButtonOnClick;
        if (_nextPageButton is not null) _nextPageButton.Click -= NextPageButtonOnClick;
        if (_lastPageButton is not null) _lastPageButton.Click -= LastPageButtonOnClick;
        if (_selectAllCheckBox is not null) _selectAllCheckBox.Click -= OnSelectAllClicked;
        if (_clearSearchButton is not null) _clearSearchButton.Click -= ClearSearchButtonOnClick;
    }

    private void BodyScrollViewerOnScrollChanged(object? sender, ScrollChangedEventArgs e)
    {
        UpdateResolvedColumnsWidth();

        if (_bodyScrollViewer is null || _headerScrollViewer is null)
        {
            return;
        }

        _headerScrollViewer.Offset = new Vector(_bodyScrollViewer.Offset.X, 0);
    }

    private void BodyScrollViewerOnSizeChanged(object? sender, SizeChangedEventArgs e)
        => UpdateResolvedColumnsWidth();

    private void ClearSearchButtonOnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        => SearchText = null;

    private void FirstPageButtonOnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        => CurrentPage = 1;

    private void PreviousPageButtonOnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        => CurrentPage = Math.Max(1, CurrentPage - 1);

    private void NextPageButtonOnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        => CurrentPage = PageCount == 0 ? 1 : Math.Min(PageCount, CurrentPage + 1);

    private void LastPageButtonOnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        => CurrentPage = Math.Max(1, PageCount);

    private void UpdatePaginationButtons()
    {
        if (_firstPageButton is not null) _firstPageButton.IsEnabled = CurrentPage > 1;
        if (_previousPageButton is not null) _previousPageButton.IsEnabled = CurrentPage > 1;
        if (_nextPageButton is not null) _nextPageButton.IsEnabled = PageCount > 0 && CurrentPage < PageCount;
        if (_lastPageButton is not null) _lastPageButton.IsEnabled = PageCount > 0 && CurrentPage < PageCount;
    }


}
