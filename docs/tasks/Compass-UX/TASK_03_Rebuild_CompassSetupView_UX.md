# TASK 03 — Rebuild CompassSetupView UX

## Goal

Rework `CompassSetupView` / `CompassSetupViewModel` around human-readable subsystem concepts while preserving current calibration functionality.

## Required layout

Use the existing MissionPlanner Next Gen visual language, including `SectionCard` where appropriate.

Recommended sections:

```text
Compass

[Status]

[Configuration]

[Calibration / Actions]

[Advanced] collapsed by default
```

## Status section

Display:

```text
Compass                    Enabled / Disabled
Primary device             <friendly device text or Not detected>
Health                     Healthy / Unhealthy / Unknown
EKF yaw source             Compass / None / Other
Calibration                Required / Completed / Unknown
```

Status should be descriptive and read-only.

Use warnings only when they are actionable.

## Configuration section

Add friendly controls:

### Enable Compass

```text
[ ] Enable compass

Use a magnetic compass for vehicle heading.
```

Secondary text:

```text
COMPASS_ENABLE = 0
```

Parameter name must be visually secondary.

### Primary Compass Usage

Friendly control:

```text
Use primary compass for navigation
```

Backing parameter:

```text
COMPASS_USE
```

Only show secondary/tertiary controls when relevant.

### Yaw Source

Friendly selector:

```text
Yaw source
[ None / Compass / <supported alternatives> ]
```

Do not show numeric `EK3_SRC1_YAW` values as the primary choice.

Show:

```text
EK3_SRC1_YAW = <value>
```

as secondary diagnostic text.

### Orientation

Use human-readable names:

```text
Orientation
[ None ▼ ]
```

Do not require users to know enum numbers.

Use parameter metadata/enums where available.

## Calibration section

Preserve current Compass calibration behavior.

Display:

```text
Calibration status
[ Start Calibration ]
```

If Compass is disabled:

- keep calibration unavailable;
- explain why.

## Advanced section

Collapsed by default.

Show relevant raw parameters in a compact expert-oriented view.

Example:

```text
COMPASS_ENABLE       0
COMPASS_USE          0
COMPASS_USE2         0
COMPASS_USE3         0
EK3_SRC1_YAW         0
COMPASS_ORIENT       0
```

The user may navigate to Full Parameters from here, but normal configuration must not require it.

Provide:

```text
[ Open Full Parameters ]
```

if there is an existing navigation mechanism.

## ViewModel responsibilities

`CompassSetupViewModel` should expose semantic properties such as:

```text
IsCompassEnabled
SelectedYawSource
SelectedOrientation
HealthText
DetectedDeviceText
CanCalibrate
PendingChanges
```

It should not directly write parameters.

## Loading state

Handle:

```text
vehicle disconnected
parameters loading
unsupported firmware
parameter metadata unavailable
```

without flashing incorrect defaults.

## Acceptance criteria

- A user can perform normal Compass setup without opening Full Parameters.
- Parameter names remain discoverable for experts.
- Existing calibration workflow still works.
- The page clearly explains current state and contradictory configuration.
