using System.Collections;
using System.Collections.ObjectModel;
using System.Windows.Input;

namespace UraniumUI.Material.VirtualizedDataGrid.Controls;

/// <summary>
/// Filtering, paging, empty-state, and pager properties for <see cref="VirtualizedDataGrid"/>.
/// </summary>
public partial class VirtualizedDataGrid
{
    /// <summary>
    /// Gets or sets a predicate applied before text filtering and paging.
    /// </summary>
    public Func<object, bool>? FilterPredicate
    {
        get => (Func<object, bool>?)GetValue(FilterPredicateProperty);
        set => SetValue(FilterPredicateProperty, value);
    }

    /// <summary>Identifies the <see cref="FilterPredicate"/> bindable property.</summary>
    public static readonly BindableProperty FilterPredicateProperty = BindableProperty.Create(
        nameof(FilterPredicate),
        typeof(Func<object, bool>),
        typeof(VirtualizedDataGrid),
        null,
        propertyChanged: static (bindable, _, _) =>
            ((VirtualizedDataGrid)bindable).OnFilterSettingsChanged());

    /// <summary>
    /// Gets or sets the text matched against <see cref="FilterMemberPaths"/>.
    /// </summary>
    public string? FilterText
    {
        get => (string?)GetValue(FilterTextProperty);
        set => SetValue(FilterTextProperty, value);
    }

    /// <summary>Identifies the <see cref="FilterText"/> bindable property.</summary>
    public static readonly BindableProperty FilterTextProperty = BindableProperty.Create(
        nameof(FilterText),
        typeof(string),
        typeof(VirtualizedDataGrid),
        null,
        defaultBindingMode: BindingMode.TwoWay,
        propertyChanged: static (bindable, _, _) =>
            ((VirtualizedDataGrid)bindable).OnFilterSettingsChanged());

    /// <summary>
    /// Gets or sets a comma- or semicolon-separated list of property paths searched by
    /// <see cref="FilterText"/>, for example "Name,DisplayName,Description".
    /// Nested paths such as "Metadata.Description" are supported.
    /// </summary>
    public string? FilterMemberPaths
    {
        get => (string?)GetValue(FilterMemberPathsProperty);
        set => SetValue(FilterMemberPathsProperty, value);
    }

    /// <summary>Identifies the <see cref="FilterMemberPaths"/> bindable property.</summary>
    public static readonly BindableProperty FilterMemberPathsProperty = BindableProperty.Create(
        nameof(FilterMemberPaths),
        typeof(string),
        typeof(VirtualizedDataGrid),
        null,
        propertyChanged: static (bindable, _, _) =>
            ((VirtualizedDataGrid)bindable).OnFilterSettingsChanged());

    /// <summary>
    /// Gets or sets the comparison used for built-in text filtering.
    /// </summary>
    public StringComparison FilterStringComparison
    {
        get => (StringComparison)GetValue(FilterStringComparisonProperty);
        set => SetValue(FilterStringComparisonProperty, value);
    }

    /// <summary>Identifies the <see cref="FilterStringComparison"/> bindable property.</summary>
    public static readonly BindableProperty FilterStringComparisonProperty = BindableProperty.Create(
        nameof(FilterStringComparison),
        typeof(StringComparison),
        typeof(VirtualizedDataGrid),
        StringComparison.CurrentCultureIgnoreCase,
        propertyChanged: static (bindable, _, _) =>
            ((VirtualizedDataGrid)bindable).OnFilterSettingsChanged());


    /// <summary>
    /// Gets or sets whether the search area is displayed above the grid.
    /// The search area remains visible when filtering produces zero rows so the
    /// user can always change or clear the search.
    /// </summary>
    public bool ShowSearchBar
    {
        get => (bool)GetValue(ShowSearchBarProperty);
        set => SetValue(ShowSearchBarProperty, value);
    }

    /// <summary>Identifies the <see cref="ShowSearchBar"/> bindable property.</summary>
    public static readonly BindableProperty ShowSearchBarProperty = BindableProperty.Create(
        nameof(ShowSearchBar),
        typeof(bool),
        typeof(VirtualizedDataGrid),
        false,
        propertyChanged: static (bindable, _, _) =>
            ((VirtualizedDataGrid)bindable).ApplySearchBar());

    /// <summary>
    /// Gets or sets a custom search view. It takes precedence over
    /// <see cref="SearchTemplate"/>.
    ///
    /// A directly supplied view keeps its inherited binding context. This is useful
    /// when the search area primarily binds to the page ViewModel.
    /// </summary>
    public View? SearchView
    {
        get => (View?)GetValue(SearchViewProperty);
        set => SetValue(SearchViewProperty, value);
    }

    /// <summary>Identifies the <see cref="SearchView"/> bindable property.</summary>
    public static readonly BindableProperty SearchViewProperty = BindableProperty.Create(
        nameof(SearchView),
        typeof(View),
        typeof(VirtualizedDataGrid),
        null,
        propertyChanged: static (bindable, _, _) =>
            ((VirtualizedDataGrid)bindable).ApplySearchBar());

    /// <summary>
    /// Gets or sets a template for a custom search area. Its binding context is the
    /// <see cref="VirtualizedDataGrid"/> itself, matching <see cref="PagerTemplate"/>.
    ///
    /// The page ViewModel remains available through the grid's BindingContext property,
    /// for example: BindingContext.ModifiedParameterCount.
    /// </summary>
    public DataTemplate? SearchTemplate
    {
        get => (DataTemplate?)GetValue(SearchTemplateProperty);
        set => SetValue(SearchTemplateProperty, value);
    }

    /// <summary>Identifies the <see cref="SearchTemplate"/> bindable property.</summary>
    public static readonly BindableProperty SearchTemplateProperty = BindableProperty.Create(
        nameof(SearchTemplate),
        typeof(DataTemplate),
        typeof(VirtualizedDataGrid),
        null,
        propertyChanged: static (bindable, _, _) =>
            ((VirtualizedDataGrid)bindable).ApplySearchBar());

    /// <summary>
    /// Gets or sets the placeholder used by the default search view.
    /// Custom templates may bind to this property.
    /// </summary>
    public string SearchPlaceholder
    {
        get => (string)GetValue(SearchPlaceholderProperty);
        set => SetValue(SearchPlaceholderProperty, value);
    }

    /// <summary>Identifies the <see cref="SearchPlaceholder"/> bindable property.</summary>
    public static readonly BindableProperty SearchPlaceholderProperty = BindableProperty.Create(
        nameof(SearchPlaceholder),
        typeof(string),
        typeof(VirtualizedDataGrid),
        "Search by name or description...",
        propertyChanged: static (bindable, _, _) =>
            ((VirtualizedDataGrid)bindable).ApplySearchBar());

    /// <summary>
    /// Gets or sets whether changing a filter returns to page one.
    /// </summary>
    public bool ResetPageOnFilterChange
    {
        get => (bool)GetValue(ResetPageOnFilterChangeProperty);
        set => SetValue(ResetPageOnFilterChangeProperty, value);
    }

    /// <summary>Identifies the <see cref="ResetPageOnFilterChange"/> bindable property.</summary>
    public static readonly BindableProperty ResetPageOnFilterChangeProperty = BindableProperty.Create(
        nameof(ResetPageOnFilterChange),
        typeof(bool),
        typeof(VirtualizedDataGrid),
        true);

    /// <summary>
    /// Gets or sets whether the control presents only one page of the filtered data.
    /// </summary>
    public bool EnablePaging
    {
        get => (bool)GetValue(EnablePagingProperty);
        set => SetValue(EnablePagingProperty, value);
    }

    /// <summary>Identifies the <see cref="EnablePaging"/> bindable property.</summary>
    public static readonly BindableProperty EnablePagingProperty = BindableProperty.Create(
        nameof(EnablePaging),
        typeof(bool),
        typeof(VirtualizedDataGrid),
        false,
        propertyChanged: static (bindable, _, _) =>
            ((VirtualizedDataGrid)bindable).OnPagingSettingsChanged(resetCurrentPage: true));

    /// <summary>
    /// Gets or sets the maximum number of rows on each page. Values less than one are coerced to one.
    /// </summary>
    public int PageSize
    {
        get => (int)GetValue(PageSizeProperty);
        set => SetValue(PageSizeProperty, value);
    }

    /// <summary>Identifies the <see cref="PageSize"/> bindable property.</summary>
    public static readonly BindableProperty PageSizeProperty = BindableProperty.Create(
        nameof(PageSize),
        typeof(int),
        typeof(VirtualizedDataGrid),
        100,
        defaultBindingMode: BindingMode.TwoWay,
        coerceValue: static (_, value) => Math.Max(1, (int)value),
        propertyChanged: static (bindable, _, _) =>
            ((VirtualizedDataGrid)bindable).OnPagingSettingsChanged(resetCurrentPage: true));

    /// <summary>
    /// Gets or sets the one-based current page number.
    /// </summary>
    public int CurrentPage
    {
        get => (int)GetValue(CurrentPageProperty);
        set => SetValue(CurrentPageProperty, value);
    }

    /// <summary>Identifies the <see cref="CurrentPage"/> bindable property.</summary>
    public static readonly BindableProperty CurrentPageProperty = BindableProperty.Create(
        nameof(CurrentPage),
        typeof(int),
        typeof(VirtualizedDataGrid),
        1,
        defaultBindingMode: BindingMode.TwoWay,
        coerceValue: static (_, value) => Math.Max(1, (int)value),
        propertyChanged: static (bindable, _, _) =>
            ((VirtualizedDataGrid)bindable).OnCurrentPageChanged());

    /// <summary>
    /// Gets or sets whether the control creates and displays a built-in pager below the rows.
    /// </summary>
    public bool ShowPager
    {
        get => (bool)GetValue(ShowPagerProperty);
        set => SetValue(ShowPagerProperty, value);
    }

    /// <summary>Identifies the <see cref="ShowPager"/> bindable property.</summary>
    public static readonly BindableProperty ShowPagerProperty = BindableProperty.Create(
        nameof(ShowPager),
        typeof(bool),
        typeof(VirtualizedDataGrid),
        false,
        propertyChanged: static (bindable, _, _) =>
            ((VirtualizedDataGrid)bindable).ApplyPager());

    /// <summary>
    /// Gets or sets whether the pager remains visible when all filtered rows fit on one page.
    /// </summary>
    public bool ShowPagerWhenSinglePage
    {
        get => (bool)GetValue(ShowPagerWhenSinglePageProperty);
        set => SetValue(ShowPagerWhenSinglePageProperty, value);
    }

    /// <summary>Identifies the <see cref="ShowPagerWhenSinglePage"/> bindable property.</summary>
    public static readonly BindableProperty ShowPagerWhenSinglePageProperty = BindableProperty.Create(
        nameof(ShowPagerWhenSinglePage),
        typeof(bool),
        typeof(VirtualizedDataGrid),
        false,
        propertyChanged: static (bindable, _, _) =>
            ((VirtualizedDataGrid)bindable).UpdatePagerVisibility());

    /// <summary>
    /// Gets or sets a custom pager view. It takes precedence over <see cref="PagerTemplate"/>.
    /// </summary>
    public View? PagerView
    {
        get => (View?)GetValue(PagerViewProperty);
        set => SetValue(PagerViewProperty, value);
    }

    /// <summary>Identifies the <see cref="PagerView"/> bindable property.</summary>
    public static readonly BindableProperty PagerViewProperty = BindableProperty.Create(
        nameof(PagerView),
        typeof(View),
        typeof(VirtualizedDataGrid),
        null,
        propertyChanged: static (bindable, _, _) =>
            ((VirtualizedDataGrid)bindable).ApplyPager());

    /// <summary>
    /// Gets or sets a template for a custom pager. Its binding context is the grid.
    /// </summary>
    public DataTemplate? PagerTemplate
    {
        get => (DataTemplate?)GetValue(PagerTemplateProperty);
        set => SetValue(PagerTemplateProperty, value);
    }

    /// <summary>Identifies the <see cref="PagerTemplate"/> bindable property.</summary>
    public static readonly BindableProperty PagerTemplateProperty = BindableProperty.Create(
        nameof(PagerTemplate),
        typeof(DataTemplate),
        typeof(VirtualizedDataGrid),
        null,
        propertyChanged: static (bindable, _, _) =>
            ((VirtualizedDataGrid)bindable).ApplyPager());

    /// <summary>
    /// Gets or sets the selectable page sizes shown by the default pager.
    /// </summary>
    public IList PageSizeOptions
    {
        get => (IList)GetValue(PageSizeOptionsProperty);
        set => SetValue(PageSizeOptionsProperty, value);
    }

    /// <summary>Identifies the <see cref="PageSizeOptions"/> bindable property.</summary>
    public static readonly BindableProperty PageSizeOptionsProperty = BindableProperty.Create(
        nameof(PageSizeOptions),
        typeof(IList),
        typeof(VirtualizedDataGrid),
        defaultValueCreator: static _ => new ObservableCollection<int> { 25, 50, 100, 250, 500 },
        propertyChanged: static (bindable, _, _) =>
            ((VirtualizedDataGrid)bindable).ApplyPager());

    /// <summary>
    /// Gets or sets whether changing pages scrolls the virtualized rows to the first row.
    /// </summary>
    public bool ScrollToTopOnPageChange
    {
        get => (bool)GetValue(ScrollToTopOnPageChangeProperty);
        set => SetValue(ScrollToTopOnPageChangeProperty, value);
    }

    /// <summary>Identifies the <see cref="ScrollToTopOnPageChange"/> bindable property.</summary>
    public static readonly BindableProperty ScrollToTopOnPageChangeProperty = BindableProperty.Create(
        nameof(ScrollToTopOnPageChange),
        typeof(bool),
        typeof(VirtualizedDataGrid),
        true);


    private static readonly BindablePropertyKey HasSearchTextPropertyKey = BindableProperty.CreateReadOnly(
        nameof(HasSearchText), typeof(bool), typeof(VirtualizedDataGrid), false);
    /// <summary>Identifies the read-only <see cref="HasSearchText"/> bindable property.</summary>
    public static readonly BindableProperty HasSearchTextProperty = HasSearchTextPropertyKey.BindableProperty;

    /// <summary>
    /// Gets a value indicating whether <see cref="FilterText"/> contains non-whitespace text.
    /// </summary>
    public bool HasSearchText => (bool)GetValue(HasSearchTextProperty);

    private static readonly BindablePropertyKey TotalItemCountPropertyKey = BindableProperty.CreateReadOnly(
        nameof(TotalItemCount), typeof(int), typeof(VirtualizedDataGrid), 0);
    /// <summary>Identifies the read-only <see cref="TotalItemCount"/> bindable property.</summary>
    public static readonly BindableProperty TotalItemCountProperty = TotalItemCountPropertyKey.BindableProperty;
    /// <summary>Gets the number of items before filtering.</summary>
    public int TotalItemCount => (int)GetValue(TotalItemCountProperty);

    private static readonly BindablePropertyKey FilteredItemCountPropertyKey = BindableProperty.CreateReadOnly(
        nameof(FilteredItemCount), typeof(int), typeof(VirtualizedDataGrid), 0);
    /// <summary>Identifies the read-only <see cref="FilteredItemCount"/> bindable property.</summary>
    public static readonly BindableProperty FilteredItemCountProperty = FilteredItemCountPropertyKey.BindableProperty;
    /// <summary>Gets the number of items after filtering and before paging.</summary>
    public int FilteredItemCount => (int)GetValue(FilteredItemCountProperty);

    private static readonly BindablePropertyKey PageItemCountPropertyKey = BindableProperty.CreateReadOnly(
        nameof(PageItemCount), typeof(int), typeof(VirtualizedDataGrid), 0);
    /// <summary>Identifies the read-only <see cref="PageItemCount"/> bindable property.</summary>
    public static readonly BindableProperty PageItemCountProperty = PageItemCountPropertyKey.BindableProperty;
    /// <summary>Gets the number of items on the current page.</summary>
    public int PageItemCount => (int)GetValue(PageItemCountProperty);

    private static readonly BindablePropertyKey TotalPageCountPropertyKey = BindableProperty.CreateReadOnly(
        nameof(TotalPageCount), typeof(int), typeof(VirtualizedDataGrid), 0);
    /// <summary>Identifies the read-only <see cref="TotalPageCount"/> bindable property.</summary>
    public static readonly BindableProperty TotalPageCountProperty = TotalPageCountPropertyKey.BindableProperty;
    /// <summary>Gets the total number of filtered pages.</summary>
    public int TotalPageCount => (int)GetValue(TotalPageCountProperty);

    private static readonly BindablePropertyKey HasPreviousPagePropertyKey = BindableProperty.CreateReadOnly(
        nameof(HasPreviousPage), typeof(bool), typeof(VirtualizedDataGrid), false);
    /// <summary>Identifies the read-only <see cref="HasPreviousPage"/> bindable property.</summary>
    public static readonly BindableProperty HasPreviousPageProperty = HasPreviousPagePropertyKey.BindableProperty;
    /// <summary>Gets whether a page precedes the current page.</summary>
    public bool HasPreviousPage => (bool)GetValue(HasPreviousPageProperty);

    private static readonly BindablePropertyKey HasNextPagePropertyKey = BindableProperty.CreateReadOnly(
        nameof(HasNextPage), typeof(bool), typeof(VirtualizedDataGrid), false);
    /// <summary>Identifies the read-only <see cref="HasNextPage"/> bindable property.</summary>
    public static readonly BindableProperty HasNextPageProperty = HasNextPagePropertyKey.BindableProperty;
    /// <summary>Gets whether a page follows the current page.</summary>
    public bool HasNextPage => (bool)GetValue(HasNextPageProperty);

    private static readonly BindablePropertyKey IsEmptyPropertyKey = BindableProperty.CreateReadOnly(
        nameof(IsEmpty), typeof(bool), typeof(VirtualizedDataGrid), true);
    /// <summary>Identifies the read-only <see cref="IsEmpty"/> bindable property.</summary>
    public static readonly BindableProperty IsEmptyProperty = IsEmptyPropertyKey.BindableProperty;
    /// <summary>Gets whether the current filtered result contains no items.</summary>
    public bool IsEmpty => (bool)GetValue(IsEmptyProperty);

    private static readonly BindablePropertyKey HasItemsPropertyKey = BindableProperty.CreateReadOnly(
        nameof(HasItems), typeof(bool), typeof(VirtualizedDataGrid), false);
    /// <summary>Identifies the read-only <see cref="HasItems"/> bindable property.</summary>
    public static readonly BindableProperty HasItemsProperty = HasItemsPropertyKey.BindableProperty;
    /// <summary>Gets whether the current filtered result contains items.</summary>
    public bool HasItems => (bool)GetValue(HasItemsProperty);


    /// <summary>Clears <see cref="FilterText"/>.</summary>
    public ICommand ClearSearchCommand { get; private set; } = null!;

    /// <summary>Moves to the first page.</summary>
    public ICommand FirstPageCommand { get; private set; } = null!;

    /// <summary>Moves to the previous page.</summary>
    public ICommand PreviousPageCommand { get; private set; } = null!;

    /// <summary>Moves to the next page.</summary>
    public ICommand NextPageCommand { get; private set; } = null!;

    /// <summary>Moves to the last page.</summary>
    public ICommand LastPageCommand { get; private set; } = null!;
}
