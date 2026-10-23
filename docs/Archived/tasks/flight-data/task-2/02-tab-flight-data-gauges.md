# Flight Data 02 — Gauges and shared telemetry descriptor catalog

Status: **Completed.**

## Objective

Implement `GaugesTabView` and establish the telemetry descriptor/catalog infrastructure reused by the Status tab.

Apply all constraints from `00-README.md`.

## Existing implementation to reuse

`VehicleState` already contains flight, position, motion, GPS, power, radio, navigation, health, estimator, vibration, pressure, range, environment and time state. `IPlannerSettingsService` already persists unit preferences and `PlannerTelemetrySettings.DisplayRateHz`.

## Shared telemetry catalog

1. Add Core/application abstractions such as:

   ```text
   TelemetryFieldDescriptor
   TelemetryFieldCategory
   TelemetryValueSnapshot
   TelemetryFreshness
   ITelemetryFieldCatalog
   ITelemetrySnapshotProjector
   ```

2. Each descriptor must define a stable key, label, category, `VehicleState` accessor, timestamp accessor, unit kind, format, recommended gauge type/range, warning thresholds where meaningful, and vehicle-family applicability.
3. Do not reflect over generated MAVLink messages. Project promoted domain state only.
4. Preserve raw SI/domain values separately from formatted display values.
5. Centralize metric/imperial/aviation conversion and explicit stale/unavailable handling.

## Initial field set

Include at least:

```text
roll, pitch, yaw and heading
ground speed, air speed and vertical speed
MSL, relative and terrain-relative altitude
distance to home/vehicle/waypoint when available
GPS fix and satellites
battery voltage/current/remaining/consumed capacity
radio/link quality
mode and armed state
throttle when promoted
EKF quality and vibration
wind and rangefinder
system load and temperatures when promoted
```

Do not fabricate unavailable values.

## Gauge controls and dashboard

1. Add MissionPlanner-owned controls for radial/dial, bar, and numeric/text gauges. Prefer Avalonia custom controls, Skia integration, or simple primitives.
2. Do not add a commercial/large chart dependency without an ADR.
3. Support light/dark themes, accessibility, stale/unavailable overlays and bounded animation.
4. Provide a default dashboard: airspeed, ground speed, altitude, climb rate, heading and battery.
5. Allow field selection, gauge type, ordering and reset-to-default.
6. Persist layout through `IPlannerSettingsService`; increment schema and add migration tests.
7. Publish UI changes at `PlannerTelemetrySettings.DisplayRateHz` and update tile objects in place.
8. Provide cross-platform reorder controls; drag/drop is optional.

## Lifecycle

- Observe state only for the transient ViewModel lifetime.
- Stop sampling/timers on disposal.
- Never clear the bound gauge collection from `Dispose()`.
- Read-only gauges may work with replay state when supported by the replay architecture.

## Tests

Cover descriptor accessors, units/formatting, stale transitions, bounded UI notification rate, layout persistence/migration, unknown descriptor keys, family filtering, active-vehicle changes and disposal. Add control-level rendering/bounds tests where practical.

## Documentation

- Add Gauges architecture/default fields to `docs/FLIGHT_DATA.md`.
- Update `docs/FEATURES.md`.
- Update `docs/PLANNER_SETTINGS.md` for layout and schema changes.
- Update promotion documents only when new domain state is added.

## Acceptance criteria

- Functional gauges replace the placeholder.
- Gauges and Status share one descriptor catalog.
- SITL telemetry does not overwhelm rendering.
- Layout and units persist with migration coverage.
