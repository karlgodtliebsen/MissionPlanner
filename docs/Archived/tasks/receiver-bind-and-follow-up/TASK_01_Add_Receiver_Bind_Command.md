# TASK 01 — Add Receiver Bind Command

## Goal

Add a user action that asks ArduPilot to place a supported RC receiver into bind/pairing mode.

Primary target:

```text
CRSF / ExpressLRS
```

The user should not need to power-cycle the FC/receiver three times to enter ELRS bind mode when the ArduPilot/receiver backend supports FC-initiated binding.

## Protocol

Use ArduPilot's existing MAVLink support:

```text
MAV_CMD_START_RX_PAIR
```

ArduPilot routes this through its RC protocol backend and CRSF/ELRS can forward a receiver bind command.

Do not implement a proprietary ELRS serial protocol in NextGen.

## Command service

Extend the existing vehicle command infrastructure with responsibility equivalent to:

```csharp
Task<ReceiverBindResult> StartReceiverBindAsync(
    VehicleId vehicleId,
    CancellationToken cancellationToken = default);
```

Reuse the generic command/ACK correlation infrastructure.

## Result semantics

`MAV_RESULT_ACCEPTED` means:

```text
ArduPilot accepted/dispatched the bind request.
```

It does not prove that the physical receiver definitely entered bind mode.

Use wording such as:

```text
Bind command accepted/sent
```

not:

```text
Receiver is definitely in bind mode
```

## Availability

Enable only when appropriate:

- vehicle connected;
- disarmed;
- ArduPilot;
- receiver/backend supports or is expected to support FC-initiated bind.

CRSF/ExpressLRS is the primary supported case.

If protocol capability is not known, represent it as Unknown rather than assuming support.

## Safety

- Disable while armed.
- Do not modify RC parameters.
- Do not reboot the FC.
- Do not alter receiver/transmitter firmware.

## Logging

Add structured diagnostic events:

```text
ReceiverBindRequested
VehicleId
ReceiverProtocol
MavCommand
CommandAck
VehicleReason
```

## Tests

Cover:

```text
Connected + disarmed + CRSF -> command sent
ACK ACCEPTED -> accepted
ACK FAILED -> failed
ACK UNSUPPORTED -> unsupported
Disconnected -> unavailable
Armed -> unavailable
Cancellation -> clean
```

## Acceptance criteria

- Receiver bind can be requested from NextGen.
- `MAV_CMD_START_RX_PAIR` is used.
- Existing command infrastructure is reused.
- ACK semantics are represented accurately.
