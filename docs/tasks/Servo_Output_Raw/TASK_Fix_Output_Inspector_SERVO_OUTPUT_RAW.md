# TASK — Fix Output Inspector SERVO_OUTPUT_RAW State Propagation

## Goal

Fix MissionPlanner Next Gen so the Live Telemetry Inspector **Outputs** panel correctly recognizes and displays `SERVO_OUTPUT_RAW` telemetry that is already being received and retained in the Raw MAVLink journal.

## Confirmed defect

The diagnostic export currently reports:

```text
Outputs:
  Flight controller output is observed. Physical motor movement remains unknown.
  No SERVO_OUTPUT_RAW received.
```

At the same time, the same diagnostic export contains valid incoming `SERVO_OUTPUT_RAW` MAVLink frames.

Example observed raw message:

```text
MessageId: 36
Name: SERVO_OUTPUT_RAW
Direction: Inbound
CRC verified: true
Wire payload: 12 bytes
Frame: 24 bytes
Decoded payload length: 37
```

The raw payload shows four motor outputs at approximately `1000`.

Therefore:

```text
MAVLink transport/parser/raw capture
        = working

Output Inspector state/update path
        = broken or incomplete
```

Do not change the raw MAVLink capture logic unless investigation proves it is necessary.

---

## Investigation scope

Trace `SERVO_OUTPUT_RAW` from:

```text
validated MAVLink frame
-> message decoder
-> decoded SERVO_OUTPUT_RAW message
-> vehicle message dispatcher / handler
-> VehicleSession / VehicleState
-> telemetry event hub
-> Live Telemetry diagnostic state
-> Outputs panel ViewModel
```

Identify exactly where the message stops propagating.

Inspect existing classes/services responsible for:

- `SERVO_OUTPUT_RAW` decoder;
- message registration;
- vehicle message dispatch;
- radio/output state;
- vehicle telemetry event hub;
- diagnostic snapshot state;
- Outputs panel state;
- sample timestamps.

Do not create a parallel output-state model if one already exists.

---

## Expected domain/state behavior

When a valid `SERVO_OUTPUT_RAW` message is received, update vehicle/output state with at least:

```csharp
ServoOutputPort
ServoOutputsRaw
ServoObservedAt
```

or equivalent existing fields.

The latest diagnostic snapshot must no longer contain:

```text
ServoOutputPort = null
ServoOutputsRaw = null
ServoObservedAt = null
```

when valid messages are being received.

For a typical quad with four active outputs, state should resemble:

```text
Port: 0
Outputs:
  1: 1000
  2: 1000
  3: 1000
  4: 1000
ObservedAt: <timestamp>
```

Preserve additional channels if present.

---

## MAVLink v2 zero-trimmed payload handling

The observed message is MAVLink v2 and may be zero-trimmed on the wire.

Do not assume:

```text
wire payload length == full decoded payload length
```

Use the decoded message model after MAVLink zero-padding rules are applied.

The diagnostic export already records cases such as:

```text
wire payload 12 bytes
decoded length 37
```

This is valid MAVLink v2 behavior.

The decoder/output-state path must correctly reconstruct omitted trailing zero fields.

---

## Output Inspector behavior

When `SERVO_OUTPUT_RAW` has been received recently, the Outputs panel must report something equivalent to:

```text
Flight controller output is observed.
SERVO_OUTPUT_RAW received.
Port: 0
Outputs: 1000, 1000, 1000, 1000
Sample age: ...
Output protocol: Normal PWM
ESC RPM: unavailable
```

Do not claim physical motor movement.

Keep the existing distinction:

```text
FC output observed != physical motor movement confirmed
```

---

## Staleness

Add/retain bounded freshness logic.

Suggested semantics:

```text
Fresh:
  sample age <= expected telemetry timeout

Stale:
  SERVO_OUTPUT_RAW was seen previously but no recent sample exists

Never seen:
  no SERVO_OUTPUT_RAW has ever been observed in this vehicle session
```

The current message:

```text
No SERVO_OUTPUT_RAW received.
```

must only be shown for the `Never seen` case.

For stale data use wording such as:

```text
SERVO_OUTPUT_RAW was received previously, but the latest sample is stale.
```

---

## Multi-vehicle correctness

State must be keyed by `VehicleId`.

A `SERVO_OUTPUT_RAW` message from one vehicle must never populate another vehicle's Outputs panel.

---

## Reconnect behavior

On disconnect/reconnect:

- clear or explicitly mark stale output state for the previous session;
- do not carry fresh output samples from the old connection into a new session;
- new messages after reconnect must repopulate the state.

---

## Live Telemetry diagnostic export

The export should include the latest decoded output state consistently with the raw journal.

If Raw contains recent `SERVO_OUTPUT_RAW`, then the exported state should also contain:

```text
ServoOutputPort
ServoOutputsRaw
ServoObservedAt
```

unless there is a documented filtering reason.

This consistency should be enforced by tests.

---

## Tests

Add regression coverage for:

### 1. Basic propagation

Input:

```text
SERVO_OUTPUT_RAW
port = 0
servo1 = 1000
servo2 = 1000
servo3 = 1000
servo4 = 1000
```

Expected:

```text
VehicleState.Radio/Outputs updated
ServoObservedAt populated
Outputs panel says received
```

### 2. MAVLink v2 trimmed payload

Use a frame whose wire payload omits trailing zero fields.

Expected:

```text
decoded output values correct
state updated
no false "No SERVO_OUTPUT_RAW received"
```

### 3. Multiple samples

Later sample replaces earlier sample and updates timestamp.

### 4. Staleness

After timeout:

```text
state = stale
message != "No SERVO_OUTPUT_RAW received"
```

### 5. Multi-vehicle

Vehicle A and Vehicle B maintain independent output state.

### 6. Reconnect

Old sample does not remain fresh across reconnect.

### 7. Diagnostic export consistency

If raw journal contains a recent valid `SERVO_OUTPUT_RAW`, exported state must contain decoded servo-output state.

---

## Acceptance criteria

- Valid `SERVO_OUTPUT_RAW` messages update vehicle/output state.
- Outputs Inspector no longer falsely reports `No SERVO_OUTPUT_RAW received`.
- MAVLink v2 zero-trimmed payloads decode correctly.
- Fresh/stale/never-seen states are distinct.
- Multi-vehicle isolation is preserved.
- Reconnect clears or stales prior-session output data.
- Diagnostic export and Raw journal are internally consistent.
- Existing `.tlog` recording is unaffected.
