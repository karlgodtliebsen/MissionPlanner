# TASK 06 — Motor Outputs, Sensors and Raw MAVLink

## Goal
Make it possible to distinguish:

```text
command rejected
```

from:

```text
command accepted + FC output changed + physical hardware did not respond
```

## Outputs / Motors
For active outputs show where available:

```text
Logical motor/function
SERVOx_FUNCTION
Current output/raw value
Output protocol
Motor-test state
Requested motor-test %
ESC/RPM telemetry if available
```

Example:

```text
Motor 2
  Function    Motor2
  Output      1200
  Protocol    DShot600
  Motor Test  20%
  ESC RPM     unavailable
```

Never claim a motor physically spun merely because output changed.

Correlate motor-test flow:

```text
Requested
Accepted / Rejected
Started
Finished
Associated STATUSTEXT
```

## Sensors
Show concise health for:

```text
IMU/Accelerometer
Gyro
Barometer
Compass
GPS
EKF
Vibration
Range sensors
Onboard logging health where relevant
```

Prefer `Configured / Detected / Required / Status` where supported.

An intentionally disabled optional compass must not be displayed as a generic critical failure.

## Raw MAVLink
Advanced bounded/virtualized stream:

```text
Time
SysId
CompId
Message Id
Message Name
Summary
```

Features:
- filter name/id;
- filter SysId/CompId;
- pause/follow-tail;
- payload/details;
- unknown messages remain visible.

Raw MAVLink must be separately bounded and must not flood the normal diagnostic journal or block MAVLink receive processing.

## Tests
Cover motor output changes, accepted motor test without physical-response claim, sensor rendering, disabled optional sensors, unknown messages and bounded high-rate Raw stream.

## Acceptance
Motor-test command state and FC output can be compared live; sensors are clear; Raw MAVLink is available as an advanced view.
