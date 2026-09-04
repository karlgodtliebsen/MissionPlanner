# Full Parameters 05 — Simplify grid and ViewModel ownership

## Objective

Remove duplicate filtering/paging and collection churn now that `VirtualizedDataGrid` owns those presentation concerns.


## Repository constraints

- Work only in the new solution under `src/`, `docs/`, `scripts/` and its test data.
- Treat `src-v.1.38/` as read-only. Never modify it.
- Preserve layering: protocol in `MissionPlanner.MavLink`, transport in `MissionPlanner.Transport`, domain/application behavior in `MissionPlanner.Core`, Avalonia presentation in `MissionPlanner.AvaloniaUI.App`.
- Views and code-behind must not call MAVLink transports directly.
- Reuse `IParameterEditSession`, `ParameterEditSession`, `ParameterApplyReport`, `ParameterWriteResult`, active-vehicle context, registry, metadata and parameter services.
- Every vehicle operation must be cancellation-aware, connection-aware and scoped to current `VehicleId` and firmware identity.
- Do not mutate UI-bound collections from `Dispose()`.
- Add deterministic unit/view-model tests. SITL tests must be bounded and opt-in.
- Preserve loading, editing, import/export, filtering and virtualization.


## Current state

The ViewModel holds `allParameterItems`, `filteredParameterItems`, `Parameters`, search/page counts and performs:

```csharp
Parameters.Clear();
Parameters.AddRange(filteredParameterItems);
```

The grid also supports filtering, counts and paging.

## Requirements

1. Choose one owner for filtering/paging.
2. Recommended: expose one stable all-row collection; grid owns UI filter/page; ViewModel owns modified and total-loaded counts.
3. Bind search template directly to grid `FilterText`.
4. Remove duplicated filtered list, filter method, page projection and redundant properties.
5. Replace loaded rows atomically while active (`ReplaceRange` or one deferred range reset).
6. Never clear bound collections from dispose/deactivation.
7. Update existing row objects when structure is unchanged.
8. Preserve row identity and sort order.
9. Keep compare/profile sources separate.
10. Document bound-collection lifecycle rules.

## Tests

- One final source replacement per load.
- N-to-N refresh renders correctly.
- Dispose does not mutate bound collection.
- New ViewModel receives fresh source.
- Zero-result search remains clearable.
- Modified count updates without rebuilding all rows.

## Acceptance criteria

- No duplicate filter/page pipeline.
- No avoidable empty/intermediate states.
- Repeated navigation remains stable.
