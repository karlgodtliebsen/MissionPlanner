# Codex Task 05 — In-Flight Adjustments Backend

## Goal

Implement typed backend support for:

- **Change Speed**
- **Change Altitude**
- **Set Loiter Radius**

This task is Core/Application/MAVLink/parameter backend only. Do not add the new UI section yet.

## Fixed semantic decisions

### Change Speed

- Copter: Ground speed only.
- Rover: Ground speed only.
- Plane: Airspeed or Ground speed.
- Throttle is not part of this feature.

### Change Altitude

- input is an **absolute target altitude above HOME**;
- this is a Guided operation;
- the command must not silently switch flight mode;
- do not reproduce legacy `MISSION_ITEM current=3` behavior.

### Set Loiter Radius

- persistent parameter write;
- use typed parameter services;
- preserve current sign/direction while replacing the magnitude.

## 1. Add/use a typed adjustment service

Use an existing movement/vehicle operation service if appropriate or introduce a focused abstraction conceptually equivalent to:

```csharp
ChangeSpeedAsync(VehicleSpeedTarget target, CancellationToken ct)
SetGuidedAltitudeAsync(HomeRelativeAltitude target, CancellationToken ct)
SetLoiterRadiusAsync(LoiterRadiusMagnitude radius, CancellationToken ct)
```

UI/ViewModels must never supply raw MAVLink enum integers, type masks, frames, parameter names, or command parameter arrays.

## 2. Semantic speed model

Introduce/reuse a typed semantic enum/value object equivalent to:

```csharp
enum VehicleSpeedTargetType
{
    GroundSpeed,
    Airspeed
}
```

and a validated positive finite speed value in m/s.

Do not include throttle in this request model.

### Supported combinations

#### Copter

Expose GroundSpeed only.

Use `MAV_CMD_DO_CHANGE_SPEED` with a semantic GroundSpeed mapping. ArduPilot Copter treats type 0/1 as ground speed, but NextGen should send the semantically correct MAVLink speed type for GroundSpeed rather than copying the legacy type-0 bug.

#### Rover

Expose GroundSpeed only and map it semantically.

#### Plane

Support:

- Airspeed -> MAVLink speed type Airspeed;
- GroundSpeed -> MAVLink speed type GroundSpeed.

Do not alter throttle (`param3` must represent no change according to the current command abstraction/protocol convention).

### Validation

At minimum reject:

- NaN/infinity;
- zero/negative normal target speeds.

If current vehicle parameters expose authoritative airspeed limits, use them to reject clearly impossible Plane airspeed requests before transmission. Do not introduce a mandatory dependency on metadata that is not loaded unless the repository already treats those limits as required state.

### Policy

Create an independent ChangeSpeed capability.

Initially enable only for supported vehicle families and navigation/autopilot modes in which an externally commanded target speed is meaningful in the current ArduPilot implementation. Map the repository's concrete mode enum deliberately; do not simply use `IsArmed` as the whole policy.

The service must still validate the selected vehicle family/type defensively before transmission.

### Result semantics

`MAV_CMD_DO_CHANGE_SPEED` uses command ACK handling.

Do not claim telemetry confirmation merely because measured airspeed/groundspeed later approaches the value: measured speed is not a reliable confirmation that the requested target setpoint was accepted/applied exactly.

Result should normally be **CommandAccepted** when ACK accepted.

## 3. Typed HOME-relative Guided altitude operation

Create a strongly typed HOME-relative altitude target in meters.

Requirements:

- finite value;
- non-negative unless the existing domain explicitly supports a justified below-HOME target;
- selected vehicle must have the position/state required by the chosen protocol path;
- current mode must already be a Guided-compatible mode;
- **never switch mode automatically**.

### Protocol implementation

Use the modern ArduPilot Guided mechanism appropriate for each supported vehicle family. Do not use the legacy `MISSION_ITEM` `current=3` special form.

#### Copter

Preferred implementation is `SET_POSITION_TARGET_GLOBAL_INT` in a HOME-relative global frame.

ArduPilot requires complete position-axis semantics when position is provided, so when using this message:

- capture current target vehicle latitude/longitude at operation start;
- use them as the horizontal target;
- set altitude to the requested HOME-relative altitude;
- use a position-only type mask that ignores velocity, acceleration, yaw, and yaw rate;
- use a HOME-relative frame (`MAV_FRAME_GLOBAL_RELATIVE_ALT_INT` or the repository's exact typed equivalent).

The application layer exposes none of those protocol details.

This message does not have a `COMMAND_ACK`. Do not manufacture one.

#### Plane / fixed wing

Use a currently supported ArduPilot Guided altitude mechanism that preserves the same product semantics: absolute HOME-relative altitude, no mode switch.

Prefer `MAV_CMD_GUIDED_CHANGE_ALTITUDE` through `COMMAND_INT` when the repository MAVLink dialect and target ArduPilot firmware support it with an explicit HOME-relative frame. A typed `SET_POSITION_TARGET_GLOBAL_INT` path is acceptable if it is the supported implementation in the current ArduPilot target and preserves the same semantics.

Before coding this adapter, verify the exact current ArduPilot source/documented frame handling and encode it in a focused protocol test. Do not fall back to the legacy mission-item hack.

#### Rover

Do not expose Change Altitude for Rover.

### Altitude confirmation

Use post-request `RelativeAltitudeMeters` telemetry for confirmation.

Define an explicit reasonable tolerance using an existing altitude tolerance convention if present. Do not require exact floating-point equality.

Result lifecycle must distinguish:

- target sent / command accepted (depending on protocol path);
- altitude target telemetry confirmed/reached;
- accepted/sent but not reached before confirmation timeout;
- rejected/unsupported where command ACK exists;
- cancellation.

For a no-ACK setpoint message, do not report “Autopilot accepted command”; use wording/state such as TargetSent until telemetry confirms the target altitude.

Do not block for an excessive flight-duration timeout. Use a dedicated bounded confirmation period consistent with existing Actions telemetry confirmation patterns.

## 4. Persistent Loiter Radius parameter operation

This operation belongs in a typed parameter-backed application service, even if exposed through the adjustment service facade.

### Parameter selection

For the selected vehicle, find the actual available parameter in this order:

1. `WP_LOITER_RAD`
2. `LOITER_RAD` compatibility fallback only if it is actually present

Do not blindly send both.

If neither exists, capability is unsupported.

### Direction/sign preservation

UI/backend request is a **positive radius magnitude**.

Read/use the current parameter value before the write:

```text
current < 0 -> write -requestedMagnitude
current > 0 -> write +requestedMagnitude
current == 0 -> write +requestedMagnitude
```

Changing radius must not unexpectedly reverse an established loiter direction.

### Validation

- magnitude must be finite and > 0;
- honor parameter metadata min/max when available;
- convert UI units to meters before reaching this Core operation if the UI supports non-SI display units.

### Confirmation

Use the existing typed parameter write lifecycle and require the normal matching parameter acknowledgement/value confirmation used elsewhere in NextGen.

Do not represent this as a `COMMAND_ACK`.

Return/report the final signed persisted value so the UI/status can be accurate if useful.

## 5. Independent policies

Add independent policy/capability entries for:

- Change Speed
- Change Altitude
- Set Loiter Radius

Policy must react to vehicle family, mode/state, connectivity, required position state, and parameter availability as applicable.

Do not use one shared `CanInFlightAdjustment` proxy for all three.

## 6. Per-vehicle operation isolation

Speed command ACKs, altitude telemetry confirmation, and parameter write confirmation must all be correlated to the selected vehicle/session.

Two vehicles may execute independent adjustments concurrently without cross-confirmation.

## Out of scope

- No UI/XAML in this task.
- No throttle adjustment.
- No automatic mode change.
- No terrain/MSL/delta altitude selector.
- No loiter-direction selector.
- No persistent setting for speed/altitude beyond what ArduPilot itself does.

## Acceptance tests

### Change Speed

1. Copter GroundSpeed sends `MAV_CMD_DO_CHANGE_SPEED` with semantic GroundSpeed mapping and no throttle change.
2. Rover GroundSpeed uses GroundSpeed mapping and no throttle change.
3. Plane Airspeed maps to Airspeed.
4. Plane GroundSpeed maps to GroundSpeed.
5. Copter/Rover Airspeed request is rejected before transmission.
6. Invalid speed values are rejected.
7. ACK accepted is represented as CommandAccepted, not fabricated telemetry confirmation.
8. Negative/zero throttle mutation is not accidentally introduced as a user-controlled side effect.

### Change Altitude

9. Copter requires a Guided-compatible mode and valid current position.
10. Copter sends a HOME-relative global target with current lat/lon and requested absolute relative altitude using the correct type mask/frame.
11. Copter path does not wait for or fabricate `COMMAND_ACK` for `SET_POSITION_TARGET_GLOBAL_INT`.
12. Pre-request altitude telemetry cannot satisfy confirmation.
13. A post-request relative altitude within tolerance produces TelemetryConfirmed.
14. A telemetry timeout yields Sent/AcceptedButNotConfirmed rather than false success.
15. The service never calls SetMode.
16. The service never emits legacy `MISSION_ITEM current=3`.
17. Plane adapter has focused tests proving the exact frame/command encoding chosen from current ArduPilot support.
18. Rover Change Altitude is unsupported.

### Loiter Radius

19. `WP_LOITER_RAD` is preferred when present.
20. `LOITER_RAD` is used only when `WP_LOITER_RAD` is absent and the fallback exists.
21. Neither parameter -> capability unsupported/no transmission.
22. Existing negative sign is preserved when changing magnitude.
23. Existing positive sign is preserved.
24. Zero current value becomes positive requested magnitude.
25. Parameter confirmation is correlated to the selected vehicle and exact parameter.
26. No `COMMAND_ACK` is fabricated for parameter write.

### Isolation/regression

27. Vehicle A speed ACK cannot complete vehicle B's speed operation.
28. Vehicle A altitude telemetry cannot confirm vehicle B's altitude operation.
29. Vehicle A parameter value cannot confirm vehicle B's loiter-radius write.
30. Existing parameter, command, and movement tests remain green.

## Build/test gate

Build affected MAVLink/Core/Application projects and run command, movement, parameter, policy, and multi-vehicle tests.
