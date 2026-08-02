using System.Collections;
using System.Collections.Concurrent;
using System.Reflection;

namespace UraniumUI.Material.VirtualizedDataGrid.Controls;

/// <summary>
/// Internal filtered/paged view and pager implementation for <see cref="VirtualizedDataGrid"/>.
/// </summary>
public partial class VirtualizedDataGrid
{
    private static readonly ConcurrentDictionary<(Type ItemType, string Path), Func<object, object?>> filterAccessorCache = new();

    private Command clearSearchCommand = null!;
    private Command firstPageCommand = null!;
    private Command previousPageCommand = null!;
    private Command nextPageCommand = null!;
    private Command lastPageCommand = null!;
    private Command<int> goToPageCommand = null!;

    private bool updatingDataView;
    private bool dataViewRefreshPending;
    private bool updatingPagingProperties;

    private void InitializeDataView()
    {
        clearSearchCommand = new Command(
            () => FilterText = string.Empty,
            () => HasSearchText);

        ClearSearchCommand = clearSearchCommand;

        firstPageCommand = new Command(
            () => GoToPage(1),
            () => HasPreviousPage);

        previousPageCommand = new Command(
            () => GoToPage(CurrentPage - 1),
            () => HasPreviousPage);

        nextPageCommand = new Command(
            () => GoToPage(CurrentPage + 1),
            () => HasNextPage);

        lastPageCommand = new Command(
            () => GoToPage(TotalPageCount),
            () => HasNextPage);

        goToPageCommand = new Command<int>(GoToPage);

        FirstPageCommand = firstPageCommand;
        PreviousPageCommand = previousPageCommand;
        NextPageCommand = nextPageCommand;
        LastPageCommand = lastPageCommand;
        GoToPageCommand = goToPageCommand;
    }

    /// <summary>
    /// Re-evaluates filtering and paging. Call this when a filter depends on row properties
    /// that changed without changing <see cref="FilterText"/> or <see cref="ItemsSource"/>.
    /// </summary>
    public void RefreshView()
    {
        RefreshDataView(false);
    }

    /// <summary>
    /// Navigates to a one-based page number. Values outside the valid range are clamped.
    /// </summary>
    public void GoToPage(int page)
    {
        var maximumPage = Math.Max(1, TotalPageCount);
        CurrentPage = Math.Clamp(page, 1, maximumPage);
    }

    private void OnFilterSettingsChanged()
    {
        RefreshDataView(ResetPageOnFilterChange);
    }

    private void OnPagingSettingsChanged(bool resetCurrentPage)
    {
        if (updatingPagingProperties)
        {
            return;
        }

        RefreshDataView(resetCurrentPage);
    }

    private void OnCurrentPageChanged()
    {
        if (updatingPagingProperties)
        {
            return;
        }

        RefreshDataView(false);

        if (ScrollToTopOnPageChange)
        {
            ScrollCurrentPageToTop();
        }
    }

    private void RefreshDataView(bool resetCurrentPage)
    {
        if (visualResourcesReleased)
        {
            return;
        }

        if (updatingDataView)
        {
            dataViewRefreshPending = true;
            return;
        }

        updatingDataView = true;
        var diagnosticsStarted = Diagnostics.StartTiming();

        try
        {
            var source = deferRefreshCount > 0 ? deferredSnapshot : ItemsSource;

            var totalItemCount = source?.Count ?? 0;
            var filteringActive = IsFilteringActive();

            List<object>? filteredItems = null;
            var filteredItemCount = totalItemCount;

            if (filteringActive)
            {
                filteredItems = source?
                                    .Cast<object?>()
                                    .Where(item => item is not null)
                                    .Cast<object>()
                                    .Where(MatchesCurrentFilter)
                                    .ToList()
                                ?? [];

                filteredItemCount = filteredItems.Count;
            }

            var pageSize = Math.Max(1, PageSize);
            var totalPageCount = EnablePaging && filteredItemCount > 0
                ? (int)Math.Ceiling(filteredItemCount / (double)pageSize)
                : filteredItemCount > 0
                    ? 1
                    : 0;

            var requestedPage = resetCurrentPage
                ? 1
                : CurrentPage;

            var currentPage = totalPageCount == 0
                ? 1
                : Math.Clamp(requestedPage, 1, totalPageCount);

            SetCurrentPageFromView(currentPage);

            IList? displayedItems;

            if (source is null)
            {
                displayedItems = null;
            }
            else if (EnablePaging)
            {
                var pageSource = filteredItems
                                 ?? source
                                     .Cast<object?>()
                                     .Where(item => item is not null)
                                     .Cast<object>()
                                     .ToList();

                displayedItems = pageSource
                    .Skip((currentPage - 1) * pageSize)
                    .Take(pageSize)
                    .ToList();
            }
            else if (filteringActive)
            {
                displayedItems = filteredItems;
            }
            else
            {
                // Keep the original observable source when no view transformation is active.
                // This preserves incremental CollectionView updates without rebuilding a list.
                displayedItems = source;
            }

            var pageItemCount = displayedItems?.Count ?? 0;
            var isEmpty = pageItemCount == 0;

            SetValue(HasSearchTextPropertyKey, !string.IsNullOrWhiteSpace(FilterText));
            SetValue(TotalItemCountPropertyKey, totalItemCount);
            SetValue(FilteredItemCountPropertyKey, filteredItemCount);
            SetValue(PageItemCountPropertyKey, pageItemCount);
            SetValue(TotalPageCountPropertyKey, totalPageCount);
            SetValue(HasPreviousPagePropertyKey, EnablePaging && totalPageCount > 0 && currentPage > 1);
            SetValue(HasNextPagePropertyKey, EnablePaging && totalPageCount > 0 && currentPage < totalPageCount);
            SetValue(IsEmptyPropertyKey, isEmpty);
            SetValue(HasItemsPropertyKey, !isEmpty);

            // Empty/null is a release boundary. Drop the realized cell trees
            // before detaching the native source so navigation and ViewModel
            // disposal do not trigger another recycle/measurement pass.
            if (!ReadyToRender || isEmpty)
            {
                ReleaseRealizedRows();
                SetRowsItemsSource(null);
            }
            else
            {
                SetRowsItemsSource(displayedItems);
            }

            UpdateEmptyViewVisibility();
            UpdateSearchBarVisibility();
            UpdatePagerVisibility();
            RaiseSearchCanExecuteChanged();
            RaisePagingCanExecuteChanged();
        }
        finally
        {
            updatingDataView = false;
            Diagnostics.RecordDataViewRefresh(diagnosticsStarted);
        }

        if (dataViewRefreshPending)
        {
            dataViewRefreshPending = false;
            RefreshDataView(false);
        }
    }

    private void SetCurrentPageFromView(int page)
    {
        if (CurrentPage == page)
        {
            return;
        }

        updatingPagingProperties = true;

        try
        {
            SetValue(CurrentPageProperty, page);
        }
        finally
        {
            updatingPagingProperties = false;
        }
    }

    private bool IsFilteringActive()
    {
        return FilterPredicate is not null || !string.IsNullOrWhiteSpace(FilterText);
    }

    private bool MatchesCurrentFilter(object item)
    {
        if (FilterPredicate is not null && !FilterPredicate(item))
        {
            return false;
        }

        var filterText = FilterText?.Trim();

        if (string.IsNullOrEmpty(filterText))
        {
            return true;
        }

        var paths = ParseFilterMemberPaths();

        if (paths.Count == 0)
        {
            return item.ToString()?.Contains(filterText, FilterStringComparison) == true;
        }

        foreach (var path in paths)
        {
            var accessor = filterAccessorCache.GetOrAdd(
                (item.GetType(), path),
                static key => CreatePropertyPathAccessor(key.ItemType, key.Path));

            var value = accessor(item);

            if (value?.ToString()?.Contains(filterText, FilterStringComparison) == true)
            {
                return true;
            }
        }

        return false;
    }

    private IReadOnlyList<string> ParseFilterMemberPaths()
    {
        return string.IsNullOrWhiteSpace(FilterMemberPaths)
            ? Array.Empty<string>()
            : FilterMemberPaths
                .Split([',', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Distinct(StringComparer.Ordinal)
                .ToArray();
    }

    private static Func<object, object?> CreatePropertyPathAccessor(Type itemType, string path)
    {
        var parts = path.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var properties = new List<PropertyInfo>();
        var currentType = itemType;

        foreach (var part in parts)
        {
            var property = currentType.GetProperty(
                part,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.IgnoreCase);

            if (property is null)
            {
                return _ => null;
            }

            properties.Add(property);
            currentType = Nullable.GetUnderlyingType(property.PropertyType) ?? property.PropertyType;
        }

        return item =>
        {
            var current = item;

            foreach (var property in properties)
            {
                if (current is null)
                {
                    return null;
                }

                current = property.GetValue(current);
            }

            return current;
        };
    }


    /// <summary>
    /// Applies the configured search view or search template.
    /// </summary>
    internal void ApplySearchBar()
    {
        if (visualResourcesReleased)
        {
            return;
        }

        if (!ShowSearchBar)
        {
            searchHost.IsVisible = false;
            searchHost.Content = null;
            return;
        }

        View searchContent;

        if (SearchView is not null)
        {
            // A directly supplied view retains/inherits the page BindingContext.
            searchContent = SearchView;
        }
        else if (SearchTemplate?.CreateContent() is View templateContent)
        {
            // Match PagerTemplate: the template binds directly to grid properties.
            // Application-specific values are available through BindingContext.
            templateContent.BindingContext = this;
            searchContent = templateContent;
        }
        else
        {
            searchContent = CreateDefaultSearchView();
        }

        if (!ReferenceEquals(searchHost.Content, searchContent))
        {
            searchHost.Content = searchContent;
        }

        UpdateSearchBarVisibility();
    }

    /// <summary>
    /// Updates search visibility independently of row count. In particular, filtering
    /// to zero rows must not hide the search UI that is needed to clear the filter.
    /// </summary>
    internal void UpdateSearchBarVisibility()
    {
        if (visualResourcesReleased)
        {
            return;
        }

        if (!ShowSearchBar)
        {
            searchHost.IsVisible = false;
            return;
        }

        if (ReadyToRender && HasItems)
        {
            searchHost.IsVisible = true;
            return;
        }

        if (ReadyToRender && HasSearchText)
        {
            searchHost.IsVisible = true;
            return;
        }

        searchHost.IsVisible = false;
    }


    private View CreateDefaultSearchView()
    {
        var label = new Label { Text = "Search:", FontSize = 14, VerticalOptions = LayoutOptions.Center };

        var entry = new Entry { FontSize = 14, VerticalOptions = LayoutOptions.Center, ClearButtonVisibility = ClearButtonVisibility.WhileEditing };
        entry.SetBinding(Entry.TextProperty, new Binding(nameof(FilterText), source: this, mode: BindingMode.TwoWay));
        entry.SetBinding(Entry.PlaceholderProperty, new Binding(nameof(SearchPlaceholder), source: this));

        var clearButton = new Button
        {
            Text = "Clear",
            FontSize = 12,
            Padding = new Thickness(8, 4),
            BackgroundColor = Colors.Transparent,
            VerticalOptions = LayoutOptions.Center,
            Command = ClearSearchCommand
        };
        clearButton.SetBinding(
            IsVisibleProperty,
            new Binding(nameof(HasSearchText), source: this));

        var matchingLabel = new Label { FontSize = 14, VerticalOptions = LayoutOptions.Center };
        matchingLabel.SetBinding(
            Label.TextProperty,
            new Binding(
                nameof(FilteredItemCount),
                source: this,
                stringFormat: "Matching: {0}"));

        var totalLabel = new Label { FontSize = 14, VerticalOptions = LayoutOptions.Center };
        totalLabel.SetBinding(
            Label.TextProperty,
            new Binding(
                nameof(TotalItemCount),
                source: this,
                stringFormat: "Total: {0}"));

        var searchGrid = new Grid
        {
            Padding = new Thickness(18, 4),
            Margin = new Thickness(0, 10),
            ColumnSpacing = 12,
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Auto), new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto), new ColumnDefinition(GridLength.Auto)
                //new ColumnDefinition(GridLength.Auto)
            }
        };

        searchGrid.Add(label, 0, 0);
        searchGrid.Add(entry, 1, 0);
        //  searchGrid.Add(clearButton, 2, 0);
        searchGrid.Add(matchingLabel, 2, 0);
        searchGrid.Add(totalLabel, 3, 0);

        return searchGrid;
    }

    internal void ApplyPager()
    {
        if (visualResourcesReleased)
        {
            return;
        }

        // UraniumUI's Paginator is an interactive control and expects a MAUI dispatcher.
        // Do not construct a hidden paginator during grid initialization (or in headless
        // consumers); create it only when the pager has actually been requested.
        if (!ShowPager)
        {
            pagerHost.IsVisible = false;
            pagerHost.Content = null;
            return;
        }

        View pagerContent;

        if (PagerView is not null)
        {
            pagerContent = PagerView;
        }
        else if (PagerTemplate?.CreateContent() is View templateContent)
        {
            templateContent.BindingContext = this;
            pagerContent = templateContent;
        }
        else
        {
            pagerContent = CreateDefaultPagerView();
        }

        if (!ReferenceEquals(pagerHost.Content, pagerContent))
        {
            pagerHost.Content = pagerContent;
        }

        UpdatePagerVisibility();
    }

    internal void UpdatePagerVisibility()
    {
        if (visualResourcesReleased)
        {
            return;
        }

        pagerHost.IsVisible =
            ShowPager &&
            EnablePaging &&
            TotalItemCount > 0 &&
            (ShowPagerWhenSinglePage || TotalPageCount > 1);
    }

    private View CreateDefaultPagerView()
    {
        var paginator = new UraniumUI.Material.Controls.Paginator { ChangePageCommand = GoToPageCommand, VerticalOptions = LayoutOptions.Center };
        paginator.SetBinding(
            UraniumUI.Material.Controls.Paginator.CurrentPageProperty,
            new Binding(nameof(CurrentPage), source: this));
        paginator.SetBinding(
            UraniumUI.Material.Controls.Paginator.TotalPageCountProperty,
            new Binding(nameof(TotalPageCount), source: this));

        var pageSizeArea = new HorizontalStackLayout { Spacing = 8, HorizontalOptions = LayoutOptions.End, VerticalOptions = LayoutOptions.Center };
        pageSizeArea.Add(new Label { Text = "Rows per page:", FontSize = 14, VerticalOptions = LayoutOptions.Center });

        var pageSizePicker = new Picker { WidthRequest = 92, FontSize = 14, VerticalOptions = LayoutOptions.Center };
        pageSizePicker.SetBinding(
            Picker.ItemsSourceProperty,
            new Binding(nameof(PageSizeOptions), source: this));
        pageSizePicker.SetBinding(
            Picker.SelectedItemProperty,
            new Binding(nameof(PageSize), source: this, mode: BindingMode.TwoWay));
        pageSizeArea.Add(pageSizePicker);

        var matchingLabel = new Label { FontSize = 14, VerticalOptions = LayoutOptions.Center };
        matchingLabel.SetBinding(
            Label.TextProperty,
            new Binding(nameof(FilteredItemCount), source: this, stringFormat: "Matching: {0}"));
        pageSizeArea.Add(matchingLabel);

        var pagerGrid = new Grid { Padding = new Thickness(8, 6), ColumnSpacing = 24, ColumnDefinitions = { new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto) } };
        pagerGrid.Add(paginator, 0, 0);
        pagerGrid.Add(pageSizeArea, 1, 0);

        return new ScrollView { Orientation = ScrollOrientation.Horizontal, HorizontalScrollBarVisibility = ScrollBarVisibility.Never, Content = pagerGrid };
    }

    private void RaiseSearchCanExecuteChanged()
    {
        clearSearchCommand.ChangeCanExecute();
    }

    private void RaisePagingCanExecuteChanged()
    {
        firstPageCommand.ChangeCanExecute();
        previousPageCommand.ChangeCanExecute();
        nextPageCommand.ChangeCanExecute();
        lastPageCommand.ChangeCanExecute();
    }

    private void ScrollCurrentPageToTop()
    {
        if (PageItemCount == 0 || !CanUseRowsPlatformHost)
        {
            return;
        }

        var generation = RowsHandlerGeneration;

        Dispatcher.Dispatch(() =>
        {
            if (PageItemCount > 0 &&
                CanUseRowsPlatformHost &&
                generation == RowsHandlerGeneration)
            {
                rowsView.ScrollTo(
                    0,
                    position: ScrollToPosition.Start,
                    animate: false);
            }
        });
    }
}
