# TASK 05 — Connection, Power and RC Panels

## Goal
Implement the panels most useful for FC bring-up and arming diagnostics.

## Status / Connection
Show:

```text
Connection state
Transport
Port/endpoint
Last valid MAVLink packet age
Last heartbeat age
Link quality
Mode
System status
Firmware
Vehicle/board identity
```

Use the connection monitor. An open serial/socket transport is not sufficient to show Online.

Distinguish:

```text
Online
Degraded
Disconnected
```

## Power
Show available:

```text
Battery voltage
Current
Remaining %
Consumed mAh/Wh
Controller voltage
Servo voltage
Failsafe state
```

Do not hard-code chemistry-specific warning thresholds in the UI.

The previous Pavo case should be obvious:

```text
Battery: 0.797 V
Battery failsafe: ACTIVE
```

## RC
Provide compact live channel visualization and calibration context:

```text
RC Input
1 Roll       1498  ├──────●──────┤
2 Pitch      1501  ├──────●──────┤
3 Throttle    996  ●──────────────┤
4 Yaw        1572  ├───────●─────┤
5 Arm        2011  ├─────────────●
8 Mode        988  ●──────────────┤
```

Optionally:

```text
Channel   Min   Now   Trim   Max
RC1       988   1498  1500   2011
```

Show:
- primary channels;
- configured AUX;
- inactive/unassigned channels less prominently;
- RC connection;
- RSSI/LQ where available;
- Arm/mode channel mapping where known.

Do not require all CRSF-advertised channels to move.

## Performance
Coalesce high-rate RC display updates to roughly 10–20 Hz. Never dispatch every RC packet individually to Avalonia.

## Tests
Cover missing values, connection states, RC mapping/calibration, unassigned channels, update coalescing, and multi-vehicle isolation.

## Acceptance
Connection, power and RC state are understandable live without opening logs.
