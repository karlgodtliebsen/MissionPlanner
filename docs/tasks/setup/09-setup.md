# Setup 09 — Optional hardware and serial/CAN peripheral setup

**Implementation status:** Completed 2026-07-22.

## Objective

Implement discoverable setup for optional hardware: GPS, airspeed, rangefinder, optical flow, proximity, CAN, serial ports, telemetry radios, and network peripherals.

## Dependencies

Setup task 01, parameter metadata, and component inventory.


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

Use a plugin-like workflow catalog driven by parameter presence, component discovery, and capabilities. Avoid one giant view model.

## Implementation requirements

1. Define peripheral setup modules with availability predicate, required parameters/messages, validation, and view model factory.
2. Implement serial-port protocol/baud configuration with conflict detection.
3. Add GPS ordering/type/status and moving-baseline options when present.
4. Add airspeed, rangefinder, optical-flow, and proximity instance setup with live status.
5. Add CAN driver/node visibility and relevant parameter editing.
6. Add telemetry radio/network setup only through dedicated services; do not expose secrets in logs.
7. Support sparse instances and firmware-version differences through parameter presence.
8. Provide reboot-required aggregation and post-reboot verification.

## Tests

- Module discovery tests.
- Serial conflict and sparse-instance tests.
- Validation tests for each first-wave peripheral.
- Secret redaction and reconnect tests.

## Acceptance criteria

- Optional hardware appears only when applicable.
- Multiple instances are supported.
- Conflicting serial assignments are detected before write.
- The architecture permits adding peripherals without modifying a monolithic switch.
