# TASK 02 — Receiver Bind Diagnostics and UI

## Goal

Make receiver binding a first-class Radio/Receiver workflow and expose its progress in Live Telemetry diagnostics.

## UI

Place the action in the existing Radio/Receiver setup area.

Example:

```text
Receiver
  Protocol: CRSF / ExpressLRS
  Status: Connected
  Link Quality: ...

[ Enter Receiver Bind Mode ]
```

After request:

```text
Bind command accepted by flight controller.

The receiver should now enter bind mode if the active
RC backend supports FC-initiated binding.

Use Bind in the ExpressLRS Lua script on the transmitter.
```

## Bind state

Add a lightweight state model such as:

```csharp
public enum ReceiverBindState
{
    Idle,
    Requested,
    CommandAccepted,
    Unsupported,
    Failed,
    WaitingForLink,
    LinkRestored
}
```

Do not remain indefinitely in Binding merely because an ACK was accepted.

## Live Telemetry Inspector

Record/display a concise sequence:

```text
Receiver Bind requested
COMMAND_ACK ACCEPTED
RC link lost / waiting for receiver
RC link restored
```

Use existing correlation-id infrastructure.

## Capability display

Where configuration supports it, show:

```text
Receiver protocol: CRSF / ExpressLRS
FC-initiated bind: Supported
```

Otherwise:

```text
FC-initiated bind: Unknown
```

Do not infer ELRS solely from a generic serial port unless current domain data supports that conclusion.

## Timeout

After accepted request, wait for a bounded period for RC link/input recovery.

If no link appears:

```text
Bind command was sent, but no receiver link was observed.
```

That is not necessarily a command failure.

## Tests

Cover:

- supported button state;
- armed/disabled state;
- accepted/failed/unsupported ACK;
- link disappears and returns;
- accepted request but no link by timeout;
- Live Inspector events;
- no duplicate notification spam.

## Acceptance criteria

- Binding is discoverable in Radio setup.
- UI distinguishes command acceptance from RF-link success.
- Live Inspector shows the bind workflow.
