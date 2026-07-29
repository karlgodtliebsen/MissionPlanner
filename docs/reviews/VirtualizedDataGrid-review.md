# VirtualizedDataGrid design review

## Scope

Reviewed the current implementation under:

```text
src/UI/UraniumUI/UraniumUI.Material.VirtualizedDataGrid/
```

The control is already a substantial improvement over UraniumUI's original Grid-based DataGrid. This review concentrates on maintainability, lifecycle correctness, and scaling beyond the parameter-list scenario.

## Strong design decisions

1. **Correct virtualization boundary**
   - The internal `CollectionView` owns vertical scrolling.
   - It is not wrapped in a vertical `ScrollView`.
   - The fixed header and rows share one resolved column-width model.

2. **Bounded visual tree**
   - Cell views exist only for realized `VirtualizedDataGridRowPresenter` instances.
   - The owner tracks presenters through `WeakReference<T>`.

3. **Good composition**
   - Search, header, rows, empty state and pager are separate hosts.
   - Empty state is explicit rather than delegated to the original DataGrid behavior.
   - Search and pager templates are practical extension points.

4. **Useful data-view features**
   - Filtering, paging, selection, auto columns, templates, batch refresh and horizontal scrolling are integrated.
   - `MeasureFirstItem` and `KeepScrollOffset` are sensible defaults.

5. **UraniumUI compatibility**
   - Existing `DataGridColumn`, selection-column, style-class and value-binding concepts are retained.

The fundamental architecture is sound enough to keep using and prepare for an upstream contribution.

# Findings

## High priority

### VDG-01 — Row-host lifecycle state is over-complex and internally inconsistent

File:

```text
Controls/VirtualizedDataGrid.RowsLifecycle.cs
```

Relevant areas:

```text
lines 15-33    flags, retries and generations
lines 56-59    CanUseRowsPlatformHost
lines 63-171   overlapping outer/child lifecycle events
lines 225-408  queued apply and timer retries
lines 341-357  exception-message based PlatformView detection
```

Problems:

- `rowsViewLoaded` and `rowsHandlerReady` are written but not used by `CanUseRowsPlatformHost`.
- Readiness is inferred from:

  ```csharp
  rowsView.Handler is not null && (rowsView.IsLoaded || IsLoaded)
  ```

  The outer grid can be loaded while the child handler has no usable native view.

- A single tab transition can increment the generation several times through outer and child handler/load events.
- A 20 × 50 ms timer-retry loop adds nondeterminism and may expire before late reattachment.
- Platform failure is detected by parsing exception text containing `PlatformView` and `null`.
- Logical source state, native lifecycle, retry scheduling, configuration and measure invalidation are mixed together.

Recommendation:

Replace the booleans/generations/retry timer with a small explicit state machine:

```text
Detached
Attaching
Ready
Faulted
```

Keep only:

```text
desiredRowsSource
appliedRowsSource
sourceRevision
appliedRevision
hostState
handler identity/generation
```

Rules:

- every data-view update replaces the desired source and increments a revision;
- apply synchronously when already on the UI thread and state is `Ready`;
- while detached, wait for a positive lifecycle event rather than polling;
- after a native failure, mark the child faulted;
- if the same child cannot recover, recreate only the internal `CollectionView`;
- keep navigation/ViewModel policy outside the control.

The discovery that the original problem was triggered by clearing a still-bound collection during ViewModel disposal reduces the need for aggressive recovery logic. Simplification is now more valuable than adding retries.

### VDG-02 — Dead visual-release subsystem

File:

```text
Controls/VirtualizedDataGrid.cs
```

- `ReleaseVisualResources()` is defined around line 505.
- No caller exists.
- `visualResourcesReleased` is set to `true` only inside that unused method.
- Many methods branch on this state.

Recommendation:

Either remove `ReleaseVisualResources()` and `visualResourcesReleased`, or introduce an explicit final-disposal contract and tests. For a reusable MAUI control, removing the dead state is preferable.

### VDG-03 — UI-thread affinity is assumed but not enforced

`ItemsSource_CollectionChanged` recalculates and writes MAUI state directly. A source mutated from a worker thread can produce undefined behavior.

Recommendation:

- Document that bound collections must be mutated on the MAUI dispatcher.
- Add debug assertions for dispatcher access.
- Optionally coalesce off-thread notifications into one dispatcher refresh, but do not silently dispatch every individual event.

## Medium priority

### VDG-04 — Filtering repeats avoidable parsing and reflection work

File:

```text
Controls/VirtualizedDataGrid.DataView.cs
```

`ParseFilterMemberPaths()` is called inside `MatchesCurrentFilter()` for every row. For 1,396 rows, every keystroke repeatedly splits and allocates the same path list.

Recommendation:

- Cache parsed paths when `FilterMemberPaths` changes.
- Normalize `FilterText` once per refresh.
- Resolve accessors once per item type/path set.
- Add optional 150–250 ms search debounce.

### VDG-05 — Static filter accessor cache is unbounded

The static `ConcurrentDictionary<(Type,string), Func<...>>` never releases entries. It can retain plugin/dynamic types for process lifetime.

Recommendation:

Use an instance cache, `ConditionalWeakTable`, bounded cache, or explicitly document the finite-type assumption.

### VDG-06 — Paging allocates unnecessary full intermediate lists

When paging is enabled without filtering, the whole source is copied and then the page is copied again.

Recommendation:

For `IList`, page by index or build only the final page list. Retain the original observable source when neither filtering nor paging is active.

### VDG-07 — Selection identity uses value equality implicitly

`HashSet<object>` and `ToHashSet()` use object equality. Records or models overriding equality can make distinct rows collide.

Recommendation:

Expose one of:

```text
SelectionComparer
ItemKeySelector
UseReferenceEquality (default true)
```

Reference equality is the safest UI-row default.

### VDG-08 — Row refresh recreates every realized cell

`RefreshRealizedRows()` calls `RefreshFromOwner()`, clearing and recreating controls. It is viewport-bounded, but can lose focus, text selection, open picker state, or transient editor state.

Recommendation:

Classify updates:

- width/visibility: update definitions and visibility;
- selection color: update visual states;
- template change: rebuild cells;
- padding/style: update affected cells only.

Editable state must always be committed to the bound model.

### VDG-09 — Auto-column widths only grow

Widths learned from realized content do not naturally shrink after filtering/source replacement.

Recommendation:

Add:

```csharp
ResetAutoColumnWidths()
```

and an optional policy:

```text
GrowOnly
ResetOnItemsSourceChange
ResetOnFilterChange
```

Profile measurement scheduling during rapid scrolling.

### VDG-10 — Search visibility semantics are surprising

`UpdateSearchBarVisibility()` hides the search host unless the grid has items or already has search text. A newly empty grid with `ShowSearchBar=true` therefore hides its search template.

Recommendation:

Use:

```csharp
searchHost.IsVisible = ShowSearchBar;
```

or expose `ShowSearchBarWhenEmpty`.

### VDG-11 — Dead default clear-button construction

A clear button is created and bound, but adding it to the default search grid is commented out.

Recommendation:

Restore it or remove the object and commented column code.

## Lower priority

### VDG-12 — Template result handling assumes `View`

`DataTemplate.CreateContent() as View` silently ignores wrapper forms such as `ViewCell`.

Recommendation:

Add a helper that unwraps supported wrappers and throws/logs a clear error for unsupported results.

### VDG-13 — Non-generic `IList` limits future source types

This preserves original DataGrid compatibility, but excludes direct `IEnumerable<T>`, `IReadOnlyList<T>`, incremental loaders and async sources.

Recommendation:

Keep `IList` for now; consider a future source-adapter layer rather than breaking the public API before an upstream PR.

### VDG-14 — Responsibility density

The implementation is roughly 3,600 lines across partials. Partials help navigation, but private state remains heavily coupled.

After lifecycle simplification, consider extracting:

```text
ColumnWidthResolver
DataViewProjector
SelectionTracker
RowsHostController
```

Extract only where state ownership becomes clearer and independently testable.

# Recommended order

1. Simplify lifecycle and remove timer polling.
2. Remove dead resource-release state.
3. Cache filtering paths and add debounce.
4. Clarify selection identity.
5. Split incremental column updates from full row rebuilds.
6. Add auto-width reset policy.
7. Clean search semantics and dead clear-button code.

# Overall assessment

The control's main weakness is no longer virtualization. It is the defensive lifecycle machinery accumulated around a problem substantially caused by mutating a still-bound collection during ViewModel disposal.

Keep the control. Simplify lifecycle before an upstream PR. Focus tests on source replacement, empty/populated transitions, detach/reattach without bound-source mutation, filtered/paged replacement, editable-cell recycling, selection identity and bounded native realization.
