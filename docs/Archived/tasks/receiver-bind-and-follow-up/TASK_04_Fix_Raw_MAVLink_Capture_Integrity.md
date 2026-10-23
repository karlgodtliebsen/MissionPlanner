# TASK 04 — Fix Raw MAVLink Capture Integrity

## Goal

Verify and fix Live Telemetry raw MAVLink capture so complete message payload/frame data is preserved.

## Observed issue

A recent exported diagnostic snapshot contained entries similar to:

```text
ATTITUDE
Summary: RX · CRC verified · 3 bytes
Payload: 92E16E

RC_CHANNELS
Summary: RX · CRC verified · 3 bytes

SERVO_OUTPUT_RAW
Summary: RX · CRC verified · 4 bytes
```

These are not plausible complete payload lengths for those MAVLink messages.

This suggests the raw diagnostic path may be retaining a buffer fragment, tail bytes, CRC bytes, or an incorrectly sliced reusable buffer.

## Trace

Inspect:

```text
transport read
-> MAVLink frame parser
-> validated frame
-> decoder
-> telemetry event
-> raw diagnostic journal
-> export
```

Identify where bytes/length are lost.

## Required raw model

Retain at least:

```csharp
VehicleId
Timestamp
Direction
SystemId
ComponentId
MessageId
MessageName
PayloadLength
PayloadBytes
FrameLength
CrcVerified
Signed
```

Where buffer lifetime requires it, copy immutable bytes at the diagnostic boundary.

Do not retain references into parser buffers that are later reused.

## Consistency

Raw and decoded representations must derive from the same validated frame.

Add tests asserting:

```text
raw.MessageId == decoded.MessageId
raw.PayloadLength == actual MAVLink payload length
stored payload bytes == decoder input payload bytes
```

## Required messages

Test at least:

```text
HEARTBEAT
ATTITUDE
RAW_IMU
RC_CHANNELS
SERVO_OUTPUT_RAW
GLOBAL_POSITION_INT
STATUSTEXT
COMMAND_ACK
```

Include MAVLink v1/v2 where supported.

## Buffer reuse regression

Explicitly reuse/overwrite the parser/read buffer after publishing an event and verify the stored Raw entry remains unchanged.

## UI/export

If the UI only displays a preview, label it:

```text
Payload preview
```

but preserve the full payload in details/export.

Do not alter `.tlog` format or telemetry recorder behavior.

## Acceptance criteria

- Raw diagnostic payload/frame lengths are truthful.
- Stored bytes remain valid after parser-buffer reuse.
- Raw and decoded views are consistent.
- `.tlog` remains unchanged.
