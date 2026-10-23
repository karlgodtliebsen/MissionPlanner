# MissionPlanner Next Generation — FlightData Actions Parity, Wave 2

This package is the follow-up to the first FlightData Actions parity task set.

The first task set correctly stopped rather than guessing when the legacy implementation exposed contradictory or unsafe semantics. Those investigations are now considered complete. This Wave 2 package turns the findings into explicit product and architecture decisions so Codex can implement the missing functionality without reproducing legacy defects.

## Starting point

Run these tasks against the repository state **after** the previous seven Actions parity commits. Do not revert or bypass the fixes already made there, especially:

- the Expert MAV CMD `Command ID` binding fix;
- independent Land / Hold / RTL policy gating;
- the existing Actions parity documentation and investigation findings.

Before editing, Codex must inspect the current repository because file/type names may have changed after the previous work. Use the existing architecture and naming conventions rather than forcing the example names in these tasks.

## Product decisions — these are requirements, not open questions

### 1. Replace legacy “Set Home Alt” with a local display operation

NextGen will **not** reproduce the contradictory legacy arithmetic and will not call this operation `Set Home Alt`.

The operator feature is **Zero Altitude** / **Reset Altitude** and is GCS-local only:

```text
zeroReference = current RelativeAltitudeMeters
DisplayedAltitude = RelativeAltitudeMeters - zeroReference
```

When the reference is reset, display returns to the normal relative-altitude path.

It must never modify vehicle HOME and must never manufacture a MAVLink ACK.

### 2. Mission execution state is first-class vehicle state

Retain the complete useful `MISSION_CURRENT` execution fields, including:

- current sequence;
- total mission items;
- mission state;
- mission mode;
- mission ID.

A downloaded onboard mission snapshot is considered **verified current** only when a non-zero mission/opaque ID from the snapshot matches the current streamed mission ID. If mission IDs are unsupported (`0`), do not claim ID-verified freshness.

### 3. Set Current WP uses the modern mission-current command

Primary path:

```text
MAV_CMD_DO_SET_MISSION_CURRENT
param1 = requested sequence
param2 = 0
```

Require the command ACK and a **post-request** matching `MISSION_CURRENT` when the primary command is supported.

An explicit `MAV_RESULT_UNSUPPORTED` may fall back to superseded `MISSION_SET_CURRENT`, followed by a post-request matching `MISSION_CURRENT`. Do not fall back merely because an ACK timed out.

### 4. Restart Mission uses reset semantics

NextGen defines **Restart Mission** as:

```text
MAV_CMD_DO_SET_MISSION_CURRENT
param1 = 0
param2 = 1   // reset mission
```

This deliberately differs from the old implementation by resetting jump counters/completed mission state. It must **not** arm, switch to AUTO, issue `MISSION_START`, or start motors.

No legacy fallback is allowed if reset semantics are unsupported.

### 5. Resume Mission uses MAVLink pause/continue semantics

NextGen will **not** reproduce the legacy destructive mission rewrite / upload / arm / takeoff / AUTO choreography.

**Resume Mission** means:

```text
MAV_CMD_DO_PAUSE_CONTINUE
param1 = 1
```

It resumes the onboard mission from the autopilot's current mission execution position. It does not arm, take off, rewrite the mission, upload a mission, or silently change mode.

### 6. Abort Landing is Plane-only and strongly gated

Enable only when NextGen can establish all required preconditions, including:

- Plane vehicle family;
- AUTO mode;
- active/appropriate mission execution state;
- a **verified current** onboard mission snapshot;
- the current mission item is `MAV_CMD_NAV_LAND`;
- the ArduPilot landing-abort feature is configured (`LAND_ABORT_THR` known and enabled).

Send typed `MAV_CMD_DO_GO_AROUND`. Do not expose the raw command to the UI.

### 7. Change Speed is vehicle-family aware

Introduce a semantic speed target type, not a raw MAVLink enum.

Initial supported UI semantics:

- Copter: Ground speed only.
- Rover: Ground speed only.
- Plane: Airspeed or Ground speed selector.

Do not hide throttle adjustment inside Change Speed. Throttle remains outside this task set.

### 8. Change Altitude is a GUIDED target, not the legacy mission-item hack

UI semantics are always:

> Target altitude above HOME

This is an **absolute HOME-relative target**, not a delta.

The typed backend may use vehicle-specific ArduPilot guided mechanisms, but it must not use the old `MISSION_ITEM current=3` trick and it must not silently switch the vehicle into GUIDED mode.

### 9. Loiter Radius is a persistent parameter operation

Use the typed parameter subsystem. Prefer `WP_LOITER_RAD`, with `LOITER_RAD` only as a compatibility fallback when that is the actual available parameter.

The UI accepts a positive radius magnitude. Preserve the current parameter sign so changing radius does not unexpectedly reverse loiter direction. If the current value is zero, use positive direction when applying a non-zero magnitude.

The UI must identify this as a persistent vehicle parameter change.

## Global implementation rules

These rules apply to every task:

1. **No raw MAVLink construction in XAML or ViewModels.** Operator actions use typed Core/Application abstractions.
2. **Expert MAV CMD remains expert-only.** Do not use it behind normal Actions controls.
3. **Do not manufacture acknowledgements.** Distinguish command ACK, mission telemetry confirmation, parameter confirmation, local-GCS application, and cases where confirmation is unavailable.
4. **Use post-request observations for confirmation.** A telemetry sample that existed before an operation started must never satisfy that operation.
5. **Serialize/correlate operations per vehicle where required.** Concurrent same-command operations must not consume one another's ACK/telemetry.
6. **Preserve selected-vehicle isolation.** No global/static command target.
7. **Cancellation and timeout are required.** No fire-and-forget operator actions.
8. **Policy/capability gating remains independent per semantic action.**
9. **Do not silently change flight mode** unless a task explicitly says to do so. None of the new Wave 2 operations are allowed to change mode implicitly.
10. **Use SI internally.** Reuse existing UI unit-conversion infrastructure if present rather than inventing a second unit system.
11. **Keep the NextGen visual hierarchy.** Core flight controls remain prominent; mission intervention and in-flight adjustments remain secondary/compact.
12. **Small coherent changes.** Avoid unrelated refactors.
13. **Every behavior change requires tests.**
14. **Build and run relevant tests before completion.**
15. If a concrete protocol implementation is impossible because the current library is missing required MAVLink definitions, add the missing typed message/enum/encoder/decoder support in the MAVLink layer rather than leaking protocol details upward.

## Recommended execution order

Run one task at a time in this order:

1. `01-mission-execution-state-and-snapshot.md`
2. `02-zero-display-altitude.md`
3. `03-mission-intervention-backend.md`
4. `04-mission-intervention-ui.md`
5. `05-inflight-adjustments-backend.md`
6. `06-inflight-adjustments-ui.md`
7. `07-final-actions-parity-hardening.md`

Do not skip Task 01: Tasks 03 and 04 depend on verified mission execution/snapshot state.

## Protocol/reference anchors

Use current source and official documentation as the implementation authority:

- MAVLink common messages/commands: `https://mavlink.io/en/messages/common.html`
- MAVLink mission protocol: `https://mavlink.io/en/services/mission.html`
- ArduPilot mission upload/download and pause/continue: `https://ardupilot.org/dev/docs/mavlink-mission-upload-download.html`
- ArduPilot Plane abort landing: `https://ardupilot.org/plane/docs/aborting-autolanding.html`
- ArduPilot Copter Guided commands: `https://ardupilot.org/dev/docs/copter-commands-in-guided-mode.html`
- ArduPilot Plane Guided commands: `https://ardupilot.org/dev/docs/plane-commands-in-guided-mode.html`
- ArduPilot Copter mission commands: `https://ardupilot.org/copter/docs/common-mavlink-mission-command-messages-mav_cmd.html`
- ArduPilot Plane mission commands: `https://ardupilot.org/plane/docs/common-mavlink-mission-command-messages-mav_cmd.html`
- ArduPilot Plane Loiter mode / `WP_LOITER_RAD`: `https://ardupilot.org/plane/docs/loiter-mode.html`

## Completion report required for every task

Codex must report:

- behavior implemented;
- files changed;
- protocol/application design choices made within the fixed semantics above;
- tests added/updated;
- exact build/test commands and results;
- any remaining blocker that prevents an acceptance criterion from being met.

Do not report a deferred task as complete. If a genuine blocker remains, make the blocker explicit and leave the feature unavailable rather than substituting different semantics.
