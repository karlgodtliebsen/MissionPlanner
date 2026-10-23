# Codex Task 06 — In-Flight Adjustments UI

## Goal

Expose the typed backend from Task 05 through a compact **In-flight adjustments** section in FlightData > Actions.

Required controls:

- Change Speed
- Change Altitude
- Set Loiter Radius

Task 05 must be complete and green first.

## 1. Section layout

Add a secondary section named **In-flight adjustments**.

Use an expander/collapsible pattern if consistent with the application. It may default collapsed because these controls are less frequently used than Arm/Mode/RTL/Land/Takeoff.

Keep the design responsive at desktop/tablet/mobile widths.

No raw MAVLink fields or parameter names should be required from the operator, except that the Loiter Radius explanatory text may identify the underlying persistent parameter for clarity.

## 2. Change Speed UI

Provide:

- positive numeric speed input;
- displayed unit using existing unit infrastructure (internally m/s);
- semantic speed-type selector only where more than one type is supported;
- **Change Speed** button.

### Vehicle-specific presentation

#### Copter / Rover

Show Ground speed as fixed semantic text. Do not offer an Airspeed selector.

#### Plane

Offer a compact selector:

- Airspeed
- Ground speed

No raw enum values.

Do not offer throttle in this control.

If current parameter/state data provides a useful valid Plane airspeed range, show it as validation/help; otherwise do not invent one.

## 3. Change Altitude UI

Label the input unambiguously:

> **Target altitude above HOME**

Show the current UI distance unit, with the typed backend receiving meters.

The operator value is absolute HOME-relative altitude, never a delta.

Do not offer MSL / terrain / delta selectors in this task.

Do not switch to GUIDED automatically. If the current mode is not supported, disable the control and provide concise help such as:

> Change Altitude is available in Guided mode.

Use the actual supported mode wording for the current vehicle family.

## 4. Set Loiter Radius UI

Provide:

- positive radius magnitude input;
- displayed distance unit;
- **Set Loiter Radius** button;
- concise persistent-setting indication, for example:

> Persistent vehicle parameter. Existing loiter direction is preserved.

Do not expose signed-radius direction convention as the input.

If the backend reports neither `WP_LOITER_RAD` nor compatibility `LOITER_RAD`, disable/hide according to existing capability UX. Prefer visible-disabled with reason for supported vehicle families when useful, and hidden for clearly irrelevant families if that matches the app pattern.

## 5. ViewModel requirements

Add typed properties only:

- speed value;
- semantic speed type;
- HOME-relative altitude value;
- positive loiter-radius magnitude;
- independent `CanChangeSpeed`, `CanChangeAltitude`, `CanSetLoiterRadius` state;
- async commands invoking only Task 05 typed services.

Do not place MAVLink `SPEED_TYPE`, `MAV_FRAME`, position type masks, command IDs, or parameter-sign logic in the ViewModel.

Refresh options/capabilities when:

- selected vehicle changes;
- vehicle family changes/initializes;
- mode/state changes;
- connection changes;
- required position state changes;
- relevant parameter availability/value changes.

## 6. Status area integration

The existing Actions status UI may currently be command-ACK centric. Extend it minimally if necessary so it can truthfully represent all three operation families.

Required user-visible semantic distinctions:

### Change Speed

- Pending
- Command accepted
- Rejected/Unsupported/Timeout/Cancelled

Do not claim measured-speed telemetry confirmation.

### Change Altitude

Depending on vehicle adapter:

- Target sent, awaiting altitude confirmation
- Command accepted, awaiting altitude confirmation
- Altitude confirmed/reached
- Sent/accepted but telemetry confirmation timed out
- Rejected/Unsupported/Cancelled where applicable

Do not show a fake ACK for a setpoint message that has none.

### Set Loiter Radius

- Writing parameter
- Parameter value confirmed
- Parameter write rejected/timed out/cancelled

Do not label parameter confirmation as Command ACK.

## 7. Input validation

Invalid values must be blocked before backend invocation and visibly indicated according to existing form conventions.

At minimum:

- no NaN/infinite values;
- speed > 0;
- altitude within typed backend's valid domain;
- loiter radius > 0 and within known metadata bounds when available.

Changing one input must not mutate another field, takeoff altitude, or Expert Command ID.

## Acceptance tests

Automated ViewModel/UI tests must cover at least:

1. Copter/Rover present Ground speed only.
2. Plane presents Airspeed/Ground speed semantic choices.
3. Selecting Plane Airspeed passes the typed Airspeed choice to backend.
4. Selecting Plane Ground speed passes typed GroundSpeed.
5. No throttle option appears.
6. Altitude label/reference is explicitly HOME-relative and value is passed unchanged semantically to the typed backend after unit conversion.
7. Unsupported/non-Guided altitude state disables the action without invoking SetMode.
8. Loiter input is always positive magnitude; sign preservation remains backend-owned.
9. Persistent parameter wording is present for Loiter Radius.
10. All three controls have independent CanExecute state.
11. Invalid input does not call backend.
12. Speed accepted is not displayed as telemetry-confirmed.
13. No-ACK altitude target is not displayed as command-ACK accepted.
14. Parameter confirmation is displayed as parameter confirmation, not command ACK.
15. Selected vehicle change refreshes values/capabilities without carrying pending state to the wrong vehicle.
16. Existing core Actions controls, mission intervention section, Zero Altitude, and Expert MAV CMD retain correct bindings.
17. Narrow-layout rendering follows existing responsive patterns without clipping/fixed-width regressions.

## Manual SITL verification

### Copter

- Guided mode: set a different ground speed and verify ACK/status.
- Guided mode: command a HOME-relative target altitude and verify the UI transitions from target sent to telemetry-confirmed when reached.
- Verify changing altitude does not change mode automatically.

### Plane

- Verify Airspeed and Ground speed choices produce distinct typed operations.
- Verify Guided altitude operation using the Task 05 Plane adapter.
- Verify Loiter Radius parameter write, including preservation of an existing negative sign/direction.

### Rover

- Verify Ground speed only.
- Verify altitude action is unavailable.

Record exact preconditions and observed statuses.

## Build/test gate

Build the UI/app projects and run Task 05 tests plus Actions/ViewModel/UI tests.
