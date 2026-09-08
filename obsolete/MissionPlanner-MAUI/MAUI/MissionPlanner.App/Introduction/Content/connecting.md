A vehicle connection is the boundary between browsing MissionPlanner and working with live flight-controller state.

## Before connecting

Verify that the selected transport identifies the intended connection. For serial/USB this normally means the correct device/COM port and baud rate. For network transports it means the correct local/remote endpoint and protocol configuration.

## After connecting

MissionPlanner begins building its live vehicle state from MAVLink messages. Pages can then show information such as:

- vehicle family and autopilot;
- firmware information when available;
- flight mode and armed state;
- attitude and position;
- GPS, power, radio, navigation, and health data;
- parameters and missions when requested.

The Top Bar remains the authoritative visual place to check whether MissionPlanner considers the vehicle connected.

## Disconnecting

Disconnect before changing physical wiring, moving a serial connection to another application, or beginning a workflow that requires exclusive access to the flight controller or bootloader.
