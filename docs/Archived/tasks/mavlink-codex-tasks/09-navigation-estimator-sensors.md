# Task 09 — Navigation, Estimator, Sensor, and Environment Telemetry

## Objective

Promote the additional messages required for navigation quality, sensor diagnostics, estimator health, and environmental state.

## Candidate messages

Implement domain promotion where useful for:

- `NAV_CONTROLLER_OUTPUT`
- `AHRS`, `AHRS2`, `AHRS3`
- `EKF_STATUS_REPORT`
- `VIBRATION`
- `RAW_IMU`
- `SCALED_IMU`, `SCALED_IMU2`, `SCALED_IMU3`
- `HIGHRES_IMU`
- `SCALED_PRESSURE`, `SCALED_PRESSURE2`, `SCALED_PRESSURE3`
- `DISTANCE_SENSOR`
- `OBSTACLE_DISTANCE`
- `TERRAIN_REPORT`
- `WIND`, `WIND_COV`
- `ALTITUDE`
- `SYSTEM_TIME` and `TIMESYNC`
- optical-flow and odometry messages if present and relevant

## Design

Create coherent state slices rather than appending dozens of scalar properties to `VehicleState`. Possible slices:

- estimator state
- vibration state
- inertial sensor state
- pressure/air-data state
- range/obstacle state
- environmental state
- time synchronization state

For multi-instance sensors, key observations by sensor ID or message instance semantics.

Do not force extremely high-rate raw sensor data into globally observable immutable state if this causes excessive allocations. Consider a bounded latest-sample store or dedicated telemetry stream abstraction.

## Tests

- units and scale conversions;
- sentinel/unknown values;
- multiple sensor instances;
- high-rate update behaviour and allocation-sensitive paths;
- stale state;
- representative SITL integration traffic.

## Acceptance criteria

- All listed messages are typed and available.
- Only messages needed by domain/UI are promoted.
- Raw high-rate traffic remains inspectable without overwhelming EventHub.
