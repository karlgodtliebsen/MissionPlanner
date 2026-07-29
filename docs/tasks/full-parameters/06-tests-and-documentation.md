# Full Parameters 06 — Tests and documentation

## Objective

Complete deterministic coverage and documentation for load, edit, compare, profile, write, readback, retry and reboot behavior.

## Dependencies

Tasks 01–05.


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


## Test layers

### Core
- write-plan snapshot/stale detection;
- equivalence;
- comparison;
- profile persistence/compatibility;
- apply progress/partial results;
- retry selection;
- reboot aggregation.

### ViewModel
- command enablement;
- confirmation cancellation;
- progress/results;
- compare/profile staging;
- disposal without bound-collection mutation;
- disconnect/vehicle change.

### Protocol
- `PARAM_SET` encoding;
- readback confirmation;
- timeout/cancellation;
- duplicate/out-of-order `PARAM_VALUE`.

### Opt-in SITL
- load all;
- change one safe parameter;
- confirm readback;
- restore in `finally`;
- compare before/after;
- strict timeout.

Never run destructive real-hardware writes in CI.

## Documentation

Update `docs/PARAMETERS.md` with live/original/pending semantics, write sequence, readback, partial failure/retry, comparison sources, profiles, reboot behavior, disconnect/cancellation and bound-collection lifecycle.

Add troubleshooting for write rejected, readback timeout, stale session, firmware mismatch, missing metadata and absent parameters.

## Acceptance criteria

- Deterministic tests pass without hardware/network.
- SITL tests are bounded and opt-in.
- No Write/Compare/Profile placeholder remains.
