The **Status Bar** provides information that remains useful while the main page changes.

## Ground Control Station location

Location information in the Status Bar represents the position of the device running MissionPlanner when platform geolocation is available.

This is different from the vehicle position:

- **GCS location** comes from Windows, Android, iOS, or macOS geolocation services.
- **Vehicle location** comes from vehicle telemetry, normally using the vehicle's GNSS/GPS solution.

A vehicle can therefore have a valid GPS fix even when the computer running MissionPlanner cannot determine its own location.

## Units

The Status Bar can be used to select the preferred display units. Unit selection changes how MissionPlanner **presents** measurements; it does not change the underlying MAVLink values or reconfigure the flight controller merely by changing a display unit.

Depending on the value being shown, MissionPlanner can present measurements in forms such as metres or feet, metres per second or other speed units, and other configured unit systems.

When comparing MissionPlanner values with flight-controller logs or parameter documentation, always pay attention to the unit shown next to the value.
