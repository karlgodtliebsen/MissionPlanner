MissionPlanner is organized around a small number of persistent UI elements and task-focused pages. This Introduction explains the new layout before you connect a vehicle or start changing configuration.

## The normal workflow

1. **Connect** to the vehicle from the Top Bar.
2. **Inspect** vehicle identity, status, telemetry, and health.
3. **Configure** the vehicle when setup changes are required.
4. **Plan** or load a mission.
5. **Upload and verify** the mission on the vehicle.
6. **Fly and monitor** the vehicle from Flight Data.

The Flyout Menu is the main navigation surface. The Top Bar remains available while you move between pages, and the Status Bar provides persistent information such as Ground Control Station location and unit selection.

> MissionPlanner assists with configuration, planning, and vehicle operation, but it does not replace operational checks. Before arming, verify the vehicle, firmware, sensors, control direction, flight mode, failsafes, power system, propeller installation, and the intended mission.

## A new interface, not a new MAVLink

The new UI changes how information is organized and presented. Vehicle telemetry and commands still flow through the MAVLink connection. Moving to another MissionPlanner page does not itself disconnect, arm, or change the vehicle.
