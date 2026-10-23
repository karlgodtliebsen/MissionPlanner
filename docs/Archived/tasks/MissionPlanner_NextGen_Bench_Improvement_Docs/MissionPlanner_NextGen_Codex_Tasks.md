# MissionPlanner Next Gen — Codex Task Set from Omnibus F405 Bring-Up

Date: 2026-09-16

These tasks are intentionally scoped so they can be executed **one at a time**. Each task should preserve the existing architecture and naming conventions, avoid unrelated refactors, include automated tests where practical, and leave the solution building cleanly.

---

# Task 1 — Implement vehicle arming status and HUD feedback

## Objective

Add a first-class arming-status model and expose the same useful information that classic Mission Planner provided during the Omnibus F405 bring-up.

## Required behavior

The UI must show:

- `ARMED` / `DISARMED`
- `Ready to Arm` / `Not Ready to Arm`
- current retained `PreArm: ...` reason
- last relevant `Arm: ...` failure reason

## MAVLink inputs

Use existing decoding/dispatch infrastructure where possible.

Derive state from:

- `HEARTBEAT`
  - inspect `MAV_MODE_FLAG_SAFETY_ARMED`
- `SYS_STATUS`
  - inspect `MAV_SYS_STATUS_PREARM_CHECK` present/enabled/health bits as appropriate
- `STATUSTEXT`
  - capture messages beginning with `PreArm:`
  - capture messages beginning with `Arm:`

## Suggested domain model

```csharp
public enum VehicleArmingState
{
    Unknown,
    DisarmedNotReady,
    DisarmedReady,
    Armed
}

public sealed record VehicleArmingStatus(
    VehicleArmingState State,
    string? PreArmReason,
    string? LastArmFailure,
    DateTimeOffset UpdatedAt);
```

Adapt names to the current domain conventions if needed.

## Retention rules

- Keep the latest meaningful `PreArm:` reason while the vehicle is not ready.
- Keep the latest `Arm:` failure long enough for the user to see it.
- Clear stale pre-arm reason when the vehicle becomes ready.
- Clear/reset both on vehicle-session reset/disconnect.
- Armed state overrides ready/not-ready presentation.

## UI

Add the status prominently to the main flight/HUD view.

The display should remain readable without opening a log pane.

## Tests

Add tests for at least:

1. heartbeat disarmed + prearm unhealthy → `DisarmedNotReady`
2. heartbeat disarmed + prearm healthy → `DisarmedReady`
3. heartbeat armed → `Armed`
4. `STATUSTEXT "PreArm: Compass 1 not healthy"` is retained
5. later readiness clears stale pre-arm reason
6. `STATUSTEXT "Arm: Roll (RC1) is not neutral"` is exposed as last arm failure
7. disconnect/session reset clears retained state

## Acceptance criteria

- Project builds.
- Existing tests remain green.
- HUD visibly distinguishes armed, disarmed-ready, and disarmed-not-ready.
- A received `PreArm:` or `Arm:` text becomes visible without requiring a log viewer.

---

# Task 2 — Redesign RC calibration validation around used channels

## Objective

Fix RC calibration so unused advertised CRSF/ELRS channels do not cause calibration failure.

## Problem to solve

A receiver may expose 16 channels even when the transmitter has controls assigned to only a subset. Static unused channels must not invalidate calibration.

## Required channel classification

Classify channels as:

1. required primary control
2. used auxiliary channel
3. unused/unassigned channel

Primary channels normally include:

- Roll
- Pitch
- Throttle
- Yaw

Used auxiliary channels should be derived where possible from current configuration, including:

- `RCx_OPTION`
- flight-mode channel
- other known active RC mappings

## Calibration validation

Only require valid min/max movement for channels that actually require calibration.

Unused channels that remain at a fixed value must not block Save/Apply.

## UI

For each channel show:

```text
Current
Observed Min
Observed Center
Observed Max
Configured Min
Configured Trim
Configured Max
Configured Dead-zone
Classification
Validation state
```

## Tests

Add tests for:

1. CH1-CH5 move; CH6-CH16 static → calibration accepted when only CH1-CH5 are used
2. required Roll channel does not move → calibration rejected
3. unused CH12 does not move → calibration still accepted
4. used `RC8` flight-mode channel does not move → explicit warning/error
5. save writes only intended RC parameters

## Acceptance criteria

- Static unused channels no longer block calibration.
- Required/used channels still receive strict validation.
- UI explains why a channel is required or ignored.
- Existing parameter-write infrastructure is reused.

---

# Task 3 — Add RC neutral/trim diagnostics

## Objective

Detect and explain RC center mismatches that can cause ArduPilot arming failures such as:

```text
Arm: Roll (RC1) is not neutral
Arm: Pitch (RC2) is not neutral
Arm: Yaw (RC4) is not neutral
```

## Required calculations

For Roll, Pitch, and Yaw calculate:

```text
CenterError = ObservedCenter - RCx_TRIM
NeutralAllowed = abs(Current - RCx_TRIM) <= RCx_DZ
```

Also expose channel asymmetry around center where useful.

## UI behavior

Show a warning when observed center is materially outside configured trim/dead-zone.

Example:

```text
Yaw
Observed: 1076 / 1587 / 2011
Configured trim: 1500
Center error: +87 µs
Dead-zone: 20 µs
Status: Not neutral
```

Provide neutral wording that suggests possible causes:

- transmitter trim/subtrim
- mixer/input/output offset
- transmitter stick calibration
- stale `RCx_TRIM`

Do not automatically modify parameters without user confirmation.

## Tests

1. center 1512, trim 1500, DZ 20 → neutral
2. center 1587, trim 1500, DZ 20 → warning/not-neutral
3. updated trim 1587 → neutral
4. diagnostics update live as RC input changes

## Acceptance criteria

- User can identify which axis is outside neutral and by how much.
- UI differentiates observed live values from configured parameter values.

---

# Task 4 — Implement Mission Planner-style MAVLink telemetry logging

## Objective

Add automatic PC-side telemetry logging to MissionPlanner Next Gen.

## Required behavior

- Record MAVLink traffic for an active vehicle connection.
- Record sufficient framing/timing information for later replay/analysis.
- Prefer `.tlog` compatibility if practical; otherwise document the exact format.
- Use deterministic, timestamped filenames.
- Make output directory configurable.
- Display current recording state and file path.
- Flush safely on disconnect/application shutdown.

## Architecture

Keep telemetry recording separate from vehicle onboard/DataFlash logging.

The recorder should subscribe at the transport/connection boundary so diagnostic traffic is not lost because a message type has no domain handler.

## Replay

If full replay is too large for this task, structure the recorder so replay can be added without rewriting the format layer.

## Tests

1. connection starts recording
2. inbound message is persisted
3. outbound message is persisted if the chosen format supports it
4. disconnect finalizes the file
5. reconnect creates a new log/session as intended
6. recorder failure does not crash vehicle connection

## Acceptance criteria

- A bench connection produces a telemetry log automatically.
- The UI shows exactly where it is being written.
- Telemetry recording is independent of `LOG_BACKEND_TYPE` on the vehicle.

---

# Task 5 — Add onboard logger health and storage diagnostics

## Objective

Make vehicle onboard logging understandable and clearly separate it from PC telemetry logging.

## Problem observed

ArduPilot reported:

```text
Failed to create log directory /APM/LOGS : ENOSPC
PreArm: Logging failed
Arm: Logging failed
```

while classic Mission Planner was still writing PC-side `.tlog`/`.rlog` files.

## Required behavior

Create an onboard logging status model containing at least:

- configured `LOG_BACKEND_TYPE`
- enabled/disabled state
- current known health
- latest logger-related `STATUSTEXT`
- whether the condition is affecting arming

Where information is available, expose storage/free-space data.

## UI

Clearly separate:

```text
PC Telemetry Recording
Vehicle Onboard Logging
```

Example:

```text
PC Telemetry Recording: Recording
Vehicle Onboard Logging: Error — ENOSPC
```

## Tests

1. `PreArm: Logging failed` marks onboard logger unhealthy
2. `ENOSPC` text is retained as detail
3. PC telemetry recorder remains healthy/recording independently
4. `LOG_BACKEND_TYPE=0` displays onboard logging disabled, not failed

## Acceptance criteria

- The UI cannot misleadingly imply that no PC telemetry log exists merely because onboard logs are unavailable.

---

# Task 6 — Implement accelerometer calibration workflow

## Objective

Provide complete ArduPilot accelerometer calibration in Next Gen so classic Mission Planner is not required.

## Required behavior

Implement the full calibration interaction supported by ArduPilot/MAVLink:

- start calibration
- display each requested orientation
- user confirmation/continue action where required
- progress indication
- completion result
- failure/cancel path
- retry support

## UI

Use clear orientation instructions such as:

- level
- left side
- right side
- nose down
- nose up
- upside down

Adapt exact sequence to the firmware's requests rather than hard-coding assumptions when possible.

## Integration

After calibration completes:

- refresh relevant parameters/status
- update arming readiness
- expose calibration success/failure

## Tests

Use a simulator/fake MAVLink sequence to verify:

1. calibration start
2. orientation-step progression
3. successful completion
4. cancellation
5. timeout/failure

## Acceptance criteria

- A user can perform accelerometer calibration entirely in Next Gen.
- Result feeds into the arming-readiness UI.

---

# Task 7 — Add compass/sensor configured-vs-detected diagnostics

## Objective

Create a diagnostics view that distinguishes configuration from actual detected sensor hardware.

## Required compass data

At minimum inspect/display:

- `COMPASS_ENABLE`
- `COMPASS_USE`
- `COMPASS_USE2`
- `COMPASS_USE3`
- `COMPASS_DEV_ID`
- `COMPASS_DEV_ID2`
- `COMPASS_DEV_ID3`
- relevant `EK3_SRC*_YAW` configuration

## Required derived states

For each sensor/subsystem expose:

```text
Configured
Detected
Required
Healthy
```

Example failing state:

```text
Compass
Configured: Yes
Detected: No
Required by EKF yaw source: Yes
Status: Error
```

Example valid compassless state:

```text
Compass
Configured: No
Detected: No
Required: No
Status: OK
```

## Device-ID decoding

Where ArduPilot device IDs can be decoded reliably, expose useful fields such as bus/device type/address/external status.

Keep raw ID visible for debugging.

## Tests

1. configured + detected + required → healthy
2. configured + not detected + required → error
3. not configured + not required → OK
4. device ID zero treated as no detected device

## Acceptance criteria

- User can tell whether a problem is configuration intent or missing physical hardware.

---

# Task 8 — Add guided motor start-threshold / dead-zone assistant

## Objective

Extend Motor Test so Next Gen can determine the practical motor start threshold and propose `MOT_SPIN_ARM` / `MOT_SPIN_MIN`.

## Safety requirement

Before running the workflow, require an explicit props-removed acknowledgement.

Do not automatically arm the vehicle.

## Workflow

For each motor in the connected frame:

1. select motor
2. command a short Motor Test pulse
3. start from a low percentage
4. increase in small increments
5. let user confirm when the motor rotates reliably
6. store the threshold

After all motors are tested:

- identify the highest reliable start threshold
- propose an armed-spin value with a configurable/default safety margin
- propose a higher minimum-spin value

Example from the Omnibus test:

```text
Motor start threshold ≈ 15%
Proposed MOT_SPIN_ARM = 0.17
Proposed MOT_SPIN_MIN = 0.20
```

Do not write parameters until the user confirms.

## UI

Show:

```text
Motor
Logical position
Output channel
Test percentage
Observed threshold
Recommendation
```

## Tests

1. thresholds 14/14/15/14 → highest = 15
2. recommendation calculation returns 0.17 / 0.20 with configured defaults
3. cancel performs no parameter writes
4. confirm writes the expected values
5. frame with 6 motors produces 6 motor steps

## Acceptance criteria

- User can reproduce the successful bench workflow without manually guessing spin parameters.

---

# Task 9 — Add motor-output diagnostic summary

## Objective

Provide one diagnostic view that explains how ArduPilot is expected to drive the motors.

## Display at minimum

```text
FRAME_CLASS
FRAME_TYPE
SERVO1_FUNCTION ... relevant SERVOx_FUNCTION
MOT_PWM_TYPE
MOT_SPIN_ARM
MOT_SPIN_MIN
MOT_SAFE_DISARM
BRD_SAFETY_DEFLT
Configured motor-interlock RC option, if any
```

Where available, also show:

- logical motor number
- physical frame position
- physical output channel
- timer/output group
- selected PWM/DShot protocol

## Diagnostic conclusions

The view should be able to communicate states such as:

```text
Motor Test succeeds → FC/ESC/motor path operational
Vehicle arms but motors remain stopped → inspect MOT_SPIN_ARM / spool state
Motor Test fails on all motors → inspect protocol/output mapping/power
```

Do not hard-code a diagnosis from one parameter alone; present evidence.

## Tests

Add tests for mapping of Motor1-4 functions and derived diagnostic text/state.

## Acceptance criteria

- A user can inspect the entire motor-output configuration without searching the full parameter list.

---

# Task 10 — Add telemetry-log replay test harness

## Objective

Use recorded MAVLink telemetry logs as regression fixtures for domain and UI-state behavior.

## Scope

Build a replay source that can feed previously captured telemetry into the existing MAVLink pipeline with preserved ordering and optional timing control.

## Required modes

- real-time or scaled-time replay
- fast deterministic test replay
- pause/stop

## Initial regression scenarios

Create or support fixtures representing:

1. accelerometer pre-arm failure
2. compass pre-arm failure
3. logging `ENOSPC`
4. RC1 not neutral
5. RC4 not neutral
6. transition to `Ready to Arm`
7. transition to `ARMED`

If real captured logs cannot be committed for size/licensing/project reasons, create reduced synthetic fixtures representing the relevant MAVLink sequence.

## Tests

- replay yields deterministic arming status
- replay preserves `STATUSTEXT` ordering
- session reset behaves correctly between logs

## Acceptance criteria

- Arming/diagnostics regressions can be reproduced without connecting physical hardware.

---

# Recommended execution order

Run the tasks individually in this order:

```text
1. Arming status/HUD
2. RC calibration redesign
3. RC neutral diagnostics
4. MAVLink telemetry logging
5. Onboard logger diagnostics
6. Accelerometer calibration
7. Compass/sensor diagnostics
8. Motor start-threshold assistant
9. Motor-output diagnostics
10. Telemetry replay/test harness
```

This order delivers immediate diagnostic value first, then fills the remaining hardware-setup gaps, then adds regression infrastructure.

---

# Global Codex constraints for all tasks

For every task:

- inspect the existing implementation before changing architecture
- reuse current domain abstractions, DI, message dispatch, and view-model patterns
- do not introduce duplicate MAVLink parsing paths
- avoid unrelated refactors
- preserve existing behavior unless the task explicitly changes it
- add focused automated tests
- build the complete solution before finishing
- report changed files and any design decisions
- do not silently change flight-critical parameters
- require explicit user confirmation before parameter writes that alter arming, motor behavior, sensor requirements, or safety behavior
