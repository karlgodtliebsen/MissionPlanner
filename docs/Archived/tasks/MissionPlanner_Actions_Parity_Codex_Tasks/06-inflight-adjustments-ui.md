# Codex Task 06 — In-Flight Adjustments Actions UI

## Goal

Expose the typed backend operations from Task 05 through a clean, compact FlightData Actions section.

Required user operations:

- Change Speed
- Change Altitude
- Set Loiter Radius

## UI design

Add a section named **In-flight adjustments**.

Prefer an expandable/collapsible section and default it to collapsed if this matches current NextGen patterns. These are secondary operational controls and should not visually compete with Arm/Disarm/RTL/Land/Takeoff.

Do not recreate the legacy button grid.

## Controls

### Change Speed

Provide:

- numeric speed input;
- unit shown explicitly, normally `m/s` if that is what the backend's proven semantics use;
- **Change Speed** button.

If the backend supports more than one speed type, expose only the types that are meaningful and proven for the current vehicle or use a compact selector. Do not expose raw MAVLink enum numbers.

### Change Altitude

Provide:

- numeric target/value input;
- units shown explicitly (`m` if applicable);
- altitude reference/frame shown explicitly in human-readable form;
- **Change Altitude** button.

The UI must not leave the user guessing whether the value means relative-home altitude, AMSL, terrain altitude, or delta altitude.

If only one reference is safely supported by the backend for the current vehicle/mode, show it as fixed explanatory text rather than creating a misleading selector.

### Set Loiter Radius

Provide:

- numeric radius input;
- units shown explicitly (`m` if applicable);
- direction indication/selector only if sign/direction semantics were proven in Task 05;
- **Set Loiter Radius** button.

Do not expose an opaque signed-number convention without a user-readable explanation.

## ViewModel requirements

Extend `ActionsTabViewModel` with:

- typed numeric properties;
- any semantic enum/selection properties supplied by Core;
- independent `CanChangeSpeed`, `CanChangeAltitude`, and `CanSetLoiterRadius`-style state;
- async commands using the typed service APIs;
- validation consistent with existing MVVM/UI conventions;
- command-status integration through the existing command lifecycle.

The ViewModel must not build raw MAVLink parameters.

## UX requirements

1. Inputs must reject or visibly flag invalid values before transmission.
2. Buttons disabled when policy/capability denies the operation.
3. Pending state prevents duplicate submissions according to existing command rules.
4. Success text must not overclaim telemetry confirmation if only ACK acceptance is known.
5. The section must remain usable on narrow/mobile layouts.
6. Existing core Actions controls must remain above these advanced/secondary controls.

## Acceptance tests

Automated tests must show at minimum:

1. Change Speed passes the exact typed value/selection to the backend.
2. Change Altitude passes the exact typed altitude and reference semantics to the backend.
3. Set Loiter Radius passes the correct typed radius/direction semantics.
4. Invalid values do not call the backend.
5. Each action is independently enabled/disabled by its own policy capability.
6. Changing one input does not mutate unrelated properties such as takeoff altitude or Expert Command ID.
7. ACK/rejection/timeout/cancellation updates the shared command-status UI correctly.
8. Existing Actions bindings and commands remain intact.

## Manual verification

Provide an ArduCopter SITL verification checklist for each supported operation and record:

- precondition/mode;
- command issued;
- expected ACK;
- expected observable telemetry/state change;
- cases where ACK is the only reliable success signal.

## Build/test gate

Build affected projects and run Task 05 backend tests plus Actions ViewModel/UI tests. Report exact commands and results.
