# Task 08 — Promote Core Vehicle-State Telemetry

## Objective

Complete typed observations, handlers, state application, and tests for the messages that directly define the normal flight display and vehicle health.

## Required message families

Review existing support and add missing coverage for at least:

- `HEARTBEAT`
- `SYS_STATUS`
- `ATTITUDE`
- `ATTITUDE_QUATERNION`
- `GLOBAL_POSITION_INT`
- `LOCAL_POSITION_NED`
- `VFR_HUD`
- `GPS_RAW_INT`
- `GPS2_RAW`
- `EXTENDED_SYS_STATE`
- `BATTERY_STATUS`
- `BATTERY2`
- `POWER_STATUS`
- `RC_CHANNELS`
- `RADIO_STATUS`
- ArduPilot `RADIO`
- `SERVO_OUTPUT_RAW`
- `HOME_POSITION`
- `AUTOPILOT_VERSION`
- `STATUSTEXT`

## Implementation

For each promoted message:

1. reuse/generated typed wire message;
2. create or extend a focused observation;
3. add to an existing cohesive handler where appropriate;
4. apply through `VehicleSession`;
5. update the appropriate immutable state slice;
6. publish meaningful change events only when state actually changes;
7. expose through existing vehicle/HUD services.

Do not create a separate handler class when an existing cohesive handler such as `PowerTelemetryHandler`, `RadioTelemetryHandler`, or `FlightTelemetryHandler` is the correct owner.

## State semantics

Define units explicitly at the boundary. Convert protocol-scaled integers exactly once. Represent unknown sentinel values as nullable values or dedicated validity state.

Add stale-data semantics where the UI needs to distinguish missing telemetry from old telemetry.

## Tests

For every promoted family:

- decoder fixture test;
- handler-to-observation test;
- `VehicleSession` state application test;
- duplicate/no-change behaviour test;
- integration test through `VehicleMessagePump` using `FakeMavLinkVehicle` or canonical frames.

## Acceptance criteria

- The normal vehicle selector, HUD, identity, power, GPS, radio, and landed-state views have complete underlying state.
- Existing UI contracts remain stable unless deliberately migrated.
