# Codex Task 2 — Complete Servo Output Parameter and Live Output PWM Support

## Objective

Complete:

**Setup → Mandatory Hardware → Servo Output**

so each physical FC servo output represents the corresponding ArduPilot `SERVOx_*` parameters and live PWM output.

The column previously named `Position` has already been renamed to:

```text
Output PWM
```

Retain that name.

## Conceptual model

Each Servo Output row represents one **physical autopilot output channel**:

```text
Row #1 -> SERVO1_*
Row #2 -> SERVO2_*
...
```

Its assigned function is independent of motor test order.

For example:

```text
SERVO1_FUNCTION = Motor2
```

means:

```text
Physical output #1 -> logical Motor2
```

This is valid and must not be reordered to make motor numbers sequential.

## Required row state

Each row should expose at least:

```csharp
ChannelNumber
CurrentOutputPwm
Reversed
Function
Min
Trim
Max
IsDirty
```

Adapt to existing architecture and naming.

Do not bind the UI directly to raw parameter-name strings if the existing project uses ViewModels/domain/services for parameter handling.

## Parameter mapping

For output channel `n`, use:

```text
SERVO{n}_REVERSED
SERVO{n}_FUNCTION
SERVO{n}_MIN
SERVO{n}_TRIM
SERVO{n}_MAX
```

For example, row 4 uses:

```text
SERVO4_REVERSED
SERVO4_FUNCTION
SERVO4_MIN
SERVO4_TRIM
SERVO4_MAX
```

## Min / Trim / Max

Populate the existing UI controls for:

```text
Min
Trim
Max
```

from the vehicle parameters.

Preserve actual vehicle values. Do not substitute defaults when a valid parameter value has already been read.

Parameter editing must support the valid range exposed by parameter metadata. Where metadata is unavailable, use the established ArduPilot servo PWM limits already used by this project rather than inventing arbitrary UI limits.

Typical values are:

```text
Min  = 1100
Trim = 1500
Max  = 1900
```

but these are not fixed values.

## Reverse

`Reverse` must read and write:

```text
SERVOx_REVERSED
```

using the representation expected by ArduPilot and the project's parameter subsystem.

## Function

`Function` must read and write:

```text
SERVOx_FUNCTION
```

using parameter metadata for the option list where available.

Do not maintain a separate hard-coded motor-function list if the parameter metadata subsystem can provide the allowed values.

## Output PWM

`Output PWM` is telemetry, not a configuration parameter.

Populate it from the application's existing servo-output telemetry state, normally originating from MAVLink servo output messages.

For example:

```text
Output #1 -> live channel 1 output PWM
Output #2 -> live channel 2 output PWM
```

The value should update while the view is active without requiring parameter rereads.

Prefer presentation including units:

```text
1000 µs
1500 µs
```

provided this fits naturally into the existing control.

Do not write Output PWM values back as configuration.

## Refresh behaviour

`Refresh servo outputs` should refresh/rebuild the relevant state without discarding unsaved user changes unexpectedly.

Follow existing project conventions for refresh/dirty-state handling.

## Write behaviour

The Write button must:

1. Be disabled when there are no dirty editable values.
2. Write only modified Servo Output parameters unless the existing parameter-writing abstraction deliberately handles this differently.
3. Await parameter writes.
4. Report write failures through the application's normal error mechanism.
5. Clear dirty state only after successful writes.
6. Not attempt to write `CurrentOutputPwm`.

## Acceptance tests

### Test 1 — Parameter mapping

Given Servo Output row 3, assert the row maps to:

```text
SERVO3_REVERSED
SERVO3_FUNCTION
SERVO3_MIN
SERVO3_TRIM
SERVO3_MAX
```

### Test 2 — Existing parameter values

Given:

```text
SERVO1_FUNCTION = Motor2
SERVO1_MIN      = 1100
SERVO1_TRIM     = 1500
SERVO1_MAX      = 1900
```

assert row #1 presents exactly those values.

### Test 3 — Non-sequential motor functions

Given:

```text
SERVO1_FUNCTION = Motor2
SERVO2_FUNCTION = Motor3
SERVO3_FUNCTION = Motor4
SERVO4_FUNCTION = Motor1
```

assert the UI/model retains that exact mapping.

It must not normalize it to Motor1/Motor2/Motor3/Motor4.

### Test 4 — Live PWM

Given a telemetry update:

```text
Output 1 = 1075
Output 2 = 1180
```

assert the corresponding row state updates to:

```text
1075
1180
```

without modifying any Servo configuration parameter or dirty state.

### Test 5 — Dirty tracking

Change:

```text
SERVO2_MIN 1100 -> 1125
```

assert:

```text
row.IsDirty == true
Write enabled
```

After successful write:

```text
row.IsDirty == false
```

### Test 6 — Failed write

Simulate a rejected/failed parameter write and assert:

- dirty state remains set;
- the UI can retry;
- the failure is surfaced.

## Definition of done

- `Output PWM` displays live telemetry.
- Reverse, Function, Min, Trim, Max load correctly.
- All editable values can be written.
- Physical channel ordering remains physical channel ordering.
- Servo Output contains no Motor Test A/B/C/D logic.
- Automated tests pass.
- Build affected projects successfully.
- Do not perform unrelated UI redesign.
