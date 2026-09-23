# TASK 01 — Arming Switch Diagnostics and Configuration

## Goal

Improve Radio/Arming setup so MissionPlanner Next Gen can detect when a transmitter switch is moving correctly but the corresponding ArduPilot `RCx_OPTION` is not configured for Arm/Disarm.

## Real observed case

The full telemetry recording shows RC channel 5 moving approximately:

```text
999 -> 2000 -> 999
```

while parameters are:

```text
RC5_OPTION = 0
FLTMODE_CH = 5
ARMING_RUDDER = 2
```

Therefore channel 5 is currently the flight-mode channel and is not configured as an arm switch.

## Required UI behavior

In Radio Calibration / Receiver / Arming setup, expose:

```text
Flight mode channel: RC5
Arm/Disarm auxiliary channel: Not configured
Stick arming: Enabled
```

When the user moves a 2-position switch, detect the channel movement and offer a helper such as:

```text
RC8 appears to be a two-position switch.
Assign as Arm/Disarm?
```

Do not automatically overwrite `FLTMODE_CH`.

## Arm/Disarm option

Use the ArduPilot auxiliary function value corresponding to:

```text
Arm/Disarm = 153
```

for the chosen `RCx_OPTION`.

The feature must use existing parameter metadata where available rather than hard-coded UI-only magic numbers.

## Conflict detection

Before assigning Arm/Disarm:

- detect if the chosen channel is already used by `FLTMODE_CH`;
- detect if `RCx_OPTION` already has another non-zero function;
- warn rather than silently overwrite;
- allow the user to select another channel.

Example:

```text
RC5 is currently used for Flight Modes.
Choose another channel for Arm/Disarm.
```

## Diagnostics

Add a compact diagnostic view:

```text
RC5: 999 -> 2000 -> 999
Function: Flight Mode
Arm function: Not assigned
```

This should make it obvious when RF and RC decoding work but the switch has no arm function.

## Tests

Cover:

- switch movement detected;
- `FLTMODE_CH=5`, `RC5_OPTION=0` -> report not configured;
- assigning `RC8_OPTION=153`;
- channel already has another option;
- channel equals `FLTMODE_CH`;
- disconnect/reconnect preserves parameter-derived state;
- Browser/WASM and desktop UI bindings.

## Acceptance criteria

- NextGen clearly distinguishes "RC switch works" from "RC switch is configured as Arm/Disarm".
- It does not suggest changing the flight-mode channel accidentally.
- Arm/Disarm can be assigned to a chosen free RC channel.
