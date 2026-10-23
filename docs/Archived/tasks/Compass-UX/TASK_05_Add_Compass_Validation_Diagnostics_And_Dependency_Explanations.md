# TASK 05 — Compass Validation, Diagnostics and Dependency Explanations

## Goal

Make the Compass page explain *why* a state is valid, invalid, or irrelevant to arming.

## Validation summary

Add a compact status banner:

### Healthy

```text
Compass configuration is valid.
```

### Disabled intentionally

```text
Compass is disabled.
The current EKF yaw configuration does not require a compass.
```

### Enabled but unavailable

```text
Compass is enabled, but no primary compass device is detected.
```

### Contradictory

```text
Compass is disabled, but the selected EKF yaw source requires a compass.
```

## Arming relevance

Where current telemetry/arming state supports it, show:

```text
Arming impact
  No current Compass arming issue
```

or:

```text
Arming impact
  Compass health is preventing arming
```

Do not infer a blocker only from configuration.

Use current arming/pre-arm state when available.

## Device identification

When `COMPASS_DEV_ID` is non-zero, provide the best friendly identification supported by existing ArduPilot metadata/code.

If exact device name cannot be resolved:

```text
Primary compass
Device ID: 123456
```

Do not invent a sensor model.

## Dependency explanation

For each control, provide concise explanatory text.

Examples:

```text
Enable Compass
Controls whether ArduPilot uses magnetic compass sensors.

Use for yaw
Controls whether EKF yaw estimation depends on compass heading.

Orientation
Describes the physical mounting rotation of the compass.
```

## Advanced diagnostics

Include:

```text
Parameter source/value
Device IDs
Health source
Calibration state
Last update timestamp
```

where data exists.

## Do not duplicate Full Parameters

This page should expose the parameters relevant to the Compass subsystem only.

Do not turn the Advanced section into a second Full Parameters List.

## Tests

Cover:

- disabled and consistent;
- disabled but yaw dependency conflict;
- enabled/no device;
- healthy detected device;
- arming-prearm Compass failure present;
- historical Compass failure no longer current;
- unknown health.

## Acceptance criteria

- Users can understand the consequence of each Compass setting.
- The page distinguishes configuration validity from current arming failure.
- No stale/pre-existing failure is presented as current without evidence.
