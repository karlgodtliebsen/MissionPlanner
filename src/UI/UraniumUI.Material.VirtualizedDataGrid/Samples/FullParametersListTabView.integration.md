# Full Parameters List integration

The existing page already has a search box and a virtualized grid. The smallest conversion to control-owned filtering and paging is:

```xml
<Entry
    Text="{Binding Source={x:Reference ParameterGrid}, Path=FilterText, Mode=TwoWay}"
    Placeholder="Search by name or description..." />

<virtualized:VirtualizedDataGrid
    x:Name="ParameterGrid"
    ItemsSource="{Binding Parameters}"
    FilterMemberPaths="Name,DisplayName,Description"
    EnablePaging="True"
    ShowPager="True"
    PageSize="100"
    UseAutoColumns="False"
    AutoColumnWidth="180"
    MinimumStarColumnWidth="120"
    ItemSizingStrategy="MeasureFirstItem"
    RowHeight="125">

    <virtualized:VirtualizedDataGrid.EmptyView>
        <VerticalStackLayout
            Padding="40"
            Spacing="8"
            HorizontalOptions="Center"
            VerticalOptions="Center">
            <Label Text="No parameters match the current filter."
                   FontAttributes="Bold"
                   FontSize="16" />
            <Label Text="Change or clear the search text."
                   FontSize="13"
                   Opacity="0.75" />
        </VerticalStackLayout>
    </virtualized:VirtualizedDataGrid.EmptyView>

    <!-- Keep the existing DataGridColumn definitions unchanged. -->
</virtualized:VirtualizedDataGrid>
```

The search/status row should not be hidden when the filtered view is empty. Otherwise the user cannot clear a filter that produced zero rows. Remove this from the search row:

```xml
IsVisible="{Binding HasRows}"
```

or replace it with a state that means the parameter feature is available rather than that rows currently exist.

The status labels can bind directly to the grid:

```xml
<Label Text="{Binding Source={x:Reference ParameterGrid}, Path=FilteredItemCount, StringFormat='Matching: {0}'}" />
<Label Text="{Binding Source={x:Reference ParameterGrid}, Path=TotalItemCount, StringFormat='Total: {0}'}" />
```

## Important ViewModel choice

If the current `Parameters` collection is already filtered or contains only the current page, either:

1. change it to expose the complete source and let the control filter/page; or
2. keep ViewModel-owned filtering/paging and leave `EnablePaging="False"` and `FilterText` unset.

Do not apply paging twice.

## Reusing the external pager

The existing external pager can be retained by binding it to the grid instead of the ViewModel:

```xml
<Button Text="First"
        Command="{Binding Source={x:Reference ParameterGrid}, Path=FirstPageCommand}" />
<Button Text="Previous"
        Command="{Binding Source={x:Reference ParameterGrid}, Path=PreviousPageCommand}" />
<Entry Text="{Binding Source={x:Reference ParameterGrid}, Path=CurrentPage, Mode=TwoWay}" />
<Label Text="{Binding Source={x:Reference ParameterGrid}, Path=TotalPageCount}" />
<Button Text="Next"
        Command="{Binding Source={x:Reference ParameterGrid}, Path=NextPageCommand}" />
<Button Text="Last"
        Command="{Binding Source={x:Reference ParameterGrid}, Path=LastPageCommand}" />
<Entry Text="{Binding Source={x:Reference ParameterGrid}, Path=PageSize, Mode=TwoWay}" />
<Label Text="{Binding Source={x:Reference ParameterGrid}, Path=FilteredItemCount}" />
```

In that design, set `ShowPager="False"` because the page owns the pager UI.
