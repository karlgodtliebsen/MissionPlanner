**Setup and Configuration** contains functions that inspect or change the vehicle itself. This is different from MissionPlanner Preferences, which changes the application running on the Ground Control Station.

## Before changing vehicle setup

- Confirm that MissionPlanner is connected to the intended vehicle.
- Confirm the detected vehicle family, firmware, and hardware where that information is available.
- Remove propellers whenever a calibration, motor-related operation, firmware operation, or other procedure could unexpectedly drive actuators.
- Read warnings shown by the individual setup page before continuing.

## Typical setup work

Depending on the connected vehicle and supported features, setup can include:

- firmware installation or update;
- board and vehicle configuration;
- sensor calibration;
- radio and control configuration;
- parameters;
- communication interfaces;
- safety and failsafe settings.

Some changes are applied to the flight controller immediately, while others require an explicit write/apply operation or a reboot. The individual page should make that behavior clear.

## Parameters

Parameters are vehicle configuration, not UI preferences. A parameter name, value, unit, range, and reboot requirement should be considered together. Avoid changing a parameter solely because another vehicle uses a different value.
