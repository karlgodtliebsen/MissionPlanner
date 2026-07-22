# Calibration workflows

Initial Setup calibration behavior is owned by Core and projected by MAUI. Views and
ViewModels do not send MAVLink packets directly.

## Accelerometer and level calibration

`IArduPilotCalibrationService` exposes an immutable `CalibrationSnapshot` with these states:

- `NotStarted`
- `Preparing`
- `WaitingForOrientation`
- `Sampling`
- `Completing`
- `Success`
- `Failed`
- `Cancelled`
- `Disconnected`

Six-position calibration starts with `MAV_CMD_PREFLIGHT_CALIBRATION`, accelerometer action
`Full`. ArduPilot's incoming `MAV_CMD_ACCELCAL_VEHICLE_POS` commands are authoritative:
positions 1–6 select the required orientation and the protocol success/failure values end the
workflow. The user explicitly confirms each requested placement; the service replies with
the same position command. Duplicate prompts do not advance progress, and a different
position is ignored until the current request has been confirmed.

Level calibration uses the same preflight command with accelerometer action `Trim`.
`MAV_RESULT_IN_PROGRESS` and its MAVLink 2 progress byte advance the workflow, while only a
terminal accepted ACK records success. Rejected, failed, cancelled, and timed-out ACK paths
remain distinct. `STATUSTEXT` containing placement or calibration guidance is displayed but
cannot produce success.

## Safety and recovery

- The vehicle must be online, active, and disarmed.
- The UI requires an explicit propeller-removal and stable-surface confirmation.
- `IVehicleOperationGate` prevents an ordinary vehicle command and a calibration from
  running concurrently for the same `VehicleId`.
- Cancellation sends the ArduPilot accelerometer failure/abort position where applicable.
- A disconnect produces `Disconnected`; after reconnect the user can reset and restart.
- Existing calibration images under `Resources/Images` illustrate each requested position.
- Relevant calibration parameters are requested again after protocol-confirmed success.
- Setup completion evidence is written only from the `Success` transition.

Protocol timeouts can be configured under `Calibration:StartTimeout` and
`Calibration:LevelTimeout`; defaults are eight and thirty seconds respectively.
