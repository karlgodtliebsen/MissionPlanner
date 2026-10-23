# Flight Data 05 — Servo and relay observation/control

Status: **Completed.**

## Objective

Implement `ServoRelayTabView` with live actuator observation and safety-gated typed servo/relay commands.

Apply all constraints from `00-README.md`.

## Existing support

```text
SERVO_OUTPUT_RAW decoder and VehicleServoOutputObservation
VehicleRadioState.ServoOutputsRaw / ServoOutputPort / ServoObservedAt
RelayStatusMessage generated but currently raw/diagnostic
MavCmd.DoSetServo / DoRepeatServo
MavCmd.DoSetRelay / DoRepeatRelay
IVehicleParameterRegistry and metadata
COMMAND_ACK infrastructure
```

## Domain ownership

Do not continue growing `VehicleRadioState` with actuator concerns. Choose and document either:

1. introduce `VehicleActuatorState` and move servo ownership to an `ActuatorTelemetryHandler`, preserving temporary compatibility; or
2. add a dedicated actuator-status service combining existing servo state with promoted relay state.

Prefer a cohesive actuator aggregate when the migration remains contained. Promote `RELAY_STATUS` and update the promotion catalog.

## Models/services

Add:

```text
ServoChannelState
RelayChannelState
ServoFunctionDescriptor
ActuatorCommandResult
IVehicleActuatorService
IActuatorCommandPolicy
```

Keep observed value, requested value, ACK result, observed confirmation and stale/unknown state separate.

## Servo behavior

- Display live banks/channels and PWM freshness.
- Map channels through `SERVOx_FUNCTION` using the active parameter registry and metadata.
- Handle absent/version-specific parameters.
- Flag motor/throttle functions as hazardous.
- Implement typed set-servo and expert/bounded repeat-servo workflows.
- Validate channel, finite PWM, safe range, repeat count and cycle time.
- Reject motor/throttle functions by default and normally require disarmed state plus explicit confirmation/hold.
- Never report ACK as measured PWM confirmation.

## Relay behavior

- Decode/promote `RelayStatusMessage`.
- Implement typed on/off and optional bounded pulse/repeat.
- Confirm through observed relay status when available.
- If only ACK is available, say `Command accepted; state unconfirmed`.
- Validate relay index against observed/configured capability.

## UI

Provide separate Servo and Relay sections with live state, mapping, freshness, requested test value/action, confirmation, operation status and observed/requested mismatch. Use virtualization if channel count warrants it.

## Lifecycle and safety

- Cancel pending operations on disconnect/disposal.
- Never clear bound channel collections in `Dispose()`.
- Repeats must be bounded.
- Issue protocol stop/reset only from the operation workflow where supported.
- Block all writes during replay.

## Tests

Cover bank/channel mapping, `SERVOx_FUNCTION` resolution, motor denial, armed-state policy, PWM validation, relay bit decoding, command encoding/targets, ACK versus observed result, repeat bounds, operation gating, cancellation/disconnect and ViewModel states.

## Documentation

- Add Servo/Relay safety and state semantics to `docs/FLIGHT_DATA.md`.
- Update `docs/FEATURES.md` and `docs/PARAMETERS.md`.
- Update `docs/MAVLINK_DOMAIN_PROMOTION.md` and `docs/mavlink-promotion-catalog.json` for relay and any servo-owner change.

## Acceptance criteria

- Live outputs and relay state are visible with freshness.
- Commands are typed, acknowledged and policy-gated.
- Observed and requested states are never conflated.
- Motor outputs and repeat operations cannot be triggered accidentally.
