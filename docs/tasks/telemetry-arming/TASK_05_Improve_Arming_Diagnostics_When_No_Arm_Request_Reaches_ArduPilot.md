# TASK 05 — Improve Arming Diagnostics When No Arm Request Reaches ArduPilot

## Goal

Improve the "Why Not Armed?" experience so NextGen can distinguish:

```text
ArduPilot rejected an arm request
```

from:

```text
No arm request has reached ArduPilot
```

## Real observed case

In the full recording:

- RC input is present;
- the arm-like switch movement is visible;
- there are no `PreArm:` failures;
- there is no arm/disarm `COMMAND_ACK`;
- heartbeat never transitions to armed;
- the apparent arm switch channel is not configured with an Arm/Disarm RC option.

The correct diagnosis is therefore not "arming rejected".

It is closer to:

```text
No arm request observed.
```

## Required arming diagnostic states

Add/extend states such as:

```csharp
Unknown
DisarmedReady
DisarmedNotReady
ArmRequested
ArmRejected
Armed
DisarmRequested
```

and diagnostic context:

```text
LastArmAttemptAt
LastArmCommandSource
LastArmAck
LastPreArmReason
```

## No-request diagnostic

If:

- vehicle is disarmed;
- no recent arm command/arming transition is seen;
- pre-arm checks appear healthy;
- user has active RC input;

show:

```text
DISARMED

No recent arm request was observed.

Check:
- assigned Arm/Disarm RC option
- transmitter switch mapping
- stick arming configuration
```

Do not invent a pre-arm blocker.

## Pre-arm health

Use `SYS_STATUS` pre-arm health when available to distinguish:

```text
checks currently healthy
```

from:

```text
checks currently failing
```

Keep STATUSTEXT as the detailed human-readable reason source.

## Command source

Where possible distinguish:

```text
MAVLink arm command
RC auxiliary switch
rudder/stick arming
```

Do not require a COMMAND_ACK for RC switch/rudder arming because those are not necessarily represented as GCS MAVLink arm commands.

Instead correlate:

```text
RC state/function
heartbeat armed flag
STATUSTEXT
```

## Historical vs current reasons

Do not keep stale `PreArm:` reasons indefinitely after the underlying condition has recovered.

Track timestamps and freshness.

## Tests

Cover:

- no arm request + pre-arm healthy;
- MAVLink arm request accepted;
- MAVLink arm request rejected;
- RC switch configured and toggled;
- RC switch moves but has no arm function;
- rudder arming enabled;
- stale PreArm reason after recovery;
- heartbeat armed transition without COMMAND_ACK.

## Acceptance criteria

- "Why Not Armed?" does not falsely imply ArduPilot rejected a command when no arm request reached the arming logic.
- It can guide the user toward transmitter/RC-option configuration.
- Current and historical arming reasons are clearly separated.
