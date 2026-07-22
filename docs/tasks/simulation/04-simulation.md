# Simulation 04 — Location, environment, sensor, and fault controls

## Objective

Add simulation controls for start location, weather/environment, sensor injection, and bounded fault scenarios using supported SITL mechanisms.

## Dependencies

Simulation task 03.


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

Use documented SITL parameters/commands and runtime adapters. Separate profile-time options from runtime controls. Do not invent unsupported controls.

## Implementation requirements

1. Add location presets and latitude/longitude/altitude/heading with map selection.
2. Add wind direction/speed/turbulence and other environment values supported by selected SITL version.
3. Add runtime sensor controls for GPS, compass, battery, rangefinder, RC, and failures only when documented.
4. Represent each control with capability/version availability and current/requested state.
5. Require confirmation and automatic reset for hazardous/failure injection.
6. Record scenario events with simulation time.
7. Allow save/load scenario presets separate from launch profiles.
8. Ensure controls target the correct SITL instance/vehicle.

## Tests

- Location/range/unit validation.
- Capability/version filtering.
- Fault start/reset/cancel tests.
- Multi-instance targeting tests.

## Acceptance criteria

- Supported environment changes can be applied and observed.
- Unsupported controls are hidden/explained.
- Failure injection is bounded and resettable.
- Scenarios are reproducible and instance-specific.
