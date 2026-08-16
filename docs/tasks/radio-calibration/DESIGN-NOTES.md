# Radio Calibration Design Notes

These notes accompany the Codex tasks and are not a separate implementation task.

## Current observed screen

The supplied screenshot shows a useful live baseline:

```text
Channel 1 (Roll)      1491 us   1100/1500/1900
Channel 2 (Pitch)     1449 us   1100/1500/1900
Channel 3 (Throttle)  1100 us   1100/1500/1900
Channel 4 (Yaw)       1508 us   1100/1500/1900
...
Channel 13             880 us   1100/1500/1900
Channel 15            2001 us   1100/1500/1900
```

That is a good argument for a fixed visual PWM domain wider than the configured endpoints.

Suggested default:

```text
800 .. 2200 µs
```

rather than:

```text
RCx_MIN .. RCx_MAX
```

as the outer rail.

The configured values remain internal ticks on that rail.

## Suggested meter concept

Centered pilot axis:

```text
800          MIN       TRIM / 1500       MAX          2200
│             │            │              │              │
├─────────────┼────────────●──────────────┼──────────────┤
              ^ captured min       captured max ^
```

During endpoint calibration:

```text
├──────────!──────────────●────────────────!─────────────┤
           cap min                         cap max
```

The final implementation should use MissionPlanner styling and should not reproduce this ASCII literally.

## Important semantic distinction

Keep these values separate:

```text
Raw/current PWM
Stored RCx_MIN
Stored RCx_TRIM
Stored RCx_MAX
Captured minimum
Captured maximum
Candidate trim during Review
Nominal 1500 reference
```

Combining these into one “range” string makes the UI harder to evolve and is likely to create calibration mistakes.

## Recommended visual priority

1. Current raw input — strongest.
2. Captured extrema during calibration — strong, distinct shape.
3. Stored trim — moderate.
4. Stored min/max — moderate/subtle.
5. Nominal 1500 — subtle reference.
6. 800/2200 outer bounds — mostly structural.

## Signal state

Avoid a disappearing UI when the transmitter is turned off.

Preferred:

```text
RC INPUT: NO SIGNAL

CH1 Roll      — µs    [───────── muted rail ─────────]
CH2 Pitch     — µs    [───────── muted rail ─────────]
...
```

This is much better diagnostically than blank rows.

## Receiver summary

A useful compact header:

```text
RC input: Live    Channels: 16    RSSI: 83%    Map: AETR
```

Use `RC_CHANNELS.rssi` / the existing receiver-RSSI projection when available.

Do not label generic telemetry-radio `RADIO_STATUS` metrics as ELRS receiver RSSI without explicit semantic proof.

## Channel map

The transmitter's model output order and ArduPilot's logical pilot-function mapping are related but not identical concepts.

The calibration page should explain what ArduPilot currently believes:

```text
Roll     -> CH1
Pitch    -> CH2
Throttle -> CH3
Yaw      -> CH4
```

and only abbreviate that to AETR/TAER when the mapping genuinely matches.

## Calibration workflow

The UI becomes much clearer if the unused Review state is made real:

```text
Start
  ↓
Capture endpoints
  ↓
Review / center controls
  ↓
Write + readback
  ↓
Verified
```

That also prevents a last endpoint position from accidentally becoming a trim value.

## Possible later work

Not included in these three tasks:

### CRSF / ELRS setup assistant

A future setup card could inspect relevant ArduPilot parameters and explain/configure a CRSF receiver:

```text
SERIALx_PROTOCOL
SERIALx_BAUD
SERIALx_OPTIONS
RC_OPTIONS
RSSI_TYPE
RC_PROTOCOLS
```

It should discover which SERIAL port the receiver is physically connected to rather than assuming `SERIAL1`.

Any “Apply CRSF defaults” feature should show a preview of parameter changes, use the existing parameter-write/readback service, and avoid overwriting unrelated RC_OPTIONS bits.

This would fit naturally in Mandatory Hardware / Radio Setup, but it should remain separate from calibration itself.
