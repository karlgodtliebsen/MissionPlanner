# Flight Data 08 — Camera, gimbal and payload component control

## Objective

Implement `PayloadControlTabView` with discovered, component-targeted camera and gimbal workflows.

Dependency: complete task 03 and reuse its component registry.

Apply all constraints from `00-README.md`.

## Existing generated protocol coverage

```text
CameraInformationMessage
CameraSettingsMessage
CameraCaptureStatusMessage
CameraImageCapturedMessage
CameraFovStatusMessage
CameraTrackingImageStatusMessage
CameraTrackingGeoStatusMessage
GimbalManagerInformationMessage
GimbalManagerStatusMessage
GimbalDeviceInformationMessage
GimbalDeviceAttitudeStatusMessage
MountStatusMessage
GimbalManagerSetPitchyawMessage
GimbalManagerSetManualControlMessage
MavCmd.ImageStartCapture / ImageStopCapture
MavCmd.VideoStartCapture / VideoStopCapture
MavCmd.SetCameraZoom / SetCameraFocus
MavCmd.DoGimbalManagerPitchyaw
```

The promotion catalog currently marks camera/gimbal/mount messages as planned protocol workflows.

## Protocol services

Add dedicated component-scoped services/models:

```text
ICameraProtocolService
IGimbalProtocolService
CameraComponentState / CameraCapabilities / CameraOperationResult
GimbalComponentState / GimbalCapabilities / GimbalOperationResult
PayloadComponentSelection
```

Do not add component workflow state to general autopilot `VehicleState`.

## Discovery and state

- Reuse the task-03 component registry.
- Discover every camera, gimbal manager, gimbal device and legacy mount component.
- Request information/status through generated protocols.
- Track state per `VehicleId + ComponentId`, with freshness/expiry/reconnect.
- Never assume one payload or fixed component IDs.
- Prefer gimbal manager protocol; use legacy mount only through an explicit fallback adapter.

## Camera scope

Implement only capability-supported operations:

```text
single image capture
start/stop interval capture
start/stop video
zoom
focus
camera mode when supported
capture status and last image information
```

Validate limits from camera information, serialize per component, correlate ACK and/or observed status, and avoid optimistic final state.

## Gimbal scope

Implement:

```text
current pitch/yaw/roll status
vehicle-frame vs earth-frame/yaw-lock mode
center/home action
acknowledged low-rate pitch/yaw command
rate-limited manual/continuous control
stop/release behavior
```

Use MAVLink NaN/flag semantics correctly. Rate-limit output and stop promptly on pointer release, cancellation, tab disposal and disconnect.

## UI

Provide component selector, capability/firmware summary, unsupported/not-discovered state, camera controls/status, gimbal state, pitch/yaw controls, frame/yaw-lock option and operation/error state. Support mouse, touch and keyboard.

## Safety

- Disable all writes during replay.
- Require confirmation for payload actions classified hazardous.
- Use operation gates and cancellation.
- Clear selected component logically on active-vehicle change, but do not clear UI-bound collections in `Dispose()`.

## Tests

Cover multi-camera/gimbal discovery, component selection/expiry, request correlation, capability gating, correct target component, camera encoding, gimbal flags/NaN semantics, rate limiting, release/cancel/disconnect stop, legacy fallback, replay and ViewModel lifecycle.

Use SITL/plugin tests only when suitable simulated components exist.

## Documentation

- Add Payload Control architecture and supported operations to `docs/FLIGHT_DATA.md`.
- Update `docs/FEATURES.md`.
- Update `docs/MAVLINK_DOMAIN_PROMOTION.md`, `docs/mavlink-promotion-catalog.json`, and `docs/MAVLINK.md` with workflow ownership and component-targeting rules.

## Acceptance criteria

- Supported payloads are discovered and selectable.
- Every command targets the selected component.
- Unsupported capabilities are explicit.
- Continuous controls are bounded, rate-limited and stop reliably.
