# Codex Task 9 — Joystick Setup and Vehicle Input

## Goal

Implement the Optional Hardware Joystick workspace with a clean separation between:

```text
local joystick device setup
vehicle command/output
```

The device can be configured when no vehicle is connected. Sending commands requires an active vehicle and explicit enablement.

---

## Classic reference

```text
src-v.1.38/Joystick/*
src-v.1.38/ExtLibs/ArduPilot/Joystick/*
```

Do not port platform-specific WinForms controls or polling loops directly.

---

## 1. Device abstraction

Create:

```text
IJoystickProvider
IJoystickDevice
JoystickDeviceDescriptor
JoystickState
JoystickAxisState
JoystickButtonState
```

The provider must be behind platform adapters.

Requirements:

- enumerate devices;
- stable device identity;
- axis/button count;
- current normalized/raw values;
- disconnect/reconnect detection;
- cancellation/disposal.

### Cross-platform

Do not hard-wire the domain to Windows APIs.

If the first concrete implementation is Windows-only, provide:

- clean interface;
- explicit supported-platform state;
- no-op/unsupported adapter on other targets;
- documented path for Linux/macOS adapters.

Prefer an existing dependency already used by the solution; otherwise justify any new native input dependency before adding it.

---

## 2. Calibration and mapping

UI should support:

```text
axis assignment
reverse
center/min/max calibration
dead zone
button assignment
live input display
```

Use a structured mapping model.

Do not encode axis/button functions as arbitrary display strings only.

Persist mappings as application preferences keyed by stable device identity.

Changing transmitter/vehicle parameters is not part of joystick calibration.

---

## 3. Vehicle output semantics

Inspect classic behavior and current MAVLink/domain command infrastructure.

Choose the appropriate current MAVLink method for joystick control and document it.

Potential mechanisms include:

```text
MANUAL_CONTROL
RC_CHANNELS_OVERRIDE
```

Do not blindly use both.

The chosen output must:

- preserve active vehicle System/Component identity;
- stop immediately on disable/disconnect;
- be rate-limited to a sensible control rate;
- have a dead-man/enable mechanism;
- not continue in background after tab/page deactivation unless user has deliberately enabled a persistent joystick-control mode.

### Safety

Default:

```text
Joystick output disabled
```

Require explicit user enablement each session.

If device disappears, send/enter a safe neutral/release state according to the chosen protocol.

Do not create a hidden permanent control source.

---

## 4. UI

Suggested:

```text
Device: RadioMaster / game controller ...
[Refresh]

Axes
Roll       X axis    reverse [ ]   live bar
Pitch      Y axis    reverse [ ]   live bar
Throttle   Z axis    reverse [ ]   live bar
Yaw        RX axis   reverse [ ]   live bar

Buttons / actions
...

[Enable vehicle control]
```

Reuse the radio-channel meter visual concepts where practical, but keep joystick calibration distinct from RC receiver calibration.

---

## Tests

1. provider enumerates mock devices.
2. stable device mapping persists.
3. axis calibration normalization.
4. reverse/deadzone.
5. button edge detection.
6. output disabled by default.
7. enable starts bounded-rate output.
8. disable stops output.
9. device disconnect releases vehicle input.
10. active-vehicle disconnect cancels output.
11. vehicle switch cannot inherit an enabled joystick silently.
12. lifecycle deactivation follows the chosen persistent/nonpersistent policy.

---

## Acceptance criteria

Complete when a joystick can be configured locally and, when explicitly enabled, safely drive the active vehicle through one well-defined MAVLink control mechanism.
