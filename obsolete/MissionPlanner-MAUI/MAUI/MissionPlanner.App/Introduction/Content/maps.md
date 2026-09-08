Maps are used throughout MissionPlanner because vehicle position, mission items, home position, routes, and the Ground Control Station all have a geographical context.

## Basic map interaction

Typical map interaction includes:

- drag or pan to move across the map;
- use the mouse wheel or pinch gesture to zoom;
- select or create mission items in mission-planning views;
- inspect the current vehicle position and route;
- return to or follow the vehicle when a page provides that function.

## Three positions that should not be confused

**Vehicle position** is received from vehicle telemetry.

**Home** is an ArduPilot/Mission concept and should not be assumed to be identical to the current GCS location.

**GCS position** is the location of the computer or device running MissionPlanner and comes from the operating system's geolocation service.

## Map source and network access

The visible map background comes from the map source selected in MissionPlanner. Depending on the configured source and the features available in the current build, that may involve online raster tiles, cached data, or an offline map source.

The geographical overlays are MissionPlanner data. The background map is provider data. A missing background map therefore does not necessarily mean that vehicle telemetry or mission coordinates are missing.

## Mission editing

The MissionPlanner mission map is shared between mission-oriented views. A waypoint or route should be treated as a projection of the mission model, not as independent graphics that exist only on the map.

When editing a mission, verify waypoint order, command, altitude reference, coordinates, and any command-specific parameters before upload.
