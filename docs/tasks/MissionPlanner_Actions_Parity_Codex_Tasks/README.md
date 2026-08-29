# MissionPlanner Next Generation — FlightData Actions Parity

This package contains a sequenced set of Codex tasks for improving the **FlightData > Actions** feature in MissionPlanner Next Generation while preserving the cleaner NextGen interaction model.

## Objective

Reach deliberate functional parity with the useful operational capabilities of legacy MissionPlanner without recreating the legacy Actions tab as a dense button matrix.

The intended result is:

- Keep the existing NextGen core flight controls and command-status model.
- Fix known correctness problems first.
- Add the missing **Set Home Alt** capability with legacy-compatible semantics.
- Add missing **mission intervention** operations.
- Add missing **in-flight adjustment** operations.
- Keep diagnostics/tools out of Actions when they belong elsewhere.
- Preserve typed command APIs, vehicle-action policy enforcement, MAVLink acknowledgement/status handling, cancellation, and selected-vehicle isolation.

## Recommended execution order

Run these Codex tasks **one at a time, in order**:

1. `01-actions-correctness-baseline.md`
2. `02-set-home-alt-parity.md`
3. `03-mission-intervention-core.md`
4. `04-mission-intervention-ui.md`
5. `05-inflight-adjustments-core.md`
6. `06-inflight-adjustments-ui.md`
7. `07-parity-hardening-tests-docs.md`

Tasks 3 and 5 could theoretically be developed independently after Task 1, but sequential execution is recommended because they are likely to touch shared command-service and vehicle-action-policy code.

## Repository

Target repository:

`karlgodtliebsen/MissionPlanner`

Use the current `main` branch as the starting point unless the repository's normal workflow says otherwise.

## Known current implementation

The current Actions implementation is primarily under:

- `src/UI/MissionPlanner.App/Views/FlightData/Tabs/ActionsTabView.xaml`
- `src/UI/MissionPlanner.App/Views/FlightData/Tabs/ActionsTabViewModel.cs`
- `src/Core/MissionPlanner.Core/Commands/IVehicleCommandService.cs`

Codex must locate the current concrete command-service implementation, vehicle-action policy types, MAVLink command builders/encoders, mission state/services, and relevant tests before editing. Do not assume paths if they have moved.

## Global implementation rules

These rules apply to every task in this package:

1. **Do not send raw MAVLink from the UI or ViewModel.** Operator-facing commands must go through typed Core/Application command abstractions such as `IVehicleCommandService` or the appropriate existing service.
2. **Do not use Expert MAV CMD as the implementation for normal operator controls.** Expert command remains an advanced escape hatch only.
3. **Preserve the existing command lifecycle:** request → command sent → MAVLink ACK/status → telemetry confirmation where confirmation is meaningful and already supported.
4. **Use cancellation and async APIs.** No fire-and-forget command execution.
5. **Enforce vehicle/session isolation.** A command initiated for the selected/active vehicle must never be delivered to another connected vehicle.
6. **Use policy/capability gating.** Controls must be disabled when disconnected or when the action is not allowed for the current vehicle state.
7. **Do not guess safety-critical semantics.** If legacy MissionPlanner behavior cannot be established from source and protocol behavior, stop that subfeature and document the unresolved semantic question rather than inventing behavior.
8. **Preserve the NextGen UI language.** Do not reproduce the old MissionPlanner green-button grid.
9. **Avoid broad refactors.** Make the smallest coherent architectural changes needed for the task.
10. **Add tests with every behavior change.** Use the existing test projects and conventions.
11. **Build and run relevant tests before declaring the task complete.** Do not leave new warnings introduced by the change.
12. **Final Codex report:** summarize behavior implemented, files changed, tests/build commands run and results, and any intentionally deferred ambiguity.

## Scope decisions already made

The following old MissionPlanner functions are **not** to be re-added to the Actions tab as part of this package:

- Joystick setup
- Raw Sensor View
- Clear Track
- Message utility
- Mount/Gimbal controls unless a later dedicated feature task determines no suitable location exists

Likewise, do not recreate the old generic **Do Action** menu. NextGen's **Expert MAV CMD** remains the advanced generic command mechanism after its correctness bug is fixed in Task 1.
