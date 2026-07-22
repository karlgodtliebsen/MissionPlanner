# Flight Data 05 — Gauges and comprehensive status telemetry

## Objective

Complete Gauges and Status tabs using a reusable telemetry-field catalog, freshness tracking, units, and user-selectable layouts.

## Dependencies

Flight Data task 01 and the MAVLink domain-promotion work.


## Repository constraints

- Work only under `src/`, `docs/`, `scripts/`, and test-data folders belonging to the new solution.
- Treat `src-v.1.38/` as read-only reference material. Never modify, format, move, or include legacy files in commits.
- Preserve the existing layered architecture: wire protocol in `MissionPlanner.MavLink`, transport in `MissionPlanner.Transport`, application/domain behavior in `MissionPlanner.Core`, and MAUI presentation in `MissionPlanner.App`.
- Do not call MAVLink transports directly from views or code-behind. Use application/domain services injected into view models.
- Keep code-behind limited to view lifecycle and unavoidable platform/UI integration.
- Use CommunityToolkit.Mvvm patterns already present in the solution.
- Reuse `VehicleId`, `VehicleSession`, `VehicleRegistry`, command ACK tracking, parameter services, domain event hub, generated MAVLink messages, and decoder catalog rather than creating parallel abstractions.
- All vehicle-changing operations must be connection-aware, cancellation-aware, target the active `VehicleId`, and expose command acknowledgement or an explicit failure state.
- Add structured logging at workflow boundaries; do not log high-frequency telemetry on every update.
- Add unit tests for domain/application behavior and view-model tests. Add smoke/integration tests only where they are deterministic.
- Update DI registrations in the existing configurators and add DI validation tests.
- The solution must build with nullable warnings treated consistently with the repository.

## Scope

Implement both `GaugesTabViewModel` and `StatusTabViewModel` in one cohesive slice so they share data definitions. Reuse immutable `VehicleState` and promoted observations; do not independently decode MAVLink in the UI.

## Implementation requirements

1. Create a telemetry descriptor catalog with stable key, label, value accessor, unit, formatting, valid range, category, and required source.
2. Include attitude, heading, speeds, altitude variants, climb rate, GPS, battery, current, consumed capacity, radio quality, throttle, mode, armed state, EKF, vibration, CPU/load, temperatures, wind, rangefinder, and servo outputs where available.
3. Track value timestamp/freshness and display unavailable/stale explicitly.
4. Gauges: allow a small configurable dashboard of dial/bar/text gauges with persisted layout per user/device.
5. Status: searchable/grouped virtualized list of all promoted telemetry with raw and formatted values.
6. Support metric/imperial/aviation unit preferences through one conversion service.
7. Throttle UI updates independently of telemetry ingest to avoid rendering at packet rate.
8. Add reset-to-default and export snapshot.

## Tests

- Descriptor formatting and unit conversion tests.
- Fresh/stale/unavailable transitions.
- Layout persistence and invalid-key migration.
- High-rate update test proving bounded UI notifications.

## Acceptance criteria

- Both tabs are functional and consistent.
- No duplicated telemetry mapping in XAML/view models.
- Status can display all available promoted observations.
- Gauge rendering remains responsive under SITL telemetry rates.
