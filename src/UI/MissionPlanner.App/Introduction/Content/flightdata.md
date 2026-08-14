**Flight Data** is the live operational view. It combines telemetry, map information, and task-specific tabs so the operator can monitor the vehicle without navigating through setup pages.

## Live data

Values shown in Flight Data are projections of the current vehicle state built from MAVLink telemetry. Different values update at different rates because they originate from different MAVLink messages and flight-controller streams.

Examples include:

- attitude: roll, pitch, and heading/yaw;
- vehicle position and altitude;
- ground or air speed when available;
- GPS fix and satellite information;
- battery and power data;
- current mode and armed state;
- mission/navigation state.

## Tabs

The Flight Data tabs divide a large amount of information into focused views. Use the map when geographical context matters and switch to specialized tabs when you need denser telemetry or a particular instrument view.

A page change in MissionPlanner does not stop telemetry processing. Returning to Flight Data should therefore show the current state, not restart the vehicle connection.
