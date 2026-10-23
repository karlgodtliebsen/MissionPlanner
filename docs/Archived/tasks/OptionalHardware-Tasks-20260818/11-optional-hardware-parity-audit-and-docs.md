# Codex Task 11 — Optional Hardware Parity Audit, Cleanup and Documentation

## Goal

After Tasks 1–10, perform the final Optional Hardware audit.

This task is not permission to add placeholder tabs.

Every visible tab must be one of:

```text
Implemented and usable
Implemented but clearly capability-unavailable for this target
Explicitly removed/deprecated with documented reason
```

No "coming soon" controls on the production Optional Hardware page unless the product intentionally uses that pattern elsewhere.

---

## 1. Compare against classic Optional Hardware

Reference:

```text
src-v.1.38/GCSViews/InitialSetup.cs
```

Classic connected list includes approximately:

```text
RTK/GPS Inject
CubeID Update
SiK Radio
CAN GPS Order
Battery Monitor
Battery Monitor 2
DroneCAN/UAVCAN
Joystick
Compass/Motor Calib
Range Finder
Airspeed
PX4Flow
Optical Flow
OSD
Camera Gimbal
Motor Test
Bluetooth Setup
Parachute
ESP8266 Setup
Antenna Tracker
FFT Setup
```

Produce a documentation table mapping each to:

```text
NextGen tab/location
status
capability rule
classic behavior intentionally changed
tests/hardware verification
```

Do not require one-to-one pages where NextGen deliberately improves the model, e.g.:

```text
Battery Monitor + Battery Monitor 2 -> Battery Monitors
PX4Flow + Optical Flow -> Optical Flow with PX4Flow calibration subsection
OSD -> existing Onboard OSD configuration subsystem
```

---

## 2. Review connection/no-connection behavior

Verify:

- disconnected state contains only genuinely usable standalone/local tools;
- vehicle-only tabs appear when actual capability/parameters exist;
- reconnecting to another vehicle recomputes the set;
- no stale component/node/device data leaks across targets;
- selected tab falls back if it disappears.

Test at least:

```text
No vehicle
ArduCopter
ArduPlane
Rover
AntennaTracker
SITL
```

where supported.

---

## 3. Safety review

Audit every command-producing Optional Hardware feature.

At minimum:

```text
Motor Test
CompassMot
Joystick vehicle output
Tracker actuator test
CubeID firmware update
DroneCAN firmware update
```

Verify:

- operation gate where appropriate;
- disconnect cancellation;
- target identity;
- explicit confirmations for destructive/dangerous operations;
- STOP/recovery;
- no command continues against a newly selected vehicle.

---

## 4. Secrets review

Audit:

```text
NTRIP credentials
Bluetooth PIN
Wi-Fi SSID/password
SiK encryption/AES keys
```

Requirements:

- no secret logs;
- diagnostic export redacts;
- password controls masked by default;
- preference storage uses the best existing secure storage mechanism when persistence is required;
- avoid persistence where unnecessary.

---

## 5. UI consistency

All tabs should follow consistent NextGen patterns:

```text
heading
status
clear capability/unavailable text
refresh when useful
error presentation
explicit Apply for writes
reboot-required indication
adaptive desktop/mobile layout
theme support
lifecycle cleanup
```

Avoid reproducing classic WinForms layout.

---

## 6. Remove/refactor obsolete generic UI

Review:

```text
MandatoryHardware/Sections/OptionalHardwareSetupView*
```

If the new Optional Hardware workspace supersedes the generic data-grid view:

- either remove it safely, or
- retain it only as an internal/advanced generic parameter-module inspector with a clear purpose.

Do not leave two competing Optional Hardware entry points.

Also review DI registrations and dead commented Mandatory Hardware tab entries.

---

## 7. Documentation

Create/update:

```text
docs/OPTIONAL_HARDWARE.md
```

Include:

- architecture and tab catalog;
- connection/capability rules;
- which tools work offline;
- parameter-write/readback policy;
- serial exclusive-access policy;
- operation gate/safety policy;
- component/node identity handling;
- testing strategy;
- classic-to-NextGen migration table.

Update Introduction content if the Setup/Optional Hardware screenshots or wording are now obsolete.

---

## 8. Tests/build

Run all relevant:

```text
MissionPlanner.Core.Tests
MAVLink tests for any promoted/generated messages
UI/ViewModel tests
Transport tests
Firmware tests if serial/device code was reused
```

Perform representative SITL/hardware checks and document them.

No skipped failing tests simply because hardware is absent; hardware-specific tests may be separately categorized, but pure protocol/domain behavior must remain automated.

---

## Acceptance criteria

Optional Hardware is complete when:

- every classic capability has a deliberate NextGen disposition;
- the workspace is capability-driven;
- no stale/placeholder pages remain;
- safety and secret handling have been reviewed;
- duplicate generic implementations are removed/resolved;
- documentation reflects actual behavior;
- automated tests pass;
- remaining hardware-only verification is explicitly listed.
