# Codex Task 02 — Zero / Reset Display Altitude

## Goal

Replace the ambiguous legacy **Set Home Alt** behavior with an explicit, per-vehicle, GCS-local display reference:

- **Zero Altitude** when no local zero is active;
- **Reset Altitude** when a local zero is active.

This task implements both backend/presentation state and the small Actions UI control because it is not a MAVLink command and does not depend on the mission-command tasks.

## Fixed product semantics

Do not reproduce the old MissionPlanner arithmetic.

Do not call `MAV_CMD_DO_SET_HOME`.

Do not modify autopilot HOME.

The local display reference is:

```text
zeroReference = current RelativeAltitudeMeters
DisplayedAltitude = RelativeAltitudeMeters - zeroReference
```

When reset:

```text
zeroReference = null
DisplayedAltitude = normal existing relative-altitude display path
```

This is a presentation operation, not a vehicle command.

## 1. Find the correct state location

Inspect the current altitude/HUD pipeline, including the existing `VehiclePositionState`, `VehicleHudDataService`, selected vehicle/session lifetime, and any presentation-state services.

Store the zero reference in a per-vehicle/per-session presentation service or state object. Do not store it as a static value or a single global property in `ActionsTabViewModel`.

Example conceptual API:

```csharp
bool HasLocalAltitudeZero(VehicleId vehicleId);
bool TryZeroAltitude(VehicleId vehicleId, double currentRelativeAltitudeMeters);
void ResetAltitude(VehicleId vehicleId);
```

Names are illustrative; follow repository conventions.

## 2. Apply the zero only to relative altitude

The offset is defined in the vehicle's **relative-to-HOME altitude domain**.

Requirements:

- Zero can be established only when a finite current `RelativeAltitudeMeters` value is available.
- The zero offset is applied only when rendering from relative altitude.
- Never subtract a relative-altitude zero from an MSL value.
- If relative altitude temporarily becomes unavailable and the existing HUD falls back to another source, preserve the existing fallback semantics without applying this offset to the wrong frame.
- When relative altitude becomes available again in the same session, the local zero may resume until explicitly reset or the session ends.

## 3. Lifetime and isolation

The zero reference must:

- apply only to the selected target vehicle;
- survive ordinary view recreation/navigation while the same vehicle session remains alive, if that matches existing session-scoped presentation state;
- reset on disconnect/session replacement;
- never transfer to another vehicle with the same or different SysId.

## 4. Actions UI

Add a compact control near the existing HOME/altitude-related Actions controls without disturbing the current hierarchy.

Required behavior:

- inactive state label: **Zero Altitude**;
- active state label: **Reset Altitude**;
- explanatory tooltip/help text equivalent to:
  - “Sets the current displayed relative altitude to 0. Vehicle HOME is not changed.”
- disabled when no connected selected vehicle or no usable relative altitude is available for creating a zero;
- Reset remains available while the local zero exists for the active session.

Do not label this control **Set Home Alt**.

Keep **Set Home Here** unchanged and visually distinct.

## 5. Status reporting

Do not route this through MAVLink command ACK infrastructure.

If the application has a generic local action/toast/status mechanism, it may report:

- “Display altitude zeroed”
- “Display altitude reference reset”

Do not show `COMMAND_ACK`, “accepted by autopilot”, or telemetry-command confirmation for this operation.

## Out of scope

- No mission intervention commands.
- No new altitude unit system.
- No autopilot HOME modification.
- No persistence across application restarts.

## Acceptance tests

Automated tests must cover at least:

1. With relative altitude 37.5 m, Zero Altitude establishes a reference of 37.5 m and displayed altitude becomes 0 m.
2. If relative altitude then becomes 42.0 m, displayed altitude becomes 4.5 m.
3. Reset restores the normal relative altitude display (42.0 m in the example).
4. The feature never invokes `SetHomeHereAsync` or any MAVLink command sender.
5. Vehicle HOME state is unchanged.
6. A zero on vehicle A does not affect vehicle B.
7. Disconnect/session replacement clears vehicle A's zero reference.
8. A zero cannot be created from missing/NaN/infinite relative altitude.
9. An MSL fallback value is not transformed by the relative-altitude zero.
10. `Set Home Here` existing behavior and tests remain unchanged.
11. The button text/state changes correctly between Zero Altitude and Reset Altitude.
12. Existing HUD altitude tests remain green.

## Manual verification

Using SITL with a non-zero HOME MSL altitude:

1. Record vehicle HOME, MSL altitude, relative altitude, and displayed HUD altitude.
2. Invoke Zero Altitude and verify only the display reference changes.
3. Move/climb and verify the display is relative to the local zero point.
4. Reset and verify normal relative altitude display returns.
5. Invoke Set Home Here separately and verify it remains an autopilot HOME operation, not the local display operation.

## Build/test gate

Build affected Core/UI projects and run HUD/FlightData/Actions tests.
