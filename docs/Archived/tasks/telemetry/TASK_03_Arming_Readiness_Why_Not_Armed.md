# TASK 03 — Arming Readiness / “Why Not Armed?”

## Goal
Make the Inspector answer immediately:

```text
Why is this vehicle not armed?
```

## Reuse
Use existing support for:
- HEARTBEAT armed flag;
- SYS_STATUS pre-arm readiness;
- `VehicleArmingStatus`;
- `PreArm:` STATUSTEXT;
- `Arm:` failure STATUSTEXT;
- COMMAND_ACK for Arm/Disarm;
- RC state;
- battery/failsafe state;
- onboard logging health.

Do not create a second truth for armed state.

## Diagnostic model
Provide a model equivalent to:

```csharp
public sealed record VehicleArmingDiagnostic(
    VehicleArmingState State,
    bool IsArmed,
    bool? IsReadyToArm,
    string? PrimaryReason,
    IReadOnlyList<ArmingDiagnosticReason> Reasons,
    string? LastArmFailure,
    DateTimeOffset? LastArmAttemptAt,
    DateTimeOffset UpdatedAt);
```

UI example:

```text
ARMING
State: DISARMED / NOT READY

Why not armed?
  Battery failsafe
  Logging failed
  RC not found

Last arm attempt:
  Failed: throttle too high
```

## Reason handling
Track only concrete evidence from vehicle/domain state.

Avoid stale `PreArm:` text remaining forever. Implement explicit lifecycle/expiry semantics:
- current/confirmed reasons remain;
- transient old messages expire or are replaced;
- `LastArmFailure` remains separately as history.

## Arm transaction correlation
Correlate:

```text
Arm command sent
STATUSTEXT
COMMAND_ACK
Heartbeat armed-state change
```

`COMMAND_ACK ACCEPTED` is not by itself proof the vehicle armed.

## Derived summary
Provide one UI-ready state:

```text
ARMED
DISARMED / READY
DISARMED / NOT READY
ARMING UNKNOWN
CONNECTION LOST
```

## Tests
Cover:
- ready/disarmed;
- armed;
- multiple PreArm reasons;
- stale reason expiry;
- failed arm + STATUSTEXT;
- ACK accepted but no armed heartbeat;
- RC/battery/logging blockers;
- multi-vehicle isolation.

## Acceptance
The Inspector can answer “Why not armed?” without opening raw logs.
