# VirtualizedItemsGrid

A thin table shell around Avalonia `ItemsRepeater` for MissionPlanner.

## Why this shape

- `ItemsRepeater` remains responsible for UI virtualization.
- Each realized data item is one row; the row itself is a `Grid`.
- The header and row templates both use `VirtualizedItemsGridRow`, which obtains
  its column definitions from the owning `VirtualizedItemsGrid`.
- Column geometry therefore has a single source of truth and cannot drift because
  one StackPanel has different spacing than another.
- Search, paging, empty-state and checkbox selection are shell features rather
  than responsibilities of the repeater itself.

## Files

- `Controls/VirtualizedItemsGrid.cs` - templated control, filtering, paging,
  selection and horizontal header synchronization.
- `Controls/VirtualizedItemsGridRow.cs` - Grid that mirrors the owner's column
  widths and spacing.
- `Controls/VirtualizedItemsGridEnums.cs` - selection configuration.
- `Themes/VirtualizedItemsGrid.axaml` - default ControlTheme.
- `Examples/FullParametersListExample.axaml` - intended Full Parameters stress test.

## Integration

Merge the theme into the Avalonia application resources, e.g. adapt the URI to
where you place the file:

```xml
<Application.Styles>
    <FluentTheme />
    <StyleInclude Source="avares://MissionPlanner.App/Themes/VirtualizedItemsGrid.axaml" />
</Application.Styles>
```

If your application already has a central controls theme/resource dictionary,
include the `ControlTheme` there instead.

## Column geometry

Define widths once:

```xml
ColumnWidths="190,110,110,110,90,420"
ColumnSpacing="6"
```

Supported tokens are:

- pixel: `120`
- auto: `Auto`
- star: `*`, `2*`

Both header and row templates must use `VirtualizedItemsGridRow` as their root
and position cells with normal `Grid.Column` values.

## Search

For a client-side Full Parameters test:

```xml
ShowSearchBar="True"
SearchMemberPaths="Name,DisplayName,Description,Value,Units"
```

Search is case-insensitive and can follow dotted property paths.
For specialized matching, assign `SearchFilter` in code.

## Pagination

Pagination is optional and is performed after filtering:

```xml
ShowPagination="True"
PageSize="100"
```

This is deliberately client-side. For a future remote/streaming data source,
keep paging in the ViewModel/service and turn built-in pagination off.

## Selection

```xml
SelectionMode="Multiple"
SelectAllScope="CurrentPage"
```

Selection state is stored in the control, not in recycled CheckBox controls.
`SelectedItems` is exposed as a read-only observable collection.

## Full Parameters performance test

Run these cases separately:

1. No pagination, ~1,100-1,500 parameter rows: validates ItemsRepeater row virtualization.
2. Pagination enabled, PageSize=100: validates search/paging/selection shell overhead.
3. Search rapidly for common terms such as `SERIAL`, `RC`, `GPS`, `BATT`.
4. Scroll continuously from first to last row and back.
5. Select rows, scroll them out of view, then back into view: selection must remain correct after recycling.
6. Select all on one page, move page, then return: state must remain correct.
7. Change the underlying ObservableCollection while scrolled: view counts and empty state must refresh.

## Intentional limits in v1

- Row virtualization only; no horizontal/cell virtualization.
- Fixed shared column geometry; no column resize/reorder yet.
- Search and pagination are client-side.
- No sorting yet.
- No keyboard-current-row model yet.

These are intentional so the first version remains much smaller and faster than a DataGrid.
