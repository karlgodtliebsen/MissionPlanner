# Codex Task 3 — Debounce VirtualizedDataGrid Text Search

## Goal

Prevent `VirtualizedDataGrid` from performing a full local filter/data-view rebuild on every keystroke.

Add the normal search behavior where the text box updates immediately, but the expensive filtering operation waits briefly until typing pauses.

Target delay:

```text
200–250 ms
```

Make it configurable.

## Current source snapshot

Control:

```text
src/UI/UraniumUI/UraniumUI.Material.VirtualizedDataGrid/
```

Relevant files:

```text
Controls/VirtualizedDataGrid.DataView.cs
Controls/VirtualizedDataGrid.DataView.BindableProperties.cs
Controls/VirtualizedDataGrid.cs
Tests/UraniumUI.Material.Tests/VirtualizedDataGrid.SearchTemplate.Tests.cs
docs/reviews/VirtualizedDataGrid-review.md
```

Current behavior:

```csharp
FilterTextProperty
    propertyChanged -> OnFilterSettingsChanged()
    -> RefreshDataView(ResetPageOnFilterChange)
```

This means each character immediately scans and projects the source.

The existing design review already identifies this under `VDG-04` and also notes that `ParseFilterMemberPaths()` is currently repeated inside row matching.

---

## Required behavior

### 1. Add a configurable debounce interval

Add a bindable property with an unambiguous unit, for example:

```csharp
public int SearchDelayMilliseconds { get; set; }
```

Recommended default:

```text
250
```

Allowed range:

```text
>= 0
```

`0` must preserve immediate filtering behavior and is useful for tests or callers that explicitly want it.

Use the final name consistently in XML comments, README/sample XAML and tests.

Do not add an external reactive/debounce package for this.

### 2. Update `FilterText` immediately, delay only the expensive projection

The `FilterText` bindable property must still change on every keystroke so bindings and the text field remain responsive.

Do **not** debounce the property itself.

Debounce only:

```text
RefreshDataView(...)
```

triggered by a `FilterText` change.

### 3. Keep lightweight search UI state immediate

Today `HasSearchText` is updated during `RefreshDataView`.

With debounce, that would make the Clear button and search visibility lag behind typing.

Refactor so a `FilterText` change immediately updates lightweight state such as:

```text
HasSearchText
ClearSearchCommand.CanExecute
search-bar visibility when it depends on HasSearchText
```

Then schedule the expensive filtered view refresh.

### 4. Latest text wins

Typing:

```text
a
ar
arm
armi
arming
```

within the delay window must result in one final filtering operation for:

```text
arming
```

Older scheduled searches must not execute after a newer value has arrived.

Use cancellation or a monotonic generation/version.

Do not use `Thread.Sleep`.

### 5. Clear search should feel immediate

Executing `ClearSearchCommand` should:

- cancel any pending delayed filter;
- set `FilterText` to empty;
- restore the unfiltered view immediately.

The user should not have to wait 250 ms after pressing Clear.

Likewise, if deleting the final character produces an empty/whitespace search, applying the unfiltered view immediately is preferred.

### 6. Other filter-setting changes remain deterministic

Changes to:

```text
FilterPredicate
FilterMemberPaths
FilterStringComparison
```

are not keyboard text entry and should not be delayed unnecessarily.

When one of these settings changes:

- cancel any pending text debounce;
- re-evaluate the current filter deterministically.

Do not let an older delayed text refresh run afterward.

### 7. Respect control lifecycle

Cancel pending debounce work when the control loses its handler/detaches or enters any final visual-resource release path.

A delayed continuation must never update a detached/disposed native view.

When a handler is restored, the normal rebuild should use the current `FilterText`.

Do not introduce a timer/task leak for each grid instance.

### 8. UI-thread safety

`RefreshDataView` writes MAUI bindable/visual state.

Any delayed continuation must return through the control's dispatcher before mutating UI state.

Avoid capturing a page/ViewModel strongly from a long-lived static timer.

### 9. Small companion optimization from VDG-04

While touching the filtering path, remove the obvious repeated per-row parsing work:

Current matching effectively reparses:

```text
FilterMemberPaths
```

for every row.

At minimum:

- parse/cache member paths once when `FilterMemberPaths` changes or once per data-view refresh;
- normalize/trim `FilterText` once per refresh;
- reuse the existing accessor cache rather than recreating accessors.

Do not turn this task into a broad filtering-engine rewrite.

### 10. Do not change filtering semantics

Preserve:

- `FilterPredicate` behavior;
- `FilterStringComparison`;
- nested `FilterMemberPaths`;
- paging semantics;
- empty-state behavior;
- custom `SearchView` / `SearchTemplate`;
- existing `FilterText` two-way binding.

This task changes **when** local text filtering runs, not what matches.

---

## Implementation guidance

A simple instance-owned design is preferred.

Possible state:

```text
CancellationTokenSource? filterTextDebounceCancellation
long filterTextDebounceVersion
```

or an equivalent dispatcher timer.

Pseudo-flow:

```text
FilterText changed
    -> update HasSearchText immediately
    -> cancel previous pending search
    -> if empty OR SearchDelayMilliseconds == 0:
           RefreshDataView(...)
       else:
           wait configured delay
           dispatch to UI
           confirm generation/value still current
           RefreshDataView(...)
```

For non-text filter settings:

```text
cancel pending search
RefreshDataView(...)
```

Keep the implementation private to the control.

---

## Tests

Extend the existing VirtualizedDataGrid tests.

Required coverage:

1. `SearchDelayMilliseconds = 0` keeps immediate filtering.
2. Default/nonzero delay does not immediately rebuild the filtered result.
3. Rapid changes use the latest text.
4. A typing burst results in one final expensive refresh if a suitable internal diagnostic/test seam exists.
5. Clear cancels a pending search and restores all rows immediately.
6. Empty/whitespace text restores the unfiltered view immediately.
7. Changing `FilterMemberPaths` while a text debounce is pending cancels the old work and applies current semantics.
8. Changing `FilterStringComparison` while pending behaves deterministically.
9. Existing custom `SearchTemplate` behavior still passes.
10. Search stays visible when the final filter has zero matches.
11. Detach/handler loss with pending debounce does not throw or update after detach.

Do not make normal tests sleep for long periods.

If the current test environment does not provide a fake time/dispatcher mechanism, keep delay values tiny and bounded in async tests, or introduce a minimal internal delay seam only if it materially improves determinism.

Do not introduce a general timing framework.

---

## Samples / documentation

Update the VirtualizedDataGrid README/sample to show the property, for example:

```xml
<virtualized:VirtualizedDataGrid
    ShowSearchBar="True"
    SearchDelayMilliseconds="250"
    FilterMemberPaths="Name,Description" />
```

Mention:

- default delay;
- `0` for immediate filtering;
- delay applies only to text search, not arbitrary source changes.

---

## Acceptance criteria

The task is complete when:

- typing into the default or custom search field no longer performs a full local filter scan for every character;
- the UI text and Clear state still react immediately;
- the latest query always wins;
- Clear is immediate;
- pending work is cancelled safely on lifecycle changes;
- existing filter semantics are unchanged;
- `FilterMemberPaths` is no longer reparsed for every row;
- tests cover debounce, clear, latest-value and lifecycle behavior;
- the VirtualizedDataGrid project/tests build and pass.
