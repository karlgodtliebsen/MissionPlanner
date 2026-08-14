MissionPlanner can use the operating system's geolocation API to determine the **Ground Control Station (GCS)** position. This helps place the operator on the map and supports location-aware UI features.

## Windows location settings

On Windows 11, first check:

`Settings > Privacy & security > Location`

Ensure **Location services** are enabled and that Windows allows the application to use location when required.

## If Windows Location is enabled but no GCS position is available

1. Press `Win + R`.
2. Enter `services.msc` and press Enter.
3. Find **Geolocation Service**.
4. Ensure the service is not **Disabled**.
5. If necessary, change the Startup type to **Manual** (or **Automatic** when required by the Windows configuration) and start the service.
6. Return to MissionPlanner and retry geolocation.

The Windows service name is `lfsvc`.

> The Windows geolocation service affects the position of the computer running MissionPlanner. It does not control the GPS receiver or position solution inside the flight controller.

## Accuracy

A desktop PC may determine its position from network and Windows location providers rather than from a dedicated GNSS receiver. The reported GCS location can therefore be less accurate than the vehicle's GPS position.
