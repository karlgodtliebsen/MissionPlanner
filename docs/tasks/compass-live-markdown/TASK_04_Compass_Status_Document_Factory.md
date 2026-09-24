# TASK 04 — Build the Compass Status/Explanation Document

## Goal

Create the first `UserDocument` factory for Compass.

This document is read-only explanation/reporting. Configuration remains native Avalonia controls.

## Factory

Add an Application-layer factory, for example:

```csharp
public interface ICompassSetupDocumentFactory
{
    UserDocument Create(
        CompassSetupState state,
        CompassConfiguration desired,
        CompassValidationResult validation,
        VehicleArmingStatus? armingStatus);
}
```

Adapt signatures to existing types.

No I/O is allowed inside the factory.

## Human-first content

The beginning of the document must answer:

1. Is Compass enabled?
2. Is a device detected?
3. Is it healthy?
4. Is it used for yaw?
5. Is Compass currently affecting arming?

Example for a no-Compass configuration:

```markdown
## Compass status

The compass is **disabled**.

The current EKF yaw source does not require a compass, so this
configuration does not prevent arming.

### Detected devices

No compass devices are currently detected.

### Relevant parameters

| Parameter | Value |
| --- | ---: |
| `COMPASS_ENABLE` | `0` |
| `COMPASS_USE` | `0` |
| `EK3_SRC1_YAW` | `0` |
```

## Required variants

Generate appropriate content for:

- loading;
- disconnected;
- unsupported;
- disabled and consistent;
- disabled but yaw-source conflict;
- enabled and healthy;
- enabled but no device;
- calibration required/in progress/completed/failed;
- current Compass-related pre-arm blocker;
- stale/historical Compass failure only.

Do not report historical pre-arm text as a current blocker.

## Parameter evidence

Show only relevant parameters that actually exist.

Raw parameter information supports the explanation; it is not the headline.

## Device identification

If exact hardware can be resolved from existing data, show it.

Otherwise show the device ID without inventing a sensor model.

## Pending configuration

When the user edits configuration, the document may explain the desired/pending state, but it must clearly distinguish:

```text
Current FC state
Pending change
```

Do not present unapplied values as already active.

## Tests

Add snapshot/golden tests for all major variants.

## Acceptance criteria

The document is concise, technically precise, selectable and suitable for pasting into an LLM or issue report.
