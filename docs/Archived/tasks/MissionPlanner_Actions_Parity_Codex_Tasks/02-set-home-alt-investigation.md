# Set Home Alt legacy investigation

## Outcome

The feature is intentionally deferred. The legacy source proves that **Set Home Alt** is a
local display-offset toggle and does not send `MAV_CMD_DO_SET_HOME` (or any other MAVLink
command), but the implementation conflicts with its own user-facing description. Reproducing
either interpretation without a product decision could display a safety-relevant altitude in
the wrong reference.

## Legacy trace

The legacy control is `BUT_Homealt` in `src-v.1.38/GCSViews/FlightData`.

- The resource tooltip says: “Set the current display alt as 0, ie home alt is shown as 0”.
- `BUT_Homealt_Click` toggles `CurrentState.altoffsethome` between zero and
  `-CurrentState.HomeAlt / CurrentState.multiplieralt`.
- `CurrentState.alt` displays `(_alt - altoffsethome) * multiplieralt`.
- `GLOBAL_POSITION_INT` assigns `relative_alt / 1000` to `_alt` through the `alt` setter.
- `CurrentState.HomeAlt` returns `HomeLocation.Alt`, an MSL home altitude supplied by home
  state.
- The handler sends no MAVLink message, changes no autopilot state, and waits for no ACK.

For a home MSL altitude of 100 m and a vehicle relative altitude of 0 m, the implementation
above displays 100 m after the toggle, not 0 m. This contradicts the tooltip and prevents a
single legacy-compatible semantic from being established confidently.

## NextGen architecture finding

NextGen already retains both `AltitudeMslMeters` and `RelativeAltitudeMeters` in
`VehiclePositionState`. `VehicleHudDataService` currently prefers the vehicle-reported
relative altitude. A faithful local-offset feature would therefore belong in a per-vehicle
HUD/presentation altitude-reference service, not `IVehicleCommandService`, and must never
manufacture a MAVLink acknowledgement.

## Decision required

Before implementation, choose one explicit product behavior:

1. Zero the currently displayed altitude and retain a per-vehicle GCS display offset until
   reset or disconnect.
2. Toggle the HUD between vehicle-relative altitude and MSL altitude.
3. Reproduce the legacy arithmetic exactly, including its apparent contradiction.

The control remains absent until that choice is made. **Set Home Here** remains unchanged
and continues to be the typed, acknowledged operation that changes autopilot HOME position.

## Future SITL verification

Once a behavior is selected, verify with a non-zero simulated home MSL altitude:

1. Record HOME position, home MSL altitude, vehicle MSL altitude, vehicle relative altitude,
   and the HUD altitude.
2. Invoke **Set Home Alt** and confirm no MAVLink command is transmitted.
3. Confirm HOME latitude, longitude, and altitude are unchanged.
4. Confirm only the selected vehicle's HUD reference changes.
5. Reset the display reference and confirm the vehicle-reported relative altitude is shown.
6. Invoke **Set Home Here** separately and confirm it still sends the acknowledged HOME
   command and changes vehicle HOME rather than the local display reference.
