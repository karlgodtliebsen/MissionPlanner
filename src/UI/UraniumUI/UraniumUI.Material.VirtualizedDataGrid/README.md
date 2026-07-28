# UraniumUI.Material.VirtualizedDataGrid

This revision builds on the virtualized grid and adds four data-view features:

1. deterministic empty-state rendering;
2. optional built-in paging;
3. optional text and predicate filtering;
4. a built-in or templated search area above the table.

The row renderer remains a platform `CollectionView`, so only viewport rows are realized.

## Files added in v2

```text
Controls/
  VirtualizedDataGrid.DataView.cs
  VirtualizedDataGrid.DataView.BindableProperties.cs
```

The existing `VirtualizedDataGrid.cs` hosts an optional search area above the table, the rows and empty state in separate overlay layers, and an optional pager below the horizontally scrollable table.

## Empty view

The control supports the same property-element syntax as UraniumUI DataGrid:

```xml
<virtualized:VirtualizedDataGrid ItemsSource="{Binding Parameters}">
    <virtualized:VirtualizedDataGrid.EmptyView>
        <VerticalStackLayout Padding="40" HorizontalOptions="Center" VerticalOptions="Center">
            <Label Text="No parameters found." FontAttributes="Bold" />
            <Label Text="Change or clear the search text." />
        </VerticalStackLayout>
    </virtualized:VirtualizedDataGrid.EmptyView>

    <virtualized:VirtualizedDataGrid.Columns>
        <!-- columns -->
    </virtualized:VirtualizedDataGrid.Columns>
</virtualized:VirtualizedDataGrid>
```

Unlike the original implementation, the empty state is **not** delegated to `CollectionView.EmptyView`. It lives in an explicit overlay and is shown only when `PageItemCount == 0`. The rows host and empty host are mutually exclusive, so the empty view cannot remain visible while rows are displayed.

The following read-only properties are available:

```csharp
bool IsEmpty
bool HasItems
int TotalItemCount
int FilteredItemCount
int PageItemCount
```

## Text filtering

Bind the grid to the complete unpaged collection and specify the searchable properties:

```xml
<virtualized:VirtualizedDataGrid
    ItemsSource="{Binding Parameters}"
    FilterText="{Binding SearchText}"
    FilterMemberPaths="Name,DisplayName,Description" />
```

`FilterMemberPaths` accepts comma- or semicolon-separated public property paths. Nested paths are supported:

```text
Name,Metadata.DisplayName,Metadata.Description
```

Filtering uses `CurrentCultureIgnoreCase` by default. It can be changed through `FilterStringComparison`.

For arbitrary business rules, bind or assign:

```csharp
grid.FilterPredicate = item =>
    item is ParameterItemViewModel parameter &&
    !parameter.IsHidden;
```

The predicate and text filter are combined with logical AND.

When a row property changes and that property affects filtering, call:

```csharp
grid.RefreshView();
```

The control deliberately does not subscribe to every row's `PropertyChanged` event. Doing so can cause an editor row to disappear or move while the user is typing and would add 1,000+ subscriptions for a parameter list.


## Search area

Enable the default search area:

```xml
<virtualized:VirtualizedDataGrid
    ShowSearchBar="True"
    FilterMemberPaths="Name,DisplayName,Description" />
```

The default search area contains:

- a label;
- an entry bound two-way to `FilterText`;
- a clear command;
- matching and total counts.

Customize it with `SearchTemplate`. Like `PagerTemplate`, its binding context is
the grid itself:

```xml
<virtualized:VirtualizedDataGrid.SearchTemplate>
    <DataTemplate x:DataType="virtualized:VirtualizedDataGrid">
        <Grid ColumnDefinitions="Auto,*,Auto">
            <Label Text="Search:" />

            <Entry
                Grid.Column="1"
                Text="{Binding FilterText, Mode=TwoWay}"
                Placeholder="{Binding SearchPlaceholder}" />

            <Label
                Grid.Column="2"
                Text="{Binding TotalItemCount, StringFormat='Total: {0}'}" />
        </Grid>
    </DataTemplate>
</virtualized:VirtualizedDataGrid.SearchTemplate>
```

The application ViewModel is available through the grid's inherited
`BindingContext`:

```xml
<Label
    Text="{Binding BindingContext.ModifiedParameterCount,
                   StringFormat='Modified: {0}'}" />
```

`SearchView` can be supplied directly and takes precedence over
`SearchTemplate`. A directly supplied view retains/inherits the page binding
context.

Search properties:

```csharp
bool ShowSearchBar
View? SearchView
DataTemplate? SearchTemplate
string SearchPlaceholder
bool HasSearchText
ICommand ClearSearchCommand
```

The search host remains visible when filtering produces zero rows. Do not bind
its visibility to a page-level `HasRows`, because doing so can hide the only UI
that can clear a zero-result filter.

## Paging

Enable paging and the default pager:

```xml
<virtualized:VirtualizedDataGrid
    ItemsSource="{Binding Parameters}"
    EnablePaging="True"
    ShowPager="True"
    PageSize="100" />
```

The default pager uses `UraniumUI.Material.Controls.Paginator` and provides:

- first and previous navigation
- nearby page-number buttons
- next and last navigation
- selectable rows per page
- matching-row count

Paging properties and commands:

```csharp
int CurrentPage
int PageSize
int TotalPageCount
bool HasPreviousPage
bool HasNextPage

ICommand FirstPageCommand
ICommand PreviousPageCommand
ICommand NextPageCommand
ICommand LastPageCommand
ICommand GoToPageCommand
```

`CurrentPage` and `PageSize` are two-way bindable. Invalid page values are clamped. Filter changes return to page one by default; set `ResetPageOnFilterChange="False"` to retain/clamp the current page instead.

The default page-size options are:

```text
25, 50, 100, 250, 500
```

Replace them with:

```xml
<virtualized:VirtualizedDataGrid.PageSizeOptions>
    <x:Array Type="{x:Type x:Int32}">
        <x:Int32>50</x:Int32>
        <x:Int32>100</x:Int32>
        <x:Int32>200</x:Int32>
    </x:Array>
</virtualized:VirtualizedDataGrid.PageSizeOptions>
```

## Custom pager

Use `PagerTemplate` when application styling should own the pager. Its binding context is the grid itself:

```xml
<virtualized:VirtualizedDataGrid.PagerTemplate>
    <DataTemplate>
        <HorizontalStackLayout Spacing="8">
            <Button Text="Previous" Command="{Binding PreviousPageCommand}" />
            <Label Text="{Binding CurrentPage, StringFormat='Page {0}'}" />
            <Label Text="{Binding TotalPageCount, StringFormat='of {0}'}" />
            <Button Text="Next" Command="{Binding NextPageCommand}" />
        </HorizontalStackLayout>
    </DataTemplate>
</virtualized:VirtualizedDataGrid.PagerTemplate>
```

`PagerView` can also be supplied directly and takes precedence over `PagerTemplate`.

## Existing ViewModel paging

Do not page the same data twice.

Choose one of these designs:

### Control-owned paging

```text
ViewModel exposes all filtered/unfiltered rows
VirtualizedDataGrid performs filtering and paging
```

### ViewModel-owned paging

```text
ViewModel exposes only the current page
EnablePaging remains false
External pager remains in the page
```

For the current Full Parameters page, control-owned paging means binding `ItemsSource` to the complete parameter collection and binding the search entry to `VirtualizedDataGrid.FilterText`.

## Batch updates

`DeferRefresh()` still works with filtering and paging:

```csharp
using (ParameterGrid.DeferRefresh())
{
    foreach (var parameter in receivedParameters)
    {
        Parameters.Add(parameter);
    }
}
```

During the scope, the visible rows stay on a snapshot. Filtering and paging are recalculated once when the scope is disposed.

## Virtualization rules

- Do not place the control inside a vertical `ScrollView`.
- Use explicit or star widths for predictable column layout.
- Keep edit/validation state in the row ViewModel because row presenters are recycled.
- Prefer a fixed `RowHeight` and `MeasureFirstItem` when row sizes are uniform.

## Suggested manual tests

1. Empty source shows only the empty view.
2. First inserted row hides the empty view immediately.
3. Removing the last row restores the empty view.
4. A filter with no matches shows the empty view while retaining the search UI.
5. Clearing the filter restores rows.
6. Page size changes recalculate the page count.
7. Removing rows from the last page clamps `CurrentPage`.
8. `DeferRefresh()` performs one visible rebind after a large batch.
9. Repeated open/close does not retain the source, search view, pager, empty view, or row presenters.
10. Editable fields retain model state after scrolling away and back.

## Optional upstream tests

`Tests/VirtualizedDataGrid.DataView.Tests.cs.txt` contains starter xUnit/Shouldly tests designed for the UraniumUI test project. Rename it to `.cs` after copying it into the test project.
# Material styling

The library includes a ResourceDictionary that gives
`VirtualizedDataGrid` the same default surface, outline, rounded border,
separator, and selection colors as UraniumUI Material's original `DataGrid`.

Merge it after UraniumUI's Material `StyleResource` in the consuming
application's `App.xaml`:

```xml
<Application
    xmlns="http://schemas.microsoft.com/dotnet/2021/maui"
    xmlns:x="http://schemas.microsoft.com/winfx/2009/xaml"
    xmlns:material="clr-namespace:UraniumUI.Material.Resources;assembly=UraniumUI.Material"
    xmlns:virtualizedResources="clr-namespace:UraniumUI.Material.VirtualizedDataGrid.Resources;assembly=UraniumUI.Material.VirtualizedDataGrid">
    <Application.Resources>
        <ResourceDictionary>
            <ResourceDictionary.MergedDictionaries>
                <!-- Merge application colors and the UraniumUI Material
                     StyleResource first. -->
                <material:StyleResource ColorsOverride="{x:Reference appColors}" />
                <virtualizedResources:StyleResource ColorsOverride="{x:Reference appColors}" />
            </ResourceDictionary.MergedDictionaries>
        </ResourceDictionary>
    </Application.Resources>
</Application>
```

The dictionary contains an implicit style, so no `Style` attribute is
required on individual grids. To base an application-specific style on the
library style, use its public resource key:

```xml
<Style
    x:Key="CompactVirtualizedDataGrid"
    TargetType="virtualized:VirtualizedDataGrid"
    BasedOn="{StaticResource UraniumUI.Styles.VirtualizedDataGrid}">
    <Setter Property="CellPadding" Value="8,4" />
</Style>
```
