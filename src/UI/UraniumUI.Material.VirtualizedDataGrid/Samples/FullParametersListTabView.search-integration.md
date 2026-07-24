# Full Parameters List search integration

Move the page-level search grid into `VirtualizedDataGrid.SearchTemplate`.

The search template receives the grid as its BindingContext:

```text
FilterText                     grid-owned search text
FilteredItemCount              grid-owned matching count
TotalItemCount                 grid-owned source count
ClearSearchCommand             grid-owned clear command
BindingContext                 original page ViewModel
BindingContext.ModifiedParameterCount
```

Example:

```xml
<virtualized:VirtualizedDataGrid
    x:Name="ParameterGrid"
    ItemsSource="{Binding Parameters}"
    FilterMemberPaths="Name,DisplayName,Description"
    ShowSearchBar="True">

    <virtualized:VirtualizedDataGrid.SearchTemplate>
        <DataTemplate x:DataType="virtualized:VirtualizedDataGrid">
            <Grid ColumnDefinitions="Auto,*,Auto" Padding="18,4" Margin="0,10" ColumnSpacing="12">
                <Label Text="Search:" VerticalOptions="Center" />

                <Entry
                    Grid.Column="1"
                    Text="{Binding FilterText, Mode=TwoWay}"
                    Placeholder="{Binding SearchPlaceholder}" />

                <HorizontalStackLayout Grid.Column="2" Spacing="16">
                    <Label
                        Text="{Binding BindingContext.ModifiedParameterCount,
                                       StringFormat='Modified: {0}'}" />
                    <Label
                        Text="{Binding TotalItemCount,
                                       StringFormat='Total: {0}'}" />
                </HorizontalStackLayout>
            </Grid>
        </DataTemplate>
    </virtualized:VirtualizedDataGrid.SearchTemplate>
</virtualized:VirtualizedDataGrid>
```

Do not apply `IsVisible="{Binding HasRows}"` to the search area. A zero-result
filter must leave the search visible, otherwise the user cannot clear it.

When the grid owns filtering, remove or stop using ViewModel filtering for the
same collection to avoid two independent filtered views.
