# TASK 07 — Freeze, Markers, Timeline and Context Integration

## Goal
Complete the diagnostic workflow and integrate it naturally across MissionPlanner.

## Freeze
`Freeze` means presentation freeze only.

When frozen:
- capture displayed snapshot;
- stop visual values changing;
- continue telemetry ingestion;
- continue diagnostic journal;
- continue `.tlog`;
- continue Serilog;
- continue normal command processing.

Show:

```text
FROZEN at 00:45:21.112
Live data is still being collected
```

Resume immediately displays current live state. Events collected while frozen remain available.

## Markers
`Add Marker` creates a diagnostic event:

```text
00:45:21.112 USER MARKER Pressed Motor 2 test
```

Optional short text. Do not alter classic `.tlog` binary format.

## Recent event timeline
Display the bounded journal:

```text
00:41:22.411 INFO     RC signal restored
00:41:27.017 WARNING  PreArm: Battery failsafe
00:41:32.103 TX       MAV_CMD_DO_MOTOR_TEST Motor=2 20%
00:41:32.109 RX       COMMAND_ACK ACCEPTED
00:41:32.111 INFO     Motor Test: starting motor test
00:41:34.115 INFO     Motor Test: finished motor test
```

Use diagnostic categories rather than only Serilog levels:

```text
Status
Warning
Error
Command TX
Command ACK
STATUSTEXT
Arming
Connection
RC
Motor
Parameter
Calibration
Marker
```

Correlate at least Arm/Disarm and Motor Test request/response flows.

`Clear Events` must not delete `.tlog`, application logs or vehicle state.

## Context-sensitive selection
If Inspector is already open, page context may initially select:

```text
Radio Calibration         -> RC
Motor Test                -> Outputs
Servo Output              -> Outputs
Accelerometer Calibration -> Sensors
Compass                   -> Sensors
Battery Monitor           -> Power
Flight Data               -> Status
```

Do not force the Drawer open and do not keep overriding a user's manual panel choice.

## “Why?” affordance
Where appropriate show a small reusable action:

```text
DISARMED / NOT READY   Why?
```

`Why?` opens/focuses Inspector Status/Arming diagnostics. Do not duplicate full reasons across pages.

## Multi-vehicle/disconnect
- vehicle selector;
- independent state/journals;
- preserve last snapshot after disconnect but mark stale;
- never display stale values as live.

## Performance
Explicitly enforce:
- bounded journals;
- bounded Raw buffer;
- coalesced UI notifications;
- RC/output UI updates around 10–20 Hz;
- no giant collection rebuild per packet;
- no full VehicleState JSON serialization per update.

Dropped UI samples are acceptable. Dropped MAVLink receive processing is not.

## Documentation
Create `docs/LiveTelemetryInspector.md` covering architecture, EventHub separation, Freeze semantics, arming diagnostics, Drawer/Window, journal, context selection, multi-vehicle, Browser and performance.

## Integration tests
At minimum:

### Why not armed
Input:
```text
Heartbeat disarmed
PreArm: Battery failsafe
PreArm: Logging failed
```
Expected:
```text
DISARMED / NOT READY
Battery failsafe
Logging failed
```

### Motor test
Input:
```text
MAV_CMD_DO_MOTOR_TEST sent
COMMAND_ACK ACCEPTED
SERVO_OUTPUT_RAW 1000 -> 1200
starting motor test
finished motor test
```
Expected:
```text
command accepted
output changed
physical movement unknown
```

### Freeze
Displayed snapshot stays fixed while service snapshot/events continue; Resume jumps to latest.

### Multi-vehicle
Two vehicles never mix RC/output/arming states.

### Disconnect
Last state remains inspectable but clearly stale/disconnected.

## Acceptance
The Inspector becomes the one-action live troubleshooting surface for arming, RC, power, motor outputs, connection and recent diagnostic events without adding another Flight Data tab.
