# Flight Data 07 — Safe cross-platform vehicle automation scripts

Status: **Completed.**

## Objective

Implement `ScriptsTabView` as a constrained, auditable local automation facility. Do not reproduce the legacy unrestricted IronPython model inside the MAUI process.

Apply all constraints from `00-README.md`.

## Required architecture decision

Add an ADR comparing:

```text
unrestricted embedded C#/Python/Lua
out-of-process scripting
constrained declarative action scripts
onboard ArduPilot Lua management
```

For this task, implement a constrained declarative MissionPlanner script format. Do not execute arbitrary code in-process.

## Script format and engine

Use versioned JSON initially unless the repository already has a reviewed safe YAML dependency.

Add:

```text
VehicleScriptDocument
VehicleScriptStep hierarchy
VehicleScriptValidationResult
VehicleScriptExecutionState
VehicleScriptStepResult
IVehicleScriptParser
IVehicleScriptValidator
IVehicleScriptExecutor
IVehicleScriptActionRegistry
```

The engine must:

1. parse and validate schema/version;
2. resolve steps through an allow-listed registry;
3. validate the complete script before execution;
4. support dry run;
5. execute only through existing typed services;
6. execute sequentially unless a future explicit parallel construct is reviewed;
7. give each step a timeout;
8. use one linked cancellation token;
9. stop on failure by default;
10. produce a complete execution log;
11. acquire the vehicle operation gate for vehicle-changing scripts;
12. revalidate active vehicle/session before every vehicle step.

## Initial allow-listed steps

```text
notify
delay
waitFor connection/state condition
set mode
arm/disarm through existing policy
takeoff/land/RTL/hold
set servo/relay after task 05
aux function after task 06
camera/gimbal after task 08
```

Unavailable actions must fail validation clearly.

Do not allow arbitrary command IDs, file/network/process access, reflection, dynamic compilation, unbounded loops or unbounded waits. A repeat construct must have strict maximum count/time.

## Storage and UI

Use MAUI file/storage abstractions. Support select/import, validate, dry run, run, cancel, save copy and recent scripts. Provide selected file, validation summary, step list, progress/current step, bounded timestamped output and copy/export log. Use atomic saves and sanitized names.

A built-in editor is optional; do not delay the core safe execution workflow for a rich editor.

## Onboard ArduPilot Lua boundary

Document onboard Lua upload/management as future work. Do not claim it is implemented until MAVFTP upload and filesystem mutations exist and are reviewed. Current MAVFTP listing/download is insufficient.

## Tests

Cover schema/versioning, all allow-listed steps, forbidden operations, timeout/cancellation, operation-gate conflict, replay denial, vehicle switch/disconnect between steps, trustworthy dry run, bounded repeat, execution-log ordering, path sanitization and ViewModel lifecycle.

## Documentation

- Add Scripts architecture, format and examples to `docs/FLIGHT_DATA.md`.
- Update `docs/FEATURES.md`.
- Add the ADR to `docs/ARCHITECTURE_DECISION_RECORDS.md`.
- Add a script-format/schema document under `docs/`.
- Update `docs/MAVFTP.md` with the onboard Lua boundary.

## Acceptance criteria

- A validated script can dry-run, execute and cancel.
- No unrestricted code executes inside MissionPlanner.
- Every vehicle-changing step uses typed safety-aware services.
- The execution log explains every step and failure.
