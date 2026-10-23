# Codex Task 03 — Mission Intervention Core Commands

## Goal

Add typed, policy-controlled backend support for the missing live mission-intervention operations used by legacy MissionPlanner:

- **Set Current Waypoint**
- **Restart Mission**
- **Resume Mission**
- **Abort Landing**

This task is **Core/Application/backend only**. Do not add the new UI controls yet.

## Investigation required before coding

For each operation, trace the legacy MissionPlanner implementation and current ArduPilot/MAVLink semantics before writing code.

Particular care is required for:

### Set Current Waypoint

Determine the exact message/command semantics used to make a mission item current and how mission sequence numbers are validated.

### Restart Mission

Determine whether legacy behavior consists of:

- setting mission current item to the first executable item;
- changing to AUTO;
- issuing `MAV_CMD_MISSION_START`;
- or a defined sequence of operations.

Preserve proven semantics rather than reducing it to a guessed single command.

### Resume Mission

Legacy MissionPlanner's Resume Mission behavior may contain state/waypoint logic beyond simply setting AUTO mode. Trace and preserve that behavior where it still makes sense in the NextGen architecture.

### Abort Landing

Determine the correct command/mode and vehicle-specific behavior. Do not assume all vehicle classes implement landing abort identically.

## Required architecture

Add explicit typed operations to the appropriate command/mission service abstraction. Use existing naming/conventions and existing mission services where they provide a better architectural home than `IVehicleCommandService`.

The ViewModel must eventually be able to invoke strongly typed methods such as the conceptual equivalents of:

- `SetCurrentMissionItemAsync(sequence, cancellationToken)`
- `RestartMissionAsync(cancellationToken)`
- `ResumeMissionAsync(cancellationToken)`
- `AbortLandingAsync(cancellationToken)`

These names are examples, not mandatory API names.

Do not expose raw command IDs or raw parameter arrays to the future UI.

## Policy and state rules

Add independent policy/capability entries for each operation.

At minimum account for:

- disconnected vehicle;
- no mission loaded/known where relevant;
- invalid mission sequence;
- inappropriate vehicle state/mode;
- armed/disarmed restrictions where ArduPilot semantics require them;
- command already pending where existing command serialization rules apply;
- selected vehicle/session identity.

Do not invent restrictions merely to be conservative; derive them from existing policy conventions and command requirements.

## Command lifecycle

Use the existing command-status infrastructure:

- command pending state;
- MAVLink ACK when the underlying protocol provides one;
- telemetry/state confirmation where meaningful;
- explicit error/timeout/cancellation reporting.

For multi-step operations such as Restart or Resume, report meaningful failure if an intermediate step fails. Do not report success after only the first step.

## Out of scope

- No XAML changes for the new mission controls.
- No in-flight speed/altitude/loiter adjustments.
- No joystick/diagnostic/map utility work.

## Acceptance tests

Add automated tests for all four operations. At minimum:

### Set Current Waypoint

1. Valid sequence produces the correct mission-current protocol action.
2. Invalid/out-of-range sequence is rejected before transmission when mission bounds are known.
3. A command targets only the selected vehicle.
4. Timeout/cancellation propagates correctly.

### Restart Mission

5. The exact proven restart sequence is executed in order.
6. Failure of any step prevents false success reporting.
7. Policy denies restart in states where it is not valid.

### Resume Mission

8. Resume reproduces the proven legacy/state-aware behavior rather than being implemented as an unconditional SetMode(AUTO).
9. Required prior/current mission item information is handled correctly.
10. Missing prerequisite mission state results in a useful failure/disabled policy rather than guessing.

### Abort Landing

11. The correct vehicle-supported abort mechanism is used.
12. Abort Landing is denied/disabled when not applicable.
13. Unsupported vehicle/firmware behavior produces an explicit unsupported result rather than sending an arbitrary command.

### Regression

14. Existing command-service and mission tests remain green.
15. Existing Arm/Disarm/Mode/RTL/Land/Hold/Takeoff behavior is unchanged.

## Build/test gate

Build affected projects and run relevant Core/MAVLink/mission tests. Report exact commands and results.
