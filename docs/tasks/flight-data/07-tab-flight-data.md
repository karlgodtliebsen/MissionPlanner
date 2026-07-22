# Flight Data 07 — Transponder and payload control

## Objective

Complete Transponder and Payload Control tabs using capability-discovered components and dedicated protocol services for ADS-B/transponder, camera, gimbal, and mount control.

## Dependencies

Flight Data tasks 01–02 and generated peripheral protocol coverage.


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

Treat these as component protocols, not generic vehicle-state handlers. Use component discovery, generated MAVLink models, command ACKs, camera information/settings/capture status, gimbal manager/device messages, mount status, and ADS-B/transponder messages available in the generated dialect.

## Implementation requirements

1. Add a component inventory per vehicle keyed by component ID and advertised capabilities.
2. Transponder: show identity/status, squawk, mode, health/faults, pressure altitude, and traffic-related status where available; gate configuration writes by support.
3. Payload: discover cameras/gimbals, select component, show capabilities and current mode/status.
4. Implement camera capture, start/stop video, zoom/focus where supported, and gimbal pitch/yaw control through dedicated services.
5. Add rate limiting and command serialization for continuous controls.
6. Provide clear Unsupported/Not discovered states rather than empty controls.
7. Do not assume component ID 100 or one camera/gimbal.
8. Preserve per-component state across tab switching and reset on vehicle change.

## Tests

- Multi-component discovery and selection tests.
- Capability-gated command tests.
- Component-targeted encoder/ACK tests.
- Rate-limit/cancellation/reconnect tests.
- SITL/plugin smoke tests when corresponding simulated components exist.

## Acceptance criteria

- Tabs function for supported components and degrade clearly for unsupported vehicles.
- Commands target the selected component, not only the autopilot component.
- No hard-coded component IDs.
- Continuous controls stop promptly on release/disconnect.
