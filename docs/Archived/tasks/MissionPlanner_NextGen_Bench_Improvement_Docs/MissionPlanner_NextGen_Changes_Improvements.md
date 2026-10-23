# MissionPlanner Next Gen — Changes and Improvements Identified from Omnibus F405 Bring-Up

Date: 2026-09-16

## 1. Bench-test outcome

The Omnibus F405 ArduPilot bring-up reached a successful end state:

- ArduPilot reached **ARMED**.
- Motor Test worked in both MissionPlanner Next Gen and classic Mission Planner.
- Motors spun after correcting the motor idle thresholds.
- The RadioMaster Pocket provided a valid RC signal path and proved the FC, receiver, ELRS/CRSF, RCIN, arming logic, motor outputs, ESCs, and motors were fundamentally working.

Important measured values from the successful path:

- Pocket CH4/Yaw: approximately **992 / 1512 / 2011**.
- TX16S CH4/Yaw previously showed approximately **1076 / 1587 / 2011** and the TX16S channel monitor showed about a **17% offset**.
- Motors only just began to move at about **14%** in Motor Test.
- **15%** was selected as the reliable motor-start threshold.
- Working motor idle settings:
  - `MOT_SPIN_ARM = 0.17`
  - `MOT_SPIN_MIN = 0.20`

The Pocket still needs a small pitch-center adjustment.

---

## 2. Arming-readiness feedback in the HUD

### Problem observed

Classic Mission Planner made the arming process understandable because it exposed:

- `ARMED` / `DISARMED`
- `Ready to Arm` / `Not Ready to Arm`
- current `PreArm: ...` and `Arm: ...` rejection reasons

During this test these messages were essential for finding:

- accelerometer calibration requirement
- missing/unhealthy compass configuration
- onboard logging failure
- RC1 roll not neutral
- RC4 yaw not neutral
- RC2 pitch not neutral

### Improvement

MissionPlanner Next Gen should provide first-class arming state rather than leaving the user to infer it from logs.

Recommended display model:

```text
ARMED / DISARMED
Ready to Arm / Not Ready to Arm
Current PreArm reason
Last Arm failure
```

Recommended MAVLink sources:

- `HEARTBEAT` + `MAV_MODE_FLAG_SAFETY_ARMED`
- `SYS_STATUS` + `MAV_SYS_STATUS_PREARM_CHECK`
- `STATUSTEXT` for human-readable `PreArm:` and `Arm:` reasons

The last meaningful reason should be retained until:

- a new reason replaces it,
- the vehicle becomes ready,
- the vehicle arms,
- or the connection/session is reset.

---

## 3. RC calibration redesign

### Problem observed

The current calibration workflow can fail because the receiver advertises many channels while some channels have no physical switch/control assigned and therefore never move.

That is incorrect validation behavior.

### Improvement

Calibration should distinguish between:

1. **Required flight controls**
   - Roll
   - Pitch
   - Throttle
   - Yaw

2. **Used auxiliary channels**
   - e.g. Arm/Disarm
   - Flight mode
   - other configured RC options

3. **Unused advertised channels**
   - must not invalidate calibration simply because they do not move

### Calibration data to expose

For every relevant channel show:

```text
Minimum
Center/Trim
Maximum
Dead-zone
Observed span
Observed center offset
Configured RCx_MIN
Configured RCx_TRIM
Configured RCx_MAX
Configured RCx_DZ
```

### Automatic diagnostics

Warn when the observed physical center is significantly different from configured trim.

Example discovered during the test:

```text
TX16S CH4 actual center ≈ 1587
Expected nominal center ≈ 1500
Result: ArduPilot rejected arming because yaw was not neutral
```

By contrast:

```text
Pocket CH4 ≈ 992 / 1512 / 2011
```

which is a normal usable range.

A useful warning would be:

```text
Yaw center is 87 µs from configured trim.
Check transmitter trim/subtrim/mix/output calibration or update RC4_TRIM.
```

---

## 4. Separate transmitter-side faults from FC-side calibration faults

### Problem observed

The TX16S itself showed a 17% CH4 offset, so the fault existed before ELRS/CRSF and before ArduPilot.

### Improvement

Next Gen should help classify a bad RC channel into one of these categories:

- transmitter-side offset/configuration
- receiver/protocol problem
- ArduPilot RC calibration mismatch
- insufficient dead-zone
- channel mapping mismatch

For each primary control, Next Gen should provide a compact health assessment such as:

```text
Yaw
Observed: 1076 / 1587 / 2011
Configured trim: 1500
Center error: +87 µs
Status: Out of neutral range
Recommended check: transmitter trim/subtrim/mix/output calibration
```

---

## 5. MAVLink telemetry logging in Next Gen

### Problem observed

Classic Mission Planner automatically wrote `.tlog` / `.rlog` telemetry files to the PC while the FC's onboard logging was failing independently.

This caused understandable confusion because Mission Planner could report no onboard log files while PC telemetry files were still being written.

### Improvement

Implement Mission Planner-style MAVLink telemetry logging in Next Gen.

Minimum capability:

- automatically record incoming/outgoing MAVLink traffic
- `.tlog`-compatible or clearly documented equivalent format
- deterministic file naming
- configurable log directory
- start/stop status in UI
- replay support for diagnostics and regression testing
- make the current log path visible

Suggested UI distinction:

```text
Telemetry Logging
  Recording: Yes
  File: C:\...\2026-09-16-....tlog

Vehicle Onboard Logging
  Status: Failed
  Reason: ENOSPC
  Backend: File
```

---

## 6. Onboard/DataFlash logger diagnostics

### Problem observed

Arming initially failed because ArduPilot reported:

```text
Failed to create log directory /APM/LOGS : ENOSPC
PreArm: Logging failed
Arm: Logging failed
```

This temporarily changed `Ready to Arm` into `Not Ready to Arm`.

Setting `LOG_BACKEND_TYPE = 0` removed the logging blocker for the bench test.

### Improvement

Next Gen should treat onboard logging as a separate subsystem with explicit health.

Display:

- configured `LOG_BACKEND_TYPE`
- logger backend type
- onboard storage health
- free/used space where available
- current `PreArm` logger failure
- clear distinction from PC telemetry recording

Useful diagnostics:

```text
Onboard logger configured: Yes
Backend: File
Status: Error
Reason: ENOSPC
Arming impact: Blocks arming
```

Do not imply that disabling onboard logging is the final production configuration; present it as a diagnostic/workaround when appropriate.

---

## 7. Sensor and compass diagnostics

### Problem observed

The FC was configured as if a compass were available/required even though no compass was detected.

The important distinction is between configuration intent and actual detected hardware.

### Improvement

Add a Hardware/Sensor Diagnostics view that separates:

```text
Configured
Detected
Required
Healthy
```

Example:

```text
Compass
Configured: No
Detected devices: None
Required by EKF yaw source: No
Status: OK for current configuration
```

Or, in the failing case:

```text
Compass
Configured: Yes
Detected devices: None
Required by EKF yaw source: Yes
Status: ERROR
```

Relevant information should include:

- `COMPASS_ENABLE`
- `COMPASS_USE*`
- `COMPASS_DEV_ID*`
- `EK3_SRC*_YAW`
- detected device IDs and decoded bus/type information where available

This should eventually extend to other sensors as well.

---

## 8. Accelerometer calibration in Next Gen

### Problem observed

Classic Mission Planner was required to complete the accelerometer calibration before arming could proceed.

### Improvement

Implement a complete accelerometer-calibration workflow in Next Gen with:

- step-by-step orientation prompts
- progress indication
- MAVLink command/result handling
- success/failure state
- post-calibration verification
- obvious retry/reset behavior

The final state should feed directly into the arming-readiness view.

---

## 9. Motor Test as a hardware diagnostic tool

### Result observed

Motor Test worked in both Next Gen and classic Mission Planner, proving:

```text
FC motor outputs → ESC communication → motors
```

were functioning.

### Improvement

Treat Motor Test as a structured diagnostic workflow, not only as a set of test buttons.

Show:

- logical motor number
- physical frame position
- output/SERVO channel
- `SERVOx_FUNCTION`
- current output protocol
- requested test percentage
- test duration
- result/acknowledgement

Frame-aware motor buttons should continue to match the connected frame type.

---

## 10. Automatic motor dead-zone / start-threshold assistant

### Problem observed

The aircraft armed successfully but motors did not spin because the configured armed-idle value was below the actual ESC/motor start threshold.

Measured behavior:

```text
14%  motors only just begin to move
15%  selected as reliable start threshold
```

Working settings became:

```text
MOT_SPIN_ARM = 0.17
MOT_SPIN_MIN = 0.20
```

### Improvement

Add a guided Motor Start Threshold test:

1. props-off safety confirmation
2. test one motor at a time
3. increase output in small increments
4. user confirms first reliable rotation
5. record threshold per motor
6. compute the highest motor threshold
7. propose safe `MOT_SPIN_ARM`
8. propose `MOT_SPIN_MIN` with appropriate margin
9. let the user review before writing parameters

Example result:

```text
Motor 1 start: 14%
Motor 2 start: 14%
Motor 3 start: 15%
Motor 4 start: 14%

Recommended:
MOT_SPIN_ARM = 0.17
MOT_SPIN_MIN = 0.20
```

This would turn an otherwise confusing armed-but-stationary condition into a guided setup step.

---

## 11. Motor-output diagnostics

Next Gen should provide a compact diagnostic view containing:

```text
FRAME_CLASS
FRAME_TYPE
SERVO1_FUNCTION ... SERVOx_FUNCTION
MOT_PWM_TYPE
MOT_SPIN_ARM
MOT_SPIN_MIN
BRD_SAFETY_DEFLT
Motor interlock configuration
Output protocol/timer grouping where known
```

The purpose is to distinguish:

- arming problem
- output-function mapping problem
- safety/interlock problem
- output-protocol problem
- ESC/motor start-threshold problem

The successful Motor Test should be shown as evidence that the FC-to-ESC path works.

---

## 12. Parameter editor validation already demonstrated

A positive result from this session is that the Next Gen text-based parameter editing workflow successfully handled multiple live setup changes and reboot-required changes.

This should be preserved and enhanced with:

- clear write success/failure state
- reboot-required indication
- before/after value display
- batch changes with review
- easy filtering of recently modified parameters
- optional restore/revert set for bench testing

---

## 13. Suggested priority order

### P0 — Immediate usability / diagnostics

1. Arming state + pre-arm/arm rejection reasons
2. RC calibration redesign
3. RC center/trim diagnostics
4. MAVLink telemetry `.tlog` recording
5. Onboard logger health distinction
6. Motor start-threshold assistant

### P1 — Hardware setup completeness

7. Accelerometer calibration
8. Sensor/compass diagnostics
9. Motor-output diagnostics
10. Telemetry-log replay

### P2 — Refinement

11. automatic recommendations based on measured hardware behavior
12. richer board/sensor discovery
13. regression-test harness using captured telemetry logs

---

## 14. Key design principle learned from this bring-up

Next Gen should not merely expose parameters. It should explain the **state relationship between configuration, detected hardware, live telemetry, pre-arm checks, and the requested action**.

The user should be able to answer, directly from the UI:

```text
What is configured?
What hardware is actually detected?
What is currently blocking the requested action?
What live value caused the block?
What subsystem owns the problem?
What is the safest next diagnostic step?
```

That is where Next Gen can become substantially more useful than a parameter editor alone.
