# Task 10 — Control, Mission, Parameter, Camera, Gimbal, and Peripheral Protocol Coverage

## Objective

Complete protocol-layer support for message families that belong to dedicated workflows rather than `VehicleState`.

## Workstreams

### Command and mode

Cover:

- `SET_MODE`
- `COMMAND_INT`
- `COMMAND_LONG`
- `COMMAND_ACK`
- request-message and message-interval commands
- guided setpoint messages
- manual control and RC override messages

Route acknowledgements through command correlation services.

### Mission

Complete all common and ArduPilot mission messages, including legacy float forms, INT forms, partial list operations, changed notifications, fences, rally points, and mission types.

Maintain one mission protocol state machine; do not create general vehicle handlers for request/response packets.

### Parameters

Cover classic parameter messages and extended parameter protocol messages if present. Preserve packed MAVFTP parameter support as an independent optimized transport.

### Logs and data transfer

Cover log request/list/data/erase/end messages and data transmission handshakes where present.

### Camera and gimbal

Cover camera information/settings/capture/status, gimbal manager/device protocols, mount status/control compatibility, and command acknowledgements.

### Peripherals

Provide typed protocol access for relevant families:

- ADS-B/transponder
- generator and EFI
- ESC telemetry/status
- winch
- landing target
- OpenDroneID
- cellular/Wi-Fi link status
- CAN/serial control/tunnel messages
- device operation messages

Only build full application services for families currently required by product scope. Others remain typed/raw protocol messages with inspector visibility.

## Tests

For each implemented workflow:

- successful request/response sequence;
- timeout;
- cancellation;
- reconnect disposal/lifetime correctness;
- wrong system/component correlation rejection;
- duplicate/out-of-order response handling;
- malformed packet handling.

## Acceptance criteria

- Protocol responses are owned by dedicated services, not general domain handlers.
- Existing MAVFTP reconnect ownership guarantees remain intact.
- All selected dialect messages are typed or explicitly documented as registry/raw only.
