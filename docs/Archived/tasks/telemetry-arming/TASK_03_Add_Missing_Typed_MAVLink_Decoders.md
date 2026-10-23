# TASK 03 — Add Missing Typed MAVLink Decoders

## Goal

Reduce unnecessary `RawMavLinkMessage` fallbacks for common telemetry messages that NextGen already uses or should use for vehicle state and diagnostics.

## Confirmed fallback messages in the latest full log

At minimum:

```text
AHRS2
VFR_HUD
GPS_RAW_INT
POWER_STATUS
```

are being identified by the MAVLink definition registry but still emitted as `RawMavLinkMessage`.

## Required work

Implement typed records/decoders for:

### AHRS2

Expose:

```text
roll
pitch
yaw
altitude
latitude
longitude
```

### VFR_HUD

Expose:

```text
airspeed
groundspeed
heading
throttle
altitude
climb
```

### GPS_RAW_INT

Expose all core fields needed by NextGen:

```text
time_usec
fix_type
lat
lon
alt
eph
epv
vel
cog
satellites_visible
```

and MAVLink 2 extension fields where present.

### POWER_STATUS

Expose:

```text
Vcc
Vservo
flags
```

## Decoder registry

Register all new decoders for:

- live telemetry;
- replay/full logger;
- tests.

Avoid separate manual decoder lists drifting apart. If the current architecture has duplicated registration lists, refactor toward one source of truth.

## State integration

Map only fields that belong in existing state models.

Examples:

```text
GPS_RAW_INT -> GPS/position diagnostics
VFR_HUD -> HUD/motion values
POWER_STATUS -> power diagnostics
AHRS2 -> estimator/AHRS diagnostic state
```

Do not overwrite higher-authority state fields if NextGen already receives better sources for the same value.

Document source precedence.

## Raw fallback

Keep `RawMavLinkMessage` as the fallback for unsupported messages.

The goal is not to remove the fallback mechanism.

## Tests

For each message:

- known binary payload -> expected typed record;
- MAVLink v2 trimming handled;
- decoder registration;
- replay and live parity;
- state mapping where applicable;
- no regression to raw fallback for supported messages.

## Acceptance criteria

- The four listed common messages decode as typed messages.
- Live and replay decoder registration cannot drift.
- Existing HUD/GPS/power/estimator state becomes more complete without overwriting authoritative sources incorrectly.
