# Full Parameters 02 — Apply progress, results and failed-only retry

## Objective

Expose existing per-parameter outcomes and allow failed parameters to be retried without rewriting confirmed values.

## Dependency

Task 01.


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


## Current state

`ParameterEditSession.ApplyAsync` returns `ParameterApplyReport.Results` with outcomes: `Unchanged`, `Confirmed`, `ValidationFailed`, `WriteFailed`, `ReadbackFailed`, `Skipped`. The ViewModel reduces this to one sentence.

## Requirements

1. Add protocol-neutral progress: index, total, name, phase and message.
2. Keep sequential writes unless testing proves a safe bounded window.
3. Display cancellable progress.
4. Show result groups and counts by outcome.
5. Add `Retry failed`, passing only retryable names.
6. Never retry confirmed, unchanged, validation-failed-without-change, or stale/skipped fields.
7. Keep failed fields modified with write status/message.
8. Refresh ambiguous fields before retry when required.
9. Preserve reboot-required after any confirmed change.
10. Support copying/exporting a diagnostic summary.

## Tests

- Progress order matches targets.
- Cancellation returns coherent partial results.
- Retry excludes confirmed values.
- Disconnect invalidates session and skips remaining targets.
- Reboot state survives partial success/retry.

## Acceptance criteria

- Every attempted parameter has a visible outcome.
- Partial failure is actionable.
- Retry never rewrites confirmed values.
