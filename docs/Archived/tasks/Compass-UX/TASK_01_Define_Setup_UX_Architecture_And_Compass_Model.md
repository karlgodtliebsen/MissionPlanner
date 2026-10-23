# TASK 01 — Define Setup UX Architecture and Compass Model

## Goal

Introduce the architectural model for subsystem-oriented Setup pages, starting with Compass.

Do not redesign the entire Setup area in this task. Establish the contracts and domain/view-state model required by Compass.

## UX target

The Compass page should eventually present:

```text
Compass

Status
  Compass: Disabled / Enabled / Detected / Unhealthy
  Detected devices
  EKF yaw source
  Calibration state
  Overall readiness

Configuration
  Enable compass
  Primary compass
  Use for yaw
  Orientation

Actions
  Start calibration

Advanced
  Relevant raw parameters
```

## New configuration model

Create a feature-level model equivalent to:

```csharp
public sealed record CompassConfiguration(
    bool Enabled,
    bool UsePrimary,
    bool UseSecondary,
    bool UseTertiary,
    CompassYawSource YawSource,
    CompassOrientation Orientation,
    int? PrimaryDeviceId,
    int? SecondaryDeviceId,
    int? TertiaryDeviceId,
    bool RequiresReboot);
```

Adjust exact shape to fit existing MissionPlanner models and ArduPilot semantics.

Do not expose raw parameter names as the primary API.

## State model

Create a state/readiness model equivalent to:

```csharp
public sealed record CompassSetupState(
    CompassConfiguration Current,
    IReadOnlyList<DetectedCompass> DetectedCompasses,
    CompassHealthState Health,
    CompassCalibrationState Calibration,
    bool IsSupported,
    string? UnsupportedReason);
```

Suggested enums:

```text
CompassHealthState:
  Unknown
  Disabled
  Healthy
  Unhealthy
  NotDetected

CompassCalibrationState:
  Unknown
  NotRequired
  Required
  InProgress
  Completed
  Failed
```

Use existing types if equivalent abstractions already exist.

## Parameter abstraction boundary

Define a dedicated interface:

```csharp
public interface ICompassConfigurationService
{
    Task<CompassSetupState> ReadAsync(
        VehicleId vehicleId,
        CancellationToken cancellationToken = default);

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

The ViewModel should depend on this feature-level service, not on a generic parameter dictionary for ordinary configuration.

## Parameter mapping

The implementation will later map the feature model to relevant ArduPilot parameters such as:

```text
COMPASS_ENABLE
COMPASS_USE
COMPASS_USE2
COMPASS_USE3
COMPASS_ORIENT
COMPASS_DEV_ID
COMPASS_DEV_ID2
COMPASS_DEV_ID3
EK3_SRC1_YAW
```

Do not assume every firmware exposes every parameter.

## Capability handling

The state must explicitly handle:

```text
parameter absent
firmware not ArduPilot
unsupported vehicle
unknown metadata
```

Do not substitute guessed values.

## No UI work yet

This task should compile with the existing `CompassSetupView` unchanged where possible.

## Tests

Add unit tests for:

- model construction;
- unsupported/missing capability state;
- no raw parameter names leaking into the public configuration model except optional diagnostics metadata;
- dependency injection registration.

## Acceptance criteria

- Compass configuration has a dedicated feature-level service contract.
- ViewModels can consume semantic Compass state rather than arbitrary parameter keys.
- Existing application builds and current calibration behavior is not broken.
