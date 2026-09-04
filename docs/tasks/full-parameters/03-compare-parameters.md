# Full Parameters 03 — Compare parameter sources

## Objective

Implement `Compare parameters` as a reusable comparison engine and virtualized comparison view.


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


## Modes

```text
live vs pending
live vs session original
live vs .param file
live vs JSON file
live vs saved profile (after task 04)
```

## Model

Include parameter name/display name, left/right source and value, difference, status, units, metadata, staging eligibility and message.

Statuses:

```text
Equal
Different
OnlyOnLeft
OnlyOnRight
InvalidRightValue
ReadOnly
MetadataMissing
```

## Requirements

1. Create one numeric-equivalence service reused by modified detection, comparison, imports and appropriate readback checks.
2. Prefer metadata increment/step precision; otherwise use documented absolute/relative tolerance.
3. Treat NaN/infinity explicitly.
4. Preserve file-only entries rather than dropping them.
5. Use only justified version-aware aliases.
6. Add filters for differences, missing, invalid, modified and all.
7. Allow selected safe right-side values to be staged into the edit session.
8. Staging must never write; normal confirmed write workflow remains required.
9. Add `Select all safe differences`.
10. Export comparison results to JSON/CSV.
11. Display source identity, firmware and timestamp.

## Tests

- Step-equivalent values compare equal.
- Real differences, missing sides and invalid values are classified correctly.
- Read-only values cannot be staged.
- Staging changes pending values but sends no write.
- Firmware mismatch warns.

## Acceptance criteria

- Compare button opens a functional, source-labelled workflow.
- Valid differences can be staged safely.
- Comparison never writes directly.
