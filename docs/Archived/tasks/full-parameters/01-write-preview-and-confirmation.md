# Full Parameters 01 — Write preview and confirmation

## Objective

Turn `Apply modified` into a reviewable write workflow. Show exact changes and require explicit confirmation before any parameter is sent.


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

`FullParametersListTabViewModel.WriteParametersAsync` calls `editSession.ApplyAsync` directly. Core validates and confirms by readback, but UI has no preflight preview.

## Requirements

1. Add a write-plan projection with name, display name, live value, pending value, units, difference, reboot flag, validation and read-only state.
2. Create a stable plan snapshot from the current edit session.
3. Reject plan creation/execution when session is stale, vehicle scope changed, no changes exist, or targets are invalid/read-only.
4. Show a modal preview with old/new values and reboot count.
5. Require explicit `Write N parameters` confirmation.
6. Recheck scope and values after confirmation before `ApplyAsync`.
7. Design for subset selection; implement it now if practical.
8. Keep cancellation and connection cancellation functional.
9. Log plan created/confirmed/cancelled/stale without excessive metadata.

## Tests

- Only modified fields are planned.
- Values and reboot flags are correct.
- Invalid/read-only fields block execution.
- Scope change invalidates a preview.
- Cancellation sends no writes.
- Confirmation applies exactly planned names.

## Acceptance criteria

- No write starts without confirmation.
- Exact old/new values are visible.
- A stale preview cannot execute.
