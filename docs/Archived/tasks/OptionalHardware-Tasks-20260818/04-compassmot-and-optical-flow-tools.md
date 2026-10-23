# Codex Task 4 — Compass/Motor Calibration and Optical-Flow Sensor Tools

## Goal

Implement the Optional Hardware calibration/diagnostic tools that are not merely parameter editors:

```text
Compass / Motor Calibration
PX4Flow / Optical Flow focus-calibration utility
```

Optical Flow parameter configuration is handled by Task 2.

---

# Part A — Compass / Motor Calibration (CompassMot)

## Classic reference

```text
src-v.1.38/GCSViews/ConfigurationView/ConfigCompassMot.cs
```

Classic behavior:

- starts with `MAV_CMD_PREFLIGHT_CALIBRATION` using the compass-motor calibration argument;
- listens for `COMPASSMOT_STATUS`;
- plots current and interference versus throttle;
- shows compensation vector/status;
- stops via the protocol acknowledgement/stop behavior.

NextGen already generates MAVLink message types for CompassMot status. Reuse the generated MAVLink model/decoder rather than defining an ad hoc packet.

---

## Domain/service

Create a dedicated service, e.g.:

```text
ICompassMotorCalibrationService
CompassMotorCalibrationService
CompassMotorCalibrationSnapshot
CompassMotorCalibrationSample
```

State machine should include at least:

```text
Idle
Starting
Running
Stopping
Completed
Failed
Disconnected
```

Each sample should preserve structured values such as:

```text
ThrottlePercent
CurrentAmps
InterferencePercent
CompensationX
CompensationY
CompensationZ
Timestamp
```

Do not make the UI parse formatted text.

---

## Safety

CompassMot can spin motors.

Require:

- supported firmware/capability;
- active vehicle;
- vehicle disarmed;
- operation gate;
- explicit "propellers removed / area clear" confirmation;
- cancellation/stop path;
- disconnect handling.

If ArduPilot requires additional prerequisites such as battery-current sensing for current-based compensation, detect/report them when possible rather than failing mysteriously.

---

## UI

Show:

```text
Safety warning
Start / Stop
Current status
Compensation vector
Current vs throttle
Interference % vs throttle
```

Use a Avalonia-compatible chart/control already in the solution if available.

Do not introduce a heavyweight plotting dependency solely for this one page without review.

Chart data should update incrementally and remain bounded.

---

# Part B — Optical Flow / PX4Flow focus utility

## Classic reference

```text
src-v.1.38/GCSViews/ConfigurationView/ConfigHWPX4Flow.cs
```

The classic page toggles an optical-flow calibration/focus mode and displays incoming sensor images.

Investigate the exact MAVLink messages/helper behavior used by classic `Utilities.OpticalFlow` before porting.

Requirements:

- do not assume every optical-flow sensor supports image/focus mode;
- expose the utility only when the connected sensor/firmware capability is compatible;
- start image/focus subscriptions only while the tab/subsection is active;
- stop calibration mode and release streams on tab deactivation/disconnect;
- bound image update rate to avoid UI flooding;
- never let image decoding block the MAVLink receive loop.

If current NextGen lacks a required promoted MAVLink message, add it through the normal generated-message promotion workflow, not a one-off decoder.

---

## Tests

CompassMot:

1. start command parameters are correct;
2. unsupported/armed/disconnected vehicle rejects before send;
3. status samples project correctly;
4. samples remain ordered/bounded;
5. stop releases operation gate;
6. disconnect terminates workflow;
7. UI state follows service state.

Optical Flow:

1. feature hides when capability is unavailable;
2. activation subscribes once;
3. deactivation stops focus/calibration mode;
4. disconnect releases subscriptions/resources;
5. incoming images are rate-limited and latest-frame wins;
6. parameter-only optical-flow tab remains independent.

---

## Acceptance criteria

Complete when CompassMot is a safe structured workflow and PX4Flow-style focus functionality is capability-driven and lifecycle-safe.
