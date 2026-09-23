# TASK 02 — Add SERVO_OUTPUT_RAW Typed Decoder and State Propagation

## Goal

Fix the root cause behind the Output Inspector reporting no servo output state even though message 36 is present continuously in the raw telemetry stream.

## Confirmed finding

The full logger repeatedly contains:

```text
MessageId = 36
Name = SERVO_OUTPUT_RAW
Summary = RawMavLinkMessage
```

This means the frame is received and identified, but it is not being converted into a typed domain message.

## Required implementation

Add/register a typed `SERVO_OUTPUT_RAW` decoder.

The decoded model should expose at least:

```csharp
TimeUsec
Port
Servo1Raw
Servo2Raw
Servo3Raw
Servo4Raw
Servo5Raw
Servo6Raw
Servo7Raw
Servo8Raw
Servo9Raw ... Servo16Raw where MAVLink v2 extensions exist
```

Use actual MAVLink field widths/order.

## MAVLink v2 zero trimming

The observed wire frame may be shorter than the maximum payload length because MAVLink 2 trims trailing zero extension bytes.

The decoder must:

- accept minimum/base payload length;
- zero-fill omitted extension fields;
- not reject a valid trimmed frame;
- not confuse wire payload length with logical decoded payload length.

## Registration

Ensure the decoder is registered in the same decoder registry used by the live vehicle connection and telemetry replay.

Do not leave live and replay decoder sets inconsistent.

## State propagation

Route the typed message through existing:

```text
decoder
-> dispatcher
-> VehicleSession / VehicleState
-> telemetry event hub
-> Live Telemetry diagnostic state
-> Outputs panel
```

Populate existing fields equivalent to:

```text
ServoOutputPort
ServoOutputsRaw
ServoObservedAt
```

Do not introduce a second parallel output-state model.

## Tests

Cover:

- MAVLink v1/base payload;
- MAVLink v2 trimmed payload;
- 4-channel output values at 1000;
- extension fields present;
- extension fields omitted;
- registration in live decoder registry;
- registration in replay decoder registry;
- state propagation;
- multi-vehicle isolation;
- reconnect resets/stales state.

## Acceptance criteria

- `SERVO_OUTPUT_RAW` is no longer represented as `RawMavLinkMessage`.
- Vehicle output state updates.
- Outputs Inspector shows live servo output values.
- Replay and live connections behave identically.
