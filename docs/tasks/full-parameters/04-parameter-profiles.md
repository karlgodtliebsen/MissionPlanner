# Full Parameters 04 — Named parameter profiles

## Objective

Replace `Load Pre saved` with version-aware named profiles that stage values into the edit session.

## Dependency

Task 03.


## Repository constraints

- Work only in the new solution under `src/`, `docs/`, `scripts/` and its test data.
- Treat `src-v.1.38/` as read-only. Never modify it.
- Preserve layering: protocol in `MissionPlanner.MavLink`, transport in `MissionPlanner.Transport`, domain/application behavior in `MissionPlanner.Core`, MAUI presentation in `MissionPlanner.App`.
- Views and code-behind must not call MAVLink transports directly.
- Reuse `IParameterEditSession`, `ParameterEditSession`, `ParameterApplyReport`, `ParameterWriteResult`, active-vehicle context, registry, metadata and parameter services.
- Every vehicle operation must be cancellation-aware, connection-aware and scoped to current `VehicleId` and firmware identity.
- Do not mutate UI-bound collections from `Dispose()`.
- Add deterministic unit/view-model tests. SITL tests must be bounded and opt-in.
- Preserve loading, editing, import/export, filtering and virtualization.


## Profile model

Include ID, name, description, timestamps, firmware family/version scope, vehicle/frame constraints, source identity, values, tags and format version.

## Requirements

1. Add an injected profile repository.
2. Start with atomic local JSON storage, keeping persistence replaceable.
3. Create profiles from all live values, pending changes only, or selected subset.
4. Validate compatibility against current firmware and parameter presence.
5. Compare before staging.
6. Stage only compatible values.
7. Never write immediately after loading.
8. Preserve unknown entries as unsupported/missing.
9. Support rename, duplicate, delete, import and export.
10. Use schema versioning.

## Tests

- Round trip and atomic replacement.
- Firmware mismatch warning.
- Missing/renamed/read-only/invalid handling.
- Loading stages pending state but sends no write.

## Acceptance criteria

- A profile browser replaces the placeholder.
- Profiles are scoped and compatibility-checked.
- Load always leads to compare/stage before write.
