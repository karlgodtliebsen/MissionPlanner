# TASK 05 — Guarded GCS Arm / Disarm Actions

## Goal
Add real Arm / Disarm actions without adding another command implementation.

## Reuse
Use:
```text
IVehicleCommandService
IVehicleCommandPolicy
VehicleLiveDiagnostics / VehicleArmingDiagnostic
IVehicleOperationGate
existing AsyncOperationRunner / operation-state patterns
IUserConfirmationService
```

Do not encode COMMAND_LONG in the ViewModel. Do not create another ACK tracker.

## Arm
1. require selected online vehicle;
2. evaluate `VehicleAction.Arm` with `IVehicleCommandPolicy`;
3. deny when policy denies;
4. require explicit Setup-page confirmation;
5. call `IVehicleCommandService.ArmAsync`;
6. show ACK separately from actual state;
7. require armed heartbeat/diagnostic confirmation before saying ARMED.

Suggested confirmation:
```text
Arm vehicle?

Confirm the vehicle is safe to arm and the motor/propeller area is clear.
```

## No force arm
Do not add force arm, magic MAV_CMD parameter values, `21196`, skipped checks, or another
bypass. The page exists to explain/fix blockers, not bypass them.

## Disarm
Use `IVehicleCommandPolicy`. Honor `RequiresConfirmation`, especially when telemetry does
not confirm the vehicle is on the ground. Call existing typed `DisarmAsync`. Do not claim
Disarmed until heartbeat confirms.

## Transaction presentation
Preserve the existing Flight Data / Actions distinction:
```text
TX
ACK
observed heartbeat transition
```

Examples:
```text
Arm acknowledged; waiting for armed heartbeat.
Arm rejected: ...
Arm acknowledged, but telemetry has not confirmed final state.
Armed heartbeat observed.
```

Prefer the existing diagnostic stage rather than another state machine.

## Blockers
Do not weaken command policy. Do not fabricate readiness. If known blockers exist, show
them in Information and handle button availability according to current policy + explicit
UX decision.

## Replay / connection / vehicle boundary
No transmission in replay-only context.
Connection loss cancels pending presentation.
A command started for Vehicle A must never complete against Vehicle B after selection
changes.

## Tests
Cover no vehicle, stale heartbeat, not-on-ground, already armed, accepted ACK + heartbeat,
accepted ACK without heartbeat, rejected ACK, timeout, connection loss, policy denial,
disarm on ground, hazardous disarm confirmation, cancelled confirmation, vehicle switch,
and replay.

## Acceptance
Normal GCS Arm/Disarm is safe, typed, correlated and honest about ACK versus actual state.
