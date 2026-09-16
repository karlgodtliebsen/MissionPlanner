# Accelerometer calibration

Initial Setup → Mandatory Hardware → Accelerometer uses the existing
ArduPilotCalibrationService and orientation view. Start six-position calibration,
position the disarmed vehicle exactly as requested by firmware, and select Confirm
orientation for each request. Level, left/right side, nose down/up, and upside-down
labels correspond to actual protocol requests; the UI does not impose an order.

The existing MAVLink pipeline supplies command acknowledgements and
ACCELCAL_VEHICLE_POS messages. Explicit firmware success completes the workflow;
generic command acceptance does not imply six-position completion. Failures,
cancellation, disconnection, malformed requests, and retry remain distinct.
Protocol sends use the application-owned IVehicleConnectionSession and operation
gate. A synchronous next-position reply cannot be overwritten by local Sampling.

Startup defaults to an eight-second deadline; level calibration to thirty seconds;
the complete six-position workflow to five minutes (Calibration.SixPositionTimeout).
Timeout releases local ownership and leaves a retryable failed state.

Success requests accelerometer offsets/scales and AHRS trims, including parameters
not already downloaded, then requests SYS_STATUS. Refresh is connection-scoped and
bounded to ten seconds. HUD readiness changes only when fresh vehicle telemetry
reports readiness; calibration success does not override other arming blockers.

Five fake-MAVLink tests cover firmware-selected order, all six positions and success,
parameter/status refresh, cancel/retry, startup and overall timeout, explicit failure,
and malformed orientation input. Physical accelerometer calibration remains unverified.
