# In-flight adjustment backend investigation

## Outcome

The typed backend is deferred. The legacy implementations are proven, but they expose
vehicle-specific ambiguity and require protocol/application capabilities that NextGen does
not yet provide through a safe typed operation.

## Change Speed

Legacy sends `MAV_CMD_DO_CHANGE_SPEED` with parameter 1 equal to 0, parameter 2 equal to the
entered value, and all other parameters zero. The displayed initial value, however, can come
from three different sources:

- Copter `WP_SPEED_MAX / 100` (ground/navigation speed in m/s);
- Plane `TRIM_ARSPD_CM / 100` (airspeed in m/s); or
- Plane `TRIM_THROTTLE` when no airspeed sensor is used, while the button changes its label
  to **Change Throttle**.

MAVLink speed type 0 denotes airspeed. Therefore the single legacy encoding does not
unambiguously represent the different values shown by the UI. A typed API must first define
supported vehicle-family/sensor combinations and use an explicit speed type; copying the
legacy parameter blindly would preserve a semantic bug.

## Change Altitude

Legacy treats the entered value as an absolute target in metres relative to HOME. It calls
`setNewWPAlt`, which creates a waypoint with zero latitude/longitude and sends it through the
mission-item protocol with sequence 0, frame `GLOBAL_RELATIVE_ALT`, and `current = 3` (the
legacy guided-mode special form). It is not a delta and not an MSL or terrain altitude.

NextGen's mission encoder currently supports ordinary mission transfer but does not expose a
typed guided mission-item operation or its acknowledgement lifecycle. This must be added in
the MAVLink/Application boundary before the Actions backend can offer the operation.

## Set Loiter Radius

Legacy does persist the value. It writes the first available parameter from
`LOITER_RAD`/`WP_LOITER_RAD`, converting the displayed distance unit back to metres. This is
not a temporary command, and the legacy handler does not encode direction separately. A
NextGen implementation must use the existing typed parameter editing/write service, retain
selected-vehicle isolation, and report parameter acknowledgement rather than a command ACK.

## Required prerequisites

- Define a vehicle-family-aware speed request model and supported speed types.
- Add an acknowledged typed guided mission-item operation for absolute HOME-relative
  altitude without exposing `current = 3` to UI code.
- Add a typed operational parameter write for the correct loiter-radius parameter and make
  persistence explicit in UI wording and confirmation.
- Add independent policy entries once firmware support and required modes are established.

No production API or policy entry is added until these semantics are resolved.
