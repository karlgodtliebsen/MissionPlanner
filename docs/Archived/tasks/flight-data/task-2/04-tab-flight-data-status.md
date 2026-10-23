# Flight Data 04 — Comprehensive promoted-telemetry Status tab

Status: **Completed.**

## Objective

Implement `StatusTabView` as a searchable, grouped, virtualized view of all promoted vehicle telemetry and diagnostics.

Dependency: complete task 02 and reuse its telemetry descriptor catalog.

Apply all constraints from `00-README.md`.

## Scope boundary

The Status tab presents promoted domain state. It must not decode MAVLink, reflect over generated wire records, or become a packet inspector. Raw packet inspection remains a separate diagnostics feature.

## Presentation model

Add models such as:

```text
StatusTelemetryItemViewModel
StatusTelemetryGroup
StatusFilter
```

Each row exposes stable descriptor key, category, label, formatted value, raw SI/domain value, unit, availability, freshness, observed time, optional warning and source description.

Use the task-02 descriptor/projector for all current state areas:

```text
Identity, Connection, Flight, Position, Motion, Gps, Power,
Radio, Navigation, Health, Estimator, Vibration, Pressure,
Range, Environment, Time and promoted servo/actuator state
```

Add explicit descriptors for uncovered promoted fields; do not use reflection fallback.

## UI

Use `VirtualizedDataGrid` with:

```text
search template
category filter
fresh/stale/unavailable filter
warning-only filter
stable sorting
copy selected value
copy/export full snapshot
compact/detailed mode
```

Recommended columns: Category, Name, Value, Unit, Freshness, Observed, Source.

## Update behavior

1. Create the row collection once per active vehicle/catalog.
2. Update row objects in place.
3. Do not clear/recreate the collection for every `VehicleStateUpdated`.
4. Coalesce UI publication at `PlannerTelemetrySettings.DisplayRateHz`.
5. Preserve search, sort and scroll state.
6. Never mutate the bound collection from `Dispose()`.

## Export

Export versioned JSON with capture timestamp, vehicle/firmware identity, descriptor key, raw and formatted values, unit, observed timestamp and freshness. Exclude secrets and transport credentials.

## Tests

Cover unique descriptor coverage, stable ordering, combined filters, stale/fresh/unavailable transitions, in-place updates, bounded notification rate, export schema, vehicle switching, disposal and VirtualizedDataGrid source stability.

## Documentation

- Add Status coverage/filter/export details to `docs/FLIGHT_DATA.md`.
- Update `docs/FEATURES.md`.
- Update promotion documentation only when missing values require new domain promotion.

## Acceptance criteria

- All available promoted state is searchable and filterable.
- High-rate telemetry does not rebuild the complete grid.
- Stale/unavailable values are explicit.
- No MAVLink decoding occurs in the UI.
