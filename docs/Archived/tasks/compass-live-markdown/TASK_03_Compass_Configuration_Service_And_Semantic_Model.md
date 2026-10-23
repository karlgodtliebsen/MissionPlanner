# TASK 03 — Add Compass Semantic Model and Configuration Service

## Goal

Move `CompassSetupViewModel` away from raw parameter-oriented behavior.

The ViewModel should work with Compass concepts rather than arbitrary parameter names.

## Semantic model

Create/adapt feature-level models equivalent to:

```csharp
public sealed record CompassConfiguration(
    bool Enabled,
    bool UsePrimary,
    bool UseSecondary,
    bool UseTertiary,
    CompassYawSource YawSource,
    CompassOrientation Orientation);

public sealed record CompassSetupState(
    CompassConfiguration Current,
    IReadOnlyList<DetectedCompass> DetectedCompasses,
    CompassHealthState Health,
    CompassCalibrationState Calibration,
    bool IsSupported,
    string? UnsupportedReason);
```

Use existing equivalent types where already available.

## Feature service

Add/adapt:

```csharp
public interface ICompassConfigurationService
{
    Task<CompassSetupState> ReadAsync(...);

    Task<CompassChangeSet> EvaluateChangesAsync(
        VehicleId vehicleId,
        CompassConfiguration desired,
        CancellationToken cancellationToken = default);

    Task<CompassApplyResult> ApplyAsync(
        VehicleId vehicleId,
        CompassChangeSet changes,
        CancellationToken cancellationToken = default);
}
```

The implementation uses the existing parameter subsystem and metadata cache.

Do not create a second parameter cache.

## Relevant ArduPilot parameters

Handle parameters when present:

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

Do not assume every firmware exposes every parameter.

## Dependency validation

Prevent contradictory states.

Example:

```text
Compass disabled
+
EKF yaw source requires Compass
```

must be rejected or represented as an explicit dependent change.

If disabling Compass requires:

```text
COMPASS_ENABLE 1 -> 0
COMPASS_USE 1 -> 0
EK3_SRC1_YAW Compass -> None
```

all dependent changes must be visible in the change set.

Never silently mutate extra parameters.

## Defaults and metadata

Use ArduPilot parameter metadata for:

```text
display values
enum choices
defaults
reboot-required flags
descriptions
```

Do not guess defaults when metadata is missing.

## Apply behavior

Apply via existing parameter write infrastructure.

After apply:

- reread changed values;
- verify actual FC state;
- report partial failure;
- aggregate reboot requirement.

## Acceptance criteria

- Compass configuration is semantic.
- ViewModel does not directly perform arbitrary parameter writes.
- Contradictory parameter combinations are prevented.
- Existing calibration behavior remains available.
