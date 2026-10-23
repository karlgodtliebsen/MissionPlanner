# Codex Task 01 — Actions Correctness Baseline

## Goal

Fix two known correctness/design defects in the existing FlightData Actions implementation before adding new functionality.

This task must **not** add new Actions features.

## Background

Current relevant UI/ViewModel files include:

- `src/UI/AvaloniaUI/MissionPlanner.AvaloniaUI.App/Views/FlightData/Tabs/ActionsTabView.axaml`
- `src/UI/AvaloniaUI/MissionPlanner.AvaloniaUI.App/Views/FlightData/Tabs/ActionsTabViewModel.cs`

Two issues have already been identified:

1. In the Expert MAV CMD section, the control labelled **Command ID** is currently bound to `TakeoffAltitudeMeters`. The ViewModel's expert-command execution uses `ExpertCommandId`, so the UI edits the wrong property.
2. The ViewModel currently derives a shared `CanInFlightAction` from `VehicleAction.Land` and uses it for **Land**, **Hold**, and **RTL**. These are semantically separate actions and must have independent policy/capability evaluation.

## Required work

### 1. Fix Expert Command ID binding

Change the XAML binding for the Expert MAV CMD **Command ID** editor to the correct ViewModel property:

- from `TakeoffAltitudeMeters`
- to `ExpertCommandId`

Confirm the takeoff altitude editor remains bound only to `TakeoffAltitudeMeters`.

### 2. Split in-flight action capability properties

Replace the shared capability used by Land/Hold/RTL with distinct properties, using naming consistent with the existing codebase, for example:

- `CanLand`
- `CanHoldPosition`
- `CanReturnToLaunch`

Each property must be based on the corresponding `VehicleAction` policy entry, not on `VehicleAction.Land` as a proxy.

If the `VehicleAction` model/policy currently lacks separate entries for Hold or RTL, add explicit actions and policy handling rather than retaining a shared surrogate.

Update:

- XAML `IsEnabled`/command gating as applicable
- ViewModel CanExecute handling
- policy/state refresh logic
- property-change notifications
- tests

### 3. Verify command isolation remains intact

Inspect how `ActionsTabViewModel` obtains the selected vehicle command service/session. Verify this task does not introduce any static/global command target or cross-vehicle dispatch behavior.

No architectural rewrite is required if the existing implementation is already correct.

## Out of scope

Do not add:

- Set Home Alt
- mission intervention commands
- speed/altitude/loiter adjustments
- new layout sections

Do not redesign Expert MAV CMD beyond fixing its binding.

## Acceptance tests

Automated tests must demonstrate at least the following:

1. Editing/setting `ExpertCommandId` results in `SendExpertCommandAsync` receiving that command ID.
2. Changing takeoff altitude does not alter `ExpertCommandId`.
3. The Land command is gated by the Land action policy.
4. The Hold command is gated independently by the Hold action policy.
5. The RTL command is gated independently by the RTL action policy.
6. A policy state in which Land is denied but RTL is allowed results in Land disabled and RTL enabled.
7. A policy state in which RTL is denied but Hold is allowed results in RTL disabled and Hold enabled.
8. Existing Arm, Disarm, Set Mode, Takeoff, Set Home Here, and Reboot behavior remains unchanged.

Where direct XAML binding tests are not part of the current test strategy, verify the binding by an appropriate UI/ViewModel test or static XAML assertion consistent with the repository's established testing approach.

## Build/test gate

Before completion:

- build the affected solution/projects;
- run all relevant Actions/ViewModel/Core command-policy tests;
- run any broader existing test suite normally used for these projects if practical.

Report exact commands and results.
