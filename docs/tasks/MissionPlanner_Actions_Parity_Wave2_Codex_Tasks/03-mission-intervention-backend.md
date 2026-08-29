# Codex Task 03 — Mission Intervention Backend

## Goal

Implement a typed, selected-vehicle mission-intervention backend for:

- **Set Current WP**
- **Restart Mission**
- **Resume Mission**
- **Abort Landing**

This task is backend/Core/Application/MAVLink only. Do not add the new XAML controls yet.

Task 01 must already be complete.

## Fixed semantic decisions

Do not reopen the legacy-semantics investigation. The NextGen behavior is explicitly defined below.

## 1. Create/use a typed mission intervention service

Use the existing mission/application service if it is a natural fit, otherwise introduce a focused typed abstraction conceptually equivalent to:

```csharp
SetCurrentMissionItemAsync(sequence, cancellationToken)
RestartMissionAsync(cancellationToken)
ResumeMissionAsync(cancellationToken)
AbortLandingAsync(cancellationToken)
```

The service must be bound to, or explicitly target, one vehicle/session according to existing architecture.

Do not expose raw command IDs or seven floating-point command parameters to UI/ViewModel code.

## 2. Serialize/correlate per vehicle

These operations consume command ACKs and/or streamed mission state that can be ambiguous if identical operations overlap.

Use the existing command serialization mechanism or add a mission-operation lease so that, at minimum, the same vehicle cannot have overlapping mission intervention operations that could consume one another's ACK or telemetry confirmation.

Do not globally serialize independent vehicles.

Every confirmation wait must establish a post-send boundary/generation so a pre-existing `MISSION_CURRENT` sample cannot count as confirmation.

## 3. Set Current WP

### Primary protocol path

Send:

```text
MAV_CMD_DO_SET_MISSION_CURRENT (224)
param1 = requested canonical mission sequence
param2 = 0
```

Use the existing command microservice/ACK infrastructure.

### Validation

Reject before transmission when the canonical sequence is known to be invalid.

Use the best authoritative mission bounds available from Task 01:

1. verified/current onboard mission snapshot sequences, else
2. valid current `MISSION_CURRENT.total`, when the protocol value is usable.

If mission bounds are not known well enough to validate, return a typed unavailable/invalid-state result rather than inventing an arbitrary bound.

Do not confuse UI row numbers with MAVLink sequence numbers.

### Confirmation

For the primary path, success semantics distinguish:

1. command ACK accepted; and
2. **post-request** `MISSION_CURRENT.seq == requested sequence`.

If ACK is accepted but matching telemetry does not arrive before the confirmation timeout, return/report **AcceptedButNotTelemetryConfirmed** (or the repository's equivalent) rather than claiming full confirmation.

### Compatibility fallback

Only when the command receives explicit `MAV_RESULT_UNSUPPORTED`:

- send superseded `MISSION_SET_CURRENT` to the same target vehicle/component;
- wait for a post-request matching `MISSION_CURRENT`;
- do not manufacture a command ACK for the fallback path.

Do **not** fall back on timeout, cancellation, FAILED, DENIED, TEMPORARILY_REJECTED, or another negative ACK.

## 4. Restart Mission

NextGen semantics are deliberately:

```text
MAV_CMD_DO_SET_MISSION_CURRENT
param1 = 0
param2 = 1   // MAV_BOOL_TRUE: reset mission
```

This operation must:

- require a mission to be known/present;
- reset mission jump counters/completed mission state using the modern command semantics;
- wait for command ACK;
- use a post-request `MISSION_CURRENT` as telemetry confirmation where available.

It must **not**:

- switch to AUTO;
- arm;
- take off;
- issue `MAV_CMD_MISSION_START`;
- upload or rewrite the mission.

Because legacy `MISSION_SET_CURRENT` cannot preserve the chosen reset semantics, **do not provide a fallback** when `MAV_CMD_DO_SET_MISSION_CURRENT` is unsupported. Return Unsupported explicitly.

## 5. Resume Mission

NextGen intentionally rejects the old destructive resume workflow.

Send:

```text
MAV_CMD_DO_PAUSE_CONTINUE
param1 = 1   // continue/resume
```

Requirements:

- no mission download/rewrite/upload;
- no arm;
- no takeoff;
- no hidden SetMode(AUTO/GUIDED);
- no change to current mission sequence unless the autopilot itself changes it.

### Policy

Prefer positive evidence that a mission is paused/suspended:

- `MISSION_STATE_PAUSED`, and/or
- mission mode indicates suspended,

using the state introduced in Task 01.

If the firmware does not report sufficient mission pause/suspend state, do not pretend the policy can prove resumability. Surface a typed unavailable/unknown capability rather than enabling based only on “mission exists”.

### Confirmation

- Require command ACK acceptance.
- If post-request mission telemetry reports transition from paused/suspended to active/in-mission state, mark telemetry-confirmed.
- If the firmware does not report those extension fields, ACK acceptance may be the strongest result. Do not claim more.

## 6. Abort Landing

This is a strongly gated **Plane-only** operation.

### Required policy preconditions

At the time of CanExecute evaluation and again defensively before transmit, require:

1. connected selected vehicle;
2. vehicle family is Plane/fixed-wing as represented by the existing domain model;
3. current flight mode is AUTO;
4. mission execution state is active/appropriate rather than unknown/complete;
5. Task 01 reports a **VerifiedCurrent** onboard mission snapshot;
6. the current mission sequence resolves in that verified snapshot;
7. the current item command is `MAV_CMD_NAV_LAND`;
8. `LAND_ABORT_THR` is known through the typed parameter subsystem and enabled (`1`).

If any prerequisite is unknown, disable/deny the operation. Do not infer current NAV_LAND merely from vehicle altitude or mode.

### Command

Send typed:

```text
MAV_CMD_DO_GO_AROUND
```

Use the existing ArduPilot/default behavior for the command altitude rather than inventing a target. If the current command abstraction requires an explicit `param1`, preserve the proven legacy/default zero value unless current ArduPilot source in the target firmware clearly requires a different representation.

### Result semantics

Treat a positive `COMMAND_ACK` as **abort accepted**, not “landing abort completed”. The resulting climb/go-around may take significant time.

If later post-request mission telemetry shows the vehicle is no longer executing the verified NAV_LAND item, that may be reflected as subsequent telemetry state, but do not hold the command call open for the full go-around maneuver merely to label it complete.

## 7. Independent policy entries

Add semantically distinct policy/capability entries for:

- Set Current Mission Item
- Restart Mission
- Resume Mission
- Abort Landing

Do not borrow Land, SetMode, or another existing action policy as a proxy.

Expose a useful denial reason if the current policy architecture supports it, especially for Abort Landing (for example: not Plane, not AUTO, mission snapshot stale, not executing NAV_LAND, LAND_ABORT_THR disabled/unknown).

## 8. Result/status model

Reuse and extend the existing command result/status model only as needed so callers can distinguish:

- Pending
- Accepted by command ACK
- Rejected/Denied/Unsupported
- Telemetry confirmed
- Accepted but telemetry not confirmed
- Fallback path confirmed by `MISSION_CURRENT` without command ACK
- Timeout
- Cancellation

Do not make the ViewModel decode raw `MAV_RESULT` values if the application layer already owns that concern.

## Out of scope

- No mission intervention XAML.
- No legacy mission rewrite Resume workflow.
- No automatic mode changes.
- No speed/altitude/loiter work.

## Acceptance tests

### Set Current WP

1. Valid sequence sends `MAV_CMD_DO_SET_MISSION_CURRENT` with param1=sequence, param2=0 to the selected vehicle.
2. Out-of-range sequence is rejected before transmission when bounds are known.
3. An accepted ACK alone is not falsely reported as telemetry confirmed.
4. A post-request matching `MISSION_CURRENT` produces confirmed success.
5. A matching `MISSION_CURRENT` that existed before the request cannot satisfy confirmation.
6. Explicit `MAV_RESULT_UNSUPPORTED` triggers exactly one `MISSION_SET_CURRENT` fallback and waits for matching post-request `MISSION_CURRENT`.
7. Timeout/FAILED/DENIED do not trigger the legacy fallback.

### Restart Mission

8. Sends command 224 with sequence 0 and reset=true.
9. Does not send SetMode, Arm, Takeoff, MISSION_START, mission upload, or `MISSION_SET_CURRENT`.
10. Unsupported command returns Unsupported rather than silently degrading to legacy semantics.
11. Accepted ACK + post-request mission-current telemetry is represented accurately.

### Resume Mission

12. Sends only `MAV_CMD_DO_PAUSE_CONTINUE` with continue=true.
13. Does not rewrite/upload the mission, arm, take off, or change mode.
14. Policy enables for a positively reported paused/suspended mission and denies/unknown-gates when resumability cannot be established.
15. ACK plus post-request active mission state yields telemetry-confirmed result when that state is supported.
16. ACK-only firmware support is not mislabeled telemetry-confirmed.

### Abort Landing

17. Sends `MAV_CMD_DO_GO_AROUND` only for a Plane in AUTO with a verified current snapshot whose current item is NAV_LAND and with LAND_ABORT_THR enabled.
18. A stale/unverified snapshot denies Abort Landing.
19. Current item not NAV_LAND denies Abort Landing.
20. LAND_ABORT_THR missing/unknown/disabled denies Abort Landing.
21. Copter/Rover denies Abort Landing without transmission.
22. ACK acceptance is reported as abort accepted, not maneuver completed.

### Concurrency / isolation / regression

23. Two simultaneous vehicles may execute independent mission operations without sharing leases, ACKs, or mission telemetry.
24. Two overlapping same-vehicle mission interventions cannot consume one another's confirmation.
25. Cancellation releases any per-vehicle operation lease.
26. Existing mission upload/download and existing flight command tests remain green.

## Build/test gate

Build MAVLink/Core/Application projects and run command, mission, policy, and multi-vehicle tests.
