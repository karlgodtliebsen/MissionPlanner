# Codex Task 02 — Implement Legacy-Compatible Set Home Alt

## Goal

Add a distinct **Set Home Alt** operation to MissionPlanner Next Generation with behavior compatible with legacy MissionPlanner.

This operation must remain clearly separate from the existing **Set Home Here** operation.

## Critical semantic constraint

**Do not implement Set Home Alt as an alias for `SetHomeHereAsync`.**

**Do not assume that `MAV_CMD_DO_SET_HOME` is the correct implementation.**

Legacy MissionPlanner's **Set Home Alt** and NextGen's **Set Home Here** have different purposes. Codex must establish the precise legacy behavior from the legacy MissionPlanner source before implementing it.

## Investigation required before coding

Locate the legacy MissionPlanner source implementation behind the FlightData Actions **Set Home Alt** control and trace it through all relevant helpers/state changes/MAVLink operations.

Document in the task completion report:

- what user-visible value/reference it changes;
- whether it sends a MAVLink command, changes a GCS-side altitude offset/reference, changes vehicle state, or performs multiple steps;
- which altitude frame/reference is involved;
- whether behavior differs by vehicle type or firmware;
- whether telemetry confirmation is possible/appropriate.

Also inspect the current NextGen altitude model to determine where the equivalent state belongs architecturally.

## Required implementation

Once semantics are proven:

1. Add an explicit typed operation for Set Home Alt in the appropriate Core/Application abstraction.
2. Add/update the corresponding action-policy capability (`VehicleAction` or equivalent).
3. Implement the operation through the existing selected vehicle/session infrastructure.
4. Integrate command/status reporting consistently with the existing Actions command-status area.
   - If the operation sends a MAVLink command, process ACK/status through the normal command path.
   - If it is intentionally a GCS-side reference/offset operation, report successful state application without manufacturing a MAVLink ACK.
5. Add a **Set Home Alt** control to Actions that is visually and semantically distinct from **Set Home Here**.
6. Use concise help/label text if necessary to make the distinction understandable.
7. Keep the NextGen layout clean; do not add a legacy-style grid.

## Safety/ambiguity rule

If the legacy semantics cannot be proven with sufficient confidence, do **not** substitute an approximation. Instead:

- leave the feature unimplemented;
- add a focused technical note describing the unresolved point;
- make the task fail explicitly rather than silently changing HOME position or altitude incorrectly.

## Acceptance tests

Automated tests must cover the actual proven semantics and at minimum demonstrate:

1. `Set Home Alt` invokes its own typed operation and does not call `SetHomeHereAsync`.
2. `Set Home Here` behavior remains unchanged.
3. The feature is disabled when disconnected or when its policy denies the action.
4. The operation applies to the currently selected vehicle/session only.
5. The correct altitude value/reference/frame is used according to the legacy behavior.
6. Success/failure is reflected correctly in command/status state.
7. Any ACK handling is based on a real command ACK; no fabricated acknowledgement is reported.
8. Existing Actions tests continue to pass.

## Manual verification checklist

Provide a short SITL verification procedure that allows a developer to distinguish:

- Set Home Here changing HOME position;
- Set Home Alt changing only the intended altitude/reference behavior.

Do not hard-code COM ports in production code or tests.

## Build/test gate

Build affected projects and run all related tests. Report exact commands and results.
