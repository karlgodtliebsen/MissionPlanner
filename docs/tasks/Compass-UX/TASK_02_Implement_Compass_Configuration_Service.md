# TASK 02 — Implement Compass Configuration Service

## Goal

Implement the translation between friendly Compass configuration concepts and ArduPilot parameters.

## Source of truth

Reuse the existing parameter subsystem and metadata parser.

Do not create a separate parameter cache.

## Required reads

Read relevant parameters when present:

```text
COMPASS_ENABLE
COMPASS_USE
COMPASS_USE2
COMPASS_USE3
COMPASS_ORIENT
COMPASS_ORIENT2
COMPASS_ORIENT3
COMPASS_DEV_ID
COMPASS_DEV_ID2
COMPASS_DEV_ID3
EK3_SRC1_YAW
```

Include additional Compass parameters only when needed by the existing view or current firmware behavior.

## Friendly interpretation

Translate raw values into semantic state.

Examples:

```text
COMPASS_ENABLE=0
-> Compass disabled

COMPASS_ENABLE=1
COMPASS_DEV_ID=0
-> Compass enabled but no primary device detected

COMPASS_ENABLE=1
COMPASS_USE=1
COMPASS_DEV_ID!=0
-> Primary compass enabled and available
```

Yaw-source interpretation must be metadata-driven or based on an explicit ArduPilot mapping in one central place.

Do not scatter numeric EKF yaw-source values across ViewModels.

## Write model

`EvaluateChangesAsync` should return an explicit change set:

```csharp
public sealed record ParameterChange(
    string Name,
    object? OldValue,
    object? NewValue,
    bool RequiresReboot,
    string? Reason);
```

The result should support:

```text
No change
Changed
Invalid
Unsupported
Conflict
Requires reboot
```

## Dependency rules

At minimum implement validation for:

### Disabling Compass

If user disables Compass:

```text
COMPASS_ENABLE = 0
COMPASS_USE = 0
COMPASS_USE2 = 0
COMPASS_USE3 = 0
```

and ensure the selected EKF yaw source is not left requiring a compass.

Do not silently change yaw source without representing that change explicitly in the change set.

### Enabling Compass

If enabling Compass while no device is detected:

- allow configuration if ArduPilot permits it;
- show warning;
- do not claim a device is healthy.

### Use for yaw

If user selects a Compass-dependent yaw source while Compass is disabled:

- reject or present a required dependent change;
- never create an internally contradictory configuration.

## Parameter availability

When parameters are absent in a specific firmware:

- hide/disable unsupported controls later in the UI;
- do not manufacture values;
- include diagnostic reason.

## Apply behavior

Apply changes through the existing parameter write infrastructure.

Requirements:

- deterministic order where dependencies matter;
- stop/report partial failure clearly;
- reread changed parameters after apply;
- return actual values from FC;
- surface reboot-required state.

## No hard-coded defaults

Use ArduPilot parameter metadata for defaults where available.

If metadata is unavailable, default must be represented as unknown rather than guessed.

## Tests

Cover:

- disabled Compass;
- enabled healthy Compass;
- enabled but no device;
- disable Compass with Compass yaw source currently selected;
- enable Compass;
- change orientation;
- missing parameters;
- partial write failure;
- reread after apply;
- reboot flag aggregation.

## Acceptance criteria

- Friendly Compass configuration translates correctly to ArduPilot parameters.
- Invalid parameter combinations cannot be created silently.
- All changes are explicit and reviewable before apply.
