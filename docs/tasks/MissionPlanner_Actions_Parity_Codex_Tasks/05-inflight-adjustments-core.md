# Codex Task 05 — In-Flight Adjustment Core Commands

## Goal

Add typed backend support for the useful legacy MissionPlanner in-flight adjustment operations:

- **Change Speed**
- **Change Altitude**
- **Set Loiter Radius**

This task is backend/Core/Application only. UI is Task 06.

## Investigation required before coding

Trace both the legacy MissionPlanner implementation and the current MAVLink/ArduPilot semantics for all three operations.

Do not infer semantics solely from button labels.

### Change Speed

Verify:

- which MAVLink command/message is used;
- speed type expected by legacy MissionPlanner for Copter/Plane/Rover where applicable;
- units;
- whether throttle/absolute-relative fields are populated;
- persistence and mode limitations.

`MAV_CMD_DO_CHANGE_SPEED` is a likely mechanism but must be confirmed and parameterized correctly rather than copied from memory.

### Change Altitude

This is the most important semantic investigation in this task.

Determine:

- how legacy MissionPlanner changes altitude while flying;
- whether it modifies current navigation target, uses a reposition command, changes mission state, or uses another mechanism;
- altitude reference/frame: relative-to-home, AMSL, terrain, etc.;
- vehicle/mode restrictions;
- whether the user-entered value is an absolute target or a delta.

The new API must make the altitude reference explicit enough that callers cannot accidentally mix frames.

### Set Loiter Radius

Determine:

- units;
- accepted range;
- whether sign encodes direction in the legacy/protocol behavior;
- vehicle-type applicability;
- whether it is a temporary operational command or a parameter change.

Do not silently convert it into a persistent parameter write unless that is the proven legacy behavior.

## Required architecture

Add typed request/value models where they improve semantic safety, especially for altitude reference and speed type.

Conceptually, the future UI should be able to call operations such as:

- `ChangeSpeedAsync(...)`
- `ChangeAltitudeAsync(...)`
- `SetLoiterRadiusAsync(...)`

without knowing MAVLink command IDs or parameter positions.

Use existing command-service/session infrastructure and existing MAVLink command encoding.

## Validation and policy

Add independent policy/capability entries and validation for all three operations.

Validation should include proven constraints such as:

- finite numeric values;
- sensible/protocol-valid ranges;
- required flight mode/state;
- supported vehicle type;
- altitude frame/reference availability;
- connected selected vehicle.

Do not impose arbitrary product limits where protocol/firmware already defines the valid range unless the application has an established safety rule.

## Status/ACK behavior

Integrate with the existing command-status mechanism.

- Track pending state.
- Consume real MAVLink ACK when applicable.
- Use telemetry confirmation where a reliable confirmation signal exists.
- If confirmation cannot be reliably inferred, report accepted ACK rather than claiming telemetry confirmation.
- Propagate rejection, timeout, cancellation, and unsupported behavior clearly.

## Out of scope

- No new Actions XAML controls.
- No permanent parameter-editor functionality.
- No diagnostics/tool relocation.

## Acceptance tests

### Change Speed

1. Correct command/message and parameter encoding for the supported primary vehicle type(s).
2. Units are correct and covered by tests.
3. Invalid/NaN/infinite values are rejected before send.
4. Unsupported state/vehicle is policy denied or returns explicit unsupported behavior.

### Change Altitude

5. The implementation matches proven legacy behavior.
6. Altitude reference/frame is explicit and tested.
7. Tests distinguish at least the relevant absolute/reference semantics so a future regression cannot reinterpret the value as a delta or wrong frame.
8. Invalid altitude/reference combinations are rejected before transmission.

### Set Loiter Radius

9. Correct units and encoding are tested.
10. Direction/sign semantics, if applicable, are tested.
11. The operation does not unexpectedly persist a parameter unless legacy behavior proves that persistence is intended.

### Common

12. All operations are independently policy-gated.
13. Commands target the selected vehicle/session only.
14. ACK/rejection/timeout/cancellation paths update command status correctly.
15. Existing command APIs remain backward compatible unless a deliberate compile-time migration is documented.

## Build/test gate

Build affected projects and run relevant Core/MAVLink/command-policy tests. Report exact commands and results.
