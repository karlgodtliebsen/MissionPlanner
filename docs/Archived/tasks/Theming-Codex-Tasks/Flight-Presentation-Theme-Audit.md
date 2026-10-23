# Flight presentation theme audit

The flight-data, mission-planning, map, and shared top/status-bar source was reviewed
at the application-versus-domain boundary.

## Application chrome

- Flight Data and Flight Planner root surfaces use `Surface`.
- Splitters, dock borders, panels, map overlays, and elevation-profile chrome use
  `Outline`, `OutlineVariant`, surface-container, and inverse-surface roles.
- Quick, Actions, Messages, and other tab content inherits semantic application
  styles; warning callouts use `Warning` and `WarningContainer`.
- The top bar uses `Surface`; replay/read-only source mode uses `Warning`.
- The status bar uses `Surface`, `OnSurfaceVariant`, `Success`, and `DisabledText`.
  The ViewModel exposes connection state rather than constructing theme colors.
- Connection controls remain normal themed buttons and do not recolor map content.

## Preserved operational visualization

- Mapsui map tiles and OpenStreetMap imagery are not assigned application palette
  colors. Mission Blue therefore changes surrounding chrome, not the map imagery.
- HUD rendering retains its black instrument background and domain-specific flight
  symbology.
- Mission overlays, elevation data, paths, vehicle markers, and geofence graphics
  retain their domain colors where color communicates flight state or geometry.
- OSD preview colors remain a preview of the vehicle display, not application chrome.

This source audit establishes the intended boundary. Live connected/SITL rendering is
covered by the runtime regression checklist and requires an interactive vehicle session.
